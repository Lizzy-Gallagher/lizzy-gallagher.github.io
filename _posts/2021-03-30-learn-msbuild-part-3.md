---
title: "Learn MSBuild - Part 3 - Functions"
date: 2021-03-31 19:47:00 -0500
excerpt: MSBuild has a concept of `Tasks` that are executed within `Targets` (i.e. event handlers).
tags: msbuild
---

<div class="notice--info" markdown="1">
[Previous post](/learn-msbuild-part-2) | [Next post](/learn-msbuild-part-4)
</div>

MSBuild is a [domain-specific language](https://en.wikipedia.org/wiki/Domain-specific_language), tailored to customizing how a .NET project is built. It shares concepts with any general-purpose language, and in this post we'll explore how MSBuild handles functions.

MSBuild has a concept of [`Tasks`](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-tasks) (i.e. functions) that are executed within [`Targets`](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets) (i.e. event handlers).

### `Tasks` (i.e. functions)

A function is executed by locating an XML node inside a `Target` XML node (more on these later).

```xml
<Target>
    <Error Text="Something went wrong!" />
</Target>
```

Here would be the equivalent C#:

```c#
throw new Exception("Something went wrong!")
```

There are dozens of built-in tasks. Most tasks make common I/O operations easy:

```xml
<Delete Files="@(MyFiles)" />
```

```c#
foreach (var fileName in myFiles)
{
    File.Delete(fileName);
}
```

```xml
<WriteLinesToFile 
    File="$(CacheFile)"
    Lines="$([System.DateTime]::Now)" 
    Overwrite="true" />
```

```c#
File.WriteAllText(
    cacheFile,
    DateTime.Now)
```

There's even a task called [`Exec`](https://docs.microsoft.com/en-us/visualstudio/msbuild/exec-task) that allows you to run arbitrary console commands!

```xml
<Exec Command="dir" WorkingDirectory="$(MSBuildProjectDirectory)" />
```

```c#
Command.Run("dir", o => o
    .WorkingDirectory(projectDirectory));
```

**Note:** The above C# example uses [MedallionShell](https://github.com/madelson/MedallionShell)
{: .notice--info}

You can also define your own task by implementing the `ITask` interface. This is a must-know trick when writing complex MSBuild logic. Custom tasks can utilize NuGet packages and are far easier to unit test. The [official docs](https://docs.microsoft.com/en-us/visualstudio/msbuild/task-writing) are very good on this.

Here's an example of how a custom task for minifying code would be invoked:

```xml
<UsingTask TaskName="Minify" AssemblyFile=".\bin\Debug\My.Minification.Project.MSBuild.dll" />

<Target Name="MinifyJavaScriptFiles" BeforeTargets="AfterBuild">
    <Info Text="Minifying files...">
    <Minify SourceFiles="@(NonMinifiedJavaScriptFiles)" />
</Target>
```

### `Targets` (i.e. event handlers)

MSBuild has an additional concept `Targets` that allows you to "schedule" your tasks. It is easy to think of these like event handlers. Targets are run at a specific point in the lifecycle of a build.

```xml
<Target Name="MyTarget" BeforeTargets="BeforeBuild">
    <Error Text="Fail the build!" />
</Target>
```

```c#
c.BeforeBuild += MyTarget;
...
static void MyTarget(object sender, BeforeBuildEventArgs e)
{
    throw new Exception("Fail the build!")
}
```

The most common "events" that you may want your code to handle are:

- `BeforeBuild`. Before compilation (the creation of `.dlls`, etc.) starts. It is best to handle this event if your code is doing validation or preprocessing required by the build.
- `AfterBuild`. After compilation has completed. This event is best for code doing post-processing or validation on output file (e.g. minification). NOTE: Unintuitively, a target handling `AfterBuild` is still capable of "failing" the build.
- `Clean`. When a `Clean` is requested by the user. It is a best practice to "clean up" after yourself.

MSBuild follows is designed to support "incremental" builds, e.g. only recompiling, re-restoring NuGet packages, etc. if the changeset since the last build requires it.

To implement "incremental builds" `Targets` support file timestamp-based caching out of the box:

- If an `Inputs` attribute is specified, the `Target` will only execute if the "last modified" timestamp of any of files is later than the last execution.
- If the `Target` does execute, `Inputs` will be edited to only contain items that have been modified since the last execution.

```xml
<ItemGroup>
    <JavaScriptFiles Include="wwwroot/*.js" Exclude="wwwroot/*.min.js"/>
</ItemGroup>

<Target Name="MinifyJavaScriptFiles" BeforeTargets="AfterBuild" Inputs="$(JavaScriptFiles)">
    ... minify files ...
</Target>
```

```c#
public void MinifyJavaScriptFiles(javaScriptFiles)
{
    if (HaveAnyTimestampsBeenUpdatedSinceLastExecution(javaScriptFiles)) 
    { 
        return; 
    }

    ... minify files ...
}
```

### Summary

MSBuild has the same primitives as general-purpose languages like C#.

In this post, we covered its support for functions and event handlers. In the next post, we'll take a look at a real-world example to reinforce what we've learned.

<div class="notice--info" markdown="1">
[Previous post](/learn-msbuild-part-2) | [Next post](/learn-msbuild-part-4)
</div>

### Appendix: A quick note on importing `Targets`

There are three ways to define / import a `Target` into your .csproj:

1. **Define the `Target` in the .csproj itself.** This is easy and makes sense for short, adhoc `Targets` written to address quirks in your build.
2. **Define a `Target` in a `.targets` file in the `build/` folder of a `NuGet` package.** This is the most reusable and a very valuable skill to be able to deploy if the situation calls for it.
3. **`Import` the `Target` from a `.targets` file.** This makes organization simple since you can group related `Targets` and `Properties` in the same files together.

```xml
<Import Project="FileWithProperties.props" />
<Import Project="FileWithTargets.targets" />
```
