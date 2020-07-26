---
#layout: post
title:  "Query interception in EntityFramework Core"
date:   2020-07-25 7:00:00 -0500
categories: 
---
At work, my team is decoupling our NuGet libraries from Entity Framework to enable consumers to switch to Entity Framework Core. To decide which features should stay / go, we needed to evaluate how compatible Entity Framework Core features were with our abstractions initially built on Entity Framework's APIs.

I investigated query interception. Although this is a heavily used feature internally, I found literally two paragraphs of information about the feature in Entity Framework Core.

This is a summary of what I learned about the feature and is attempting to be the blog post that I wish I would have found during my investigation.

### What is query interception?

Query interception is the ability to insert logic before a query executes on the database or insert logic immediately after a query executes (and before control returns to the calling code).

There are a variety of real world use cases for this feature:
- Extend the timeout of a command that has certain charateristics
- Log diagnostic information when a query fails with an exception
- Log a warning when the number of rows read into memory is above a certain threshold

### How do I use query interception in EntityFramework Core?

EF Core exposes a base class `DbCommandInterceptor` with hooks into the query "life cycle".

Create a class that extends DbCommandInterceptor
```cs
public class TestQueryInterceptor : DbCommandInterceptor
{
  ...
}
```

then override the individual life cycle methods you care about:
```cs
// runs before a query is executed
public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
{
    ...
}

// runs after a query is excuted
public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
{
    ...
}
```

NOTE: Most life cycle methods have a synchronous and an asynchronous version. Annoyingly, asynchronous queries only trigger the asynchronous method (and vice-versa), so you must override both when writing an interceptor.

#### How to install a DbCommandInterceptor

You can add multiple interceptors when configuring your DbContext.

```cs
public class SampleDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlite(@"Data Source=Sample.db;")
            .AddInterceptors(new SampleInterceptor1(), new SampleInterceptor2());
    }

    ...
}
```

#### How to modify the command before execution
This is fairly straightforward because most of `DbCommand`'s properties are settable.

```cs
public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
{
   command.CommandText += " OPTION (OPTIMIZE FOR UNKNOWN)";
   
   command.CommandTimeout = 12345;
   
   return result;
}
```

#### How to suppress execution
By returning a new `InterceptionResult` created via `InterceptionResult<T>.SuppressWithResult()` from a pre-event life cycle method, the command will not be executed.

It is important to note that any other `DbCommandInterceptor`s installed will still execute (and can check whether another interceptor has suppressed execution via the `HasResult` property on `result`).

```cs
public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
{
    if (this.ShouldSuppressExecution(command))
    {
        return InterceptionResult.SuppressWithResult<object>(null);
    }
    
    return result;
}
```

It is worth mentioning that an exception thrown in a pre-event life cycle method will technically prevent execution. Do not take advantage of this fact. It is almost always bad design to use exceptions for control flow. Exceptions should be save for exceptional situations.

```cs
public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
{
   if (this.IsSomethingWrongWithThisCommand(command, out var reasonSomethingIsWrong))
   {
       // query will not be executed
       throw new InvalidOperationException(reasonSomethingIsWrong);
   }
   
   return result;
}
```

#### Change the result of execution
From a post-event life cycle method, you can opt to return a different result.
```cs
public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
{
    if (this.ShouldChangeResult(command, out var changedResult))
    {
        return changedResult;
    }
    
    return result;
}
```

#### How to log diagnostic data if there's an exception
Although you can't catch exceptions, you can respond to them before they are thrown.

```cs
 public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
  {
      // there is a lot of other metadata on `eventData` that you might find useful
      this.LogDiagnosticInformation(
          eventData.Duration,
          eventData.Exception,
          command.CommandText);
  }
```

### Appendix 1: What types of operations can you intercept?

There are 17 methods you can overwrite when implementing `DbCommandInterceptor`. 

Here is a cheatsheet:

| Method | Description
|---|---|
| CommandCreating | Before a command is created (NOTE: Everything is a command, so this will intercept all queries)
| CommandCreated | After a command creation but before execution
| CommandFailed[Async] | After a command has failed with an exception during execution 
| ReaderExecuting[Async] | Before a "query" command is executed
| ReaderExecuted[Async] | After a "query" command is executed
| NonQueryExecuting[Async] | Before a "non-query" command is executed (NOTE: An example of a non-query are usages of `ExecuteSqlRaw`
| NonQueryExecuted[Async] | After a "non-query" command is executed
| ScalarExecuting[Async] | Before a "scalar" command is executed (NOTE: "scalar" is kind of synonymous with stored procedure)
| ScalarExecuted[Async] | After a "scalar" command is executed
| DataReaderDisposing | After a command is executed