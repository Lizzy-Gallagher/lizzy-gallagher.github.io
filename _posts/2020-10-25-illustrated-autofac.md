---
title:  "An illustrated guide to Autofac"
date:   2020-10-25 10:00:00 -0500
tags: dotnet autofac
---

[Autofac](https://autofac.org/) is a popular .NET "[inversion of control](https://martinfowler.com/articles/injection.html)" library. Designed to be unobtrusive, Autofac fades into the background of large applications.

This article is for those who have used Autofac without understanding the *why* behind it. If you want specific API details, I highly recommend the [official documentation](https://autofaccn.readthedocs.io/en/latest/).

This post will take a step back from specific API details and instead explain Autofac using the analogy of a restaurant.

### Autofac, a restaurant kitchen

![You provide autofac a cookbook full of recipes that it uses to fill customer orders](/assets/images/autofac-main.png)

#### Why do restaurants exist?

Most of us have kitchens, access to grocery stores, and (thanks to the internet!) step-by-step instructions for a million different dishes.

Why do restaurants still exist? Because cooking complex meals yourself requires effort and precision.

**Effort.** A simple recipe might take 15 minutes; a complex recipe many hours.

**Precision.** For any recipe with more than five steps, I am bound to mismeasure something or undermine perfection with a short cut.

For a special dish -- say, a Beef Wellington (a 6 hour effort on a good day), we'd all concede that outsourcing this complex dish to a restaurant's kitchen would be a good trade-off of time and effort.

#### Why does Autofac exist?

Like cooking, managing dependencies yourself requires both effort and precision.

**Effort.** If manually creating dependencies, you instantiate components by `new`-ing them which quickly gets hard to read and maintain. Adding a new dependency to a common component requires propagating that change to 100% of instantiations.

**Precision.** Complex applications might require careful handling of component disposal (to prevent memory leaks) or careful component reuse (for caching purposes).

Imagine we were to cook a Beef Wellington ourselves:

```cs
// without Autofac or another IOC solution
public class MyMeal()
{
    public MyMeal(Pate pateLeftOverFromLastWeek)
    {
        var beefWellington = new BeefWellington(
            new Steak { Doneness = Doneness.MediumRare },
            // pâté is expensive! Reuse it if we can.
            pateLeftOverFromLastWeek,
            // duxelles has so many ingredients :(
            new Duxelles(
                new WhiteButtonMushrooms().TrimEnds(),
                new Shallot(),
                ...
                // (this is an option I would like to set when 
                // instantiating everything I eat)
                o => o.ThrowIfSoHotItWillBurnMyTongue(true)
            ),
            ...
        );

        beefWellington.Enjoy(); // finally!
    }
}
```

Oof.

By using Autofac, we can separate instantiation from business logic and reuse our configuration instructions again and again in our code!

```cs
// with Autofac! So nice!
public class MyMeal()
{
    // You specify how this dependency is created in your Autofac configuration.
    public MyMeal(BeefWellington beefWellington) // dependency injection!
    {
        beefWellington.Enjoy();
    }
}

// re-use is really easy!
// configure Autofac to either reuse the same "recipe" or even the exact same copy!
public class YourMeal()
{
    public YourMeal(BeefWellington beefWellington, Wine wine)
    {
        wine.Sip();
        beefWellington.Enjoy();
    }
}
```

And this magic happens by handing off your recipe to Autofac (in `Startup.cs`), your restaurant.

```cs
// when properties are important, specify them!
builder.Register(() => new Steak { Doneness = Doneness.MediumRare }); 

// when you don't need anything fancy!
builder.RegisterType<Shallot>();

// when you need caching!
builder.RegisterType<Pate>().SingleInstance();

// when you need to pick a constructor!
builder.RegisterType<Duxelles>(cc => new Duxelles(
    cc.Resolve<WhiteButtonMushrooms>().TrimEnds(), 
    cc.Resolve<Shallot>(),
    o => o.ThrowIfSoHotItWillBurnMyTongue(true)));

// and tie it all together!
builder.RegisterType<BeefWellington>();
```

#### Vocabulary

![You provide autofac a cookbook full of recipes that it uses to fill customer orders](/assets/images/autofac-main.png)

Now that our appetite has been whetted, let's break down this restaurant metaphor.

> You provide Autofac a cookbook full of recipes that it uses to fill customer orders.

**Customer orders == Resolving components** As your application runs, whenever you need a component you "resolve" it from the container. This act of "resolving" dependencies happens throughout your app's lifecycle, e.g. on each HTTP request. Resolving a component is like placing an order -- restaurants don't prebake all their food, so they need to start cooking when an order comes in!

**Recipes == Registrations** In code, before your application runs, you define a set of instructions called **registrations** that Autofac uses to create the components when they are *resolved* from the container. Like recipes, your registrations are the instructions followed to create the end product.

**Cookbook == Container** During application startup, all the registrations are added to an immutable **container**. At runtime, recipes for resolving components are pulled from this container. Like a cookbook, it is immutable and cannot be changed during runtime / dinner hours. Like a cookbook, if a customer orders something not on the menu / attempts to resolve a component with no registration, you're out of luck.

#### What options do I have when writing recipes?

##### Register with a specific lifetime

![Image showing the different lifetimes available in Autofac](/assets//images/autofac-lifetimes-trim.png)

There are three types of **lifetimes**:

- Single instance
- Instance per Dependency
- Instance per Lifetime Scope

**Single instance.** Every time you resolve the component, you get the exact same instance of it. This is useful if you are caching something inside that component. Think of this like a pot of soup: the soup is made in a big batch at the beginning of the evening and then every customer is served from the same batch all night. The soup is only cooked once.

**Instance per Dependency.** Every time you resolve the component, you get a new instance of it. This is the default registration and good to use if all your components are stateless / easy. This is like a made-to-order burger. It is not made ahead of time, only when you order it.

**Instance per Lifetime Scope.** Every time you resolve the component *within the same lifetime*, you get the exact same instance. This is a API to use if you need to scope your component (e.g. for caching) to some unit of work, e.g. an HTTP request. This is like a bottle of wine for the table. A new one is served to each table that orders a bottle of wine, but the same bottle is shared for the whole tale.

##### Common registration APIs

![Image showing the different registration methods available in Autofac](/assets/images/autofac-3.png)

##### Register as an interface

Depending on your design philosophy, you may use a lot of interfaces or you might not! Autofac provides an easy hook to register components by their regular type or by any interface they implement.

In our restaurant metaphor, interface registration is like ordering coffee after dinner at a restaurant. When ordering coffee this way, you are rarely given a chance to specify your blend or roast.

```cs
// at a coffee shop, you may order the specific type
builder
    .Register<ColumbianSingleOriginDarkRoast>()
    .As<ICoffee>();

// after a restaurant meal, you may instead just request "ICoffee"
builder
    .Register<GroceryStoreBlend>()
    .As<ICoffee>();
```

##### Register using reflection

This is the bread and butter way to register. If you don't require any specific settings or constructor, but just want to use the defaults for each of your component's dependencies.

```cs
builder.RegisterType<Lettuce>(); // ok because no dependencies!
builder.RegisterType<LettuceSalad>(); // ok because Lettuce is the only dependency!
```

##### Register using a custom function

Remember that complicated Beef Wellington recipe earlier in this article? The Duxelles component is a good candidate for using a custom function since it requires some specific configuration. The custom function API is the most flexible as it allows you to use "new" syntax.

```cs
builder.RegisterType<Duxelles>(cc => new Duxelles(
    cc.Resolve<WhiteButtonMushrooms>().TrimEnds(), 
    cc.Resolve<Shallot>(),
    o => o.ThrowIfSoHotItWillBurnMyTongue(true)));
```

##### Register using a module

A neat, more advanced feature is bundling your registrations together using an Autofac module -- a logical grouping of registrations. This is especially handy if writing a NuGet package or otherwise want to register components that may not be publicly visible to your consumer.

```cs
builder.RegisterModule<PastryModule>();
```

![Image showing the idea of modules](/assets/images/autofac-4.png)

### Summary

Autofac is a flexible technology that is a delight to use. This post is purposely just an introduction to the main concepts of Autofac. If you want to dive deeper, you should spend some time with the [official documentation]("https://autofaccn.readthedocs.io/en/latest/getting-started/index.html") which is excellent.

### Postscript - Real life Wellingtons

Since you made it this far, here is a picture of real-life Beef Wellington my husband, parents, and I made last Christmas. It took us four hours. Next time, we all agreed, we'll order it from a restaurant.

![Image showing a beef wellington](/assets/images/autofac-wellington.jpg
