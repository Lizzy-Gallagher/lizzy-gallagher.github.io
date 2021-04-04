---
title: How to fix "resolve operation has already ended" exceptions in lambda Autofac registrations
date: 2021-04-04 17:35:00 -0500
tags: autofac documentation
---

At work today, we ran into an `ObjectDisposedException` when writing a lambda Autofac registration:

> This resolve operation has already ended. When registering components using lambdas, the IComponentContext 'c' parameter to the lambda cannot be stored. Instead, either resolve IComponentContext again from 'c', or resolve a Func<> based factory to create subsequent components from.

# Solution

This error happens at runtime when attempting to resolve a component from a "captured" `IComponentContext` that belonged to the `Register` method:

```c#
var cb = new ContainerBuilder();

// maybe the IComponentContext was captured inside the body of a Lazy<>
cb.Register(cc => 
    new ApplePie(new Lazy<Apple>(() => cc.Resolve<Apple>())));

// ...or captured as a parameter (and stored inside) the instance
cb.Register(cc => new ApplePie(cc));

// ...or captured inside the body of a Func<>
cb.Register<Func<Apple>>(cc => () => cc.Resolve<Apple>());

var container = cb.Build();
container.Resolve<ApplePie>().UseApple(); // ERROR! This resolve operation h...
```

The resolution is to resolve `IComponentContext` _outside_ of the capture context and resolve from it instead:
```c#
cb.Register(cc =>
{
    var context = cc.Resolve<IComponentContext>();
    return new ApplePie(new Lazy<Apple>(() => context.Resolve<Apple>()));
});

// depending on which pattern from above maybe instead
// ... new ApplePie(context)
// ... or () => context.Resolve<Apple>
```

As explained below, there are no meaningful downsides to this technique for 99.9% of projects. The impact is a very, very small performance hit as the `IComponentContext` from `cc.Resolve<IComponentContext>()` is a tiny bit slower than the parameter `IComponentContext` (`cc`).

# Why does this work?

**Disclaimer:** This section discusses the internals of Autofac. All details are liable to change at any time. This section is accurate as of Autofac version 6.1.0.
{: .notice--warning}

**Note:** This section is deep in the weeds of Autofac internals. For a conceptual explainer of Autofac try [An illustrated guide to Autofac](/illustrated-autofac).
{: .notice--info}

The first thing to understand is that the `IComponentContext` resolved _inside_ `Register` is not the same instance as the `IComponentContext` which is a parameter.
```c#
cb.Register(cc => {
    var context = cc.Resolve<IComponentContext>();
    context != cc; // true
    ...
});
```

The *parameter* `IComponentContext` (`cc`) is a [`DefaultResolveRequestContext`](https://github.com/autofac/Autofac/blob/e477eb4632523d8780d32fb1105a10b0af988634/src/Autofac/Core/Resolving/Pipeline/DefaultResolveRequestContext.cs), and the *resolved* `IComponentContext` (`context`) is a [`LifetimeScope`](https://github.com/autofac/Autofac/blob/e662b6bace37a569eec1e42335336b3fe015855c/src/Autofac/Core/Lifetime/LifetimeScope.cs). Both of these classes implement `IComponentContext`, an interface which exposes methods for resolving services from the container, but differ greatly in their implementation.

As its name implies, `LifetimeScope` is designed to persist for the length of a lifetime scope, e.g. an HTTP request, resolving hundreds of thousands of services across multiple threads. `LifetimeScope` is the smallest unit of "sharing" in Autofac, so this class must keep track (in a thread safe manner) any services that were created with an `InstancePerLifetimeScope` registration.

By contrast to meticulous, long-lived `LifetimeScope`, `DefaultResolveRequestContext` is an `IComponentContext` spun up exclusively for the lifetime of a single `Resolve` call, to be disposed of immediately afterward. To keep this short-lived service inexpensive, `DefaultResolveRequestContext` is not-thread safe.

Here's an example of what this "lack of thread-safety" looks like:
```c#
public class Apple { }
// ...
var cb = new ContainerBuilder();
cb.RegisterType<Apple>();
cb.Register(cc =>
{
    // attempt use 'cc' simulatenously across multiple threads
    Enumerable.Range(0, 100)
        .Select(_ => Task.Run(() => cc.Resolve<Apple>()))
        .ToList();

    // give the other threads space to execute before 'cc'
    // is disposed
    Thread.Sleep(500);

    return new object();
});

// This error will appear 0-99 times (it is a race condition after all!):
// DependencyResolutionException: Circular dependency detected: 
//      System.Object -> Apple -> Apple.
cb.Build().Resolve<object>();
```

At first blush, the error appears nonsensical, `Apple` doesn't depend on `Apple`, where is the "circular dependency?"

Because `DefaultResolveRequestContext` is not designed to be used by multiple threads, it interprets the *parallel* `Resolve<Apple>()` to be happening *sequentially*. Specifically, the [`CircularDependencyDetectorMiddleware`](https://github.com/autofac/Autofac/blob/e477eb4632523d8780d32fb1105a10b0af988634/src/Autofac/Core/Resolving/Middleware/CircularDependencyDetectorMiddleware.cs) maintains a non-thread safe stack of `Resolve` requests that occur in the current [`ResolveOperation`](https://github.com/autofac/Autofac/blob/e477eb4632523d8780d32fb1105a10b0af988634/src/Autofac/Core/Resolving/ResolveOperation.cs) in order to detect circular dependencies. For example, if the constructor for `Apple` took `Apple` as a parameter, the middleware would find a circular dependency by recognizing that `Apple` appears twice in the stack of requests. In our example, the `CircularDependencyDetectorMiddleware` errors out because it believes that our parallel requests are a recursive resolution with a circular dependency!

## Summary

- Knowing that `DefaultResolveRequestContext` is short-lived and disposed at the conclusion of a resolution operation, helps us understand the error message a the top of this blog post. We cannot capture `DefaultResolveRequestContext` for later use because it will have been disposed by then!

- Knowing that `DefaultResolveRequestContext` is not thread safe in order to be cheap, helps us understand the tradeoffs of the "fix". `DefaultResolveRequestContext` does less bookkeeping than `LifetimeScope`. Specifically `LifetimeScope` initializes a new stack (for circular dependency detection) for every `Resolve` request, but `DefaultResolveRequestContext` reuses the same stack each time. 

It is important to note that `DefaultResolveRequestContext` uses `LifetimeScope` under the hood to manage creating and sharing instances. This means that both have *identical* business logic behavior. The only trade-off is that `LifetimeScope` is a very small amount slower because of additional bookkeeping to ensure thread safety. Autofac is rarely a performance bottleneck, so 99.9% of projects can make this tradeoff without losing any sleep.

Curious about the philosophy toward thread safety in Autofac? The ["Concurrency" section](https://autofaccn.readthedocs.io/en/latest/advanced/concurrency.html) of their documentation is quite good.