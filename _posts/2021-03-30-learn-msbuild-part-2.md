---
title: "Learn MSBuild - Part 2 - Variables"
date: 2021-03-31 19:47:00 -0500
excerpt: MSBuild has two variable types `Properties` (i.e. strings) and `Items` (i.e. object arrays).
tags: msbuild
---

<div class="notice--info" markdown="1">
[Previous post](/learn-msbuild-part-1) | [Next post](/learn-msbuild-part-3)
</div>

MSBuild is a [domain-specific language](https://en.wikipedia.org/wiki/Domain-specific_language), tailored to customizing how a project is built and compiled. It shares concepts with any general-purpose language, and in this post we'll explore how MSBuild handles variables.

MSBuild has two variable types: `Properties` (i.e. strings) and `Items` (i.e. object arrays).

# `Properties` (i.e. strings)

A string variable is declared by locating an XML node inside `PropertyGroup` XML node.

```xml
<PropertyGroup>
    <CoolProperty>wow! this is so cool</CoolProperty>
</PropertyGroup>
```
In many ways, MSBuild behaves similarly to a general-purpose language. Here would be the equivalent C#:
```c#
var coolProperty = "wow! this is so cool!";
```

Properties can be reassigned at any time.

```xml
<PropertyGroup>
    <Direction>Go left</Direction>
    <Direction>Sorry! Right!</Direction>
</PropertyGroup>
```
```c#
var direction = "Go left";
direction = "Sorry! Right!";
```

<div class="notice--success" markdown="1">

<h4 class="no_toc"><i class="fas fa-lightbulb"></i> Tip: Whitespace matters!</h4>
Any and all whitespace inside the XML node is respected.

```xml
<IsFoo>
    true
</Foo>
```
```c#
var isFoo = @"
    true
";
```
</div>

You can access the value of a property (using `$`) to define other strings (i.e. string templating).
```xml
<Name>Lizzy</Name>
<Greeting>Hello $(Name)!</Greeting> <!-- Hello Lizzy! -->
```
```c#
var name = "Lizzy";
var greeting = $"Hello {name}!"; // Hello Lizzy!
```
Properties can also be defined conditionally using the `Condition` attribute.

```xml
<UsesNpm Condition="Exists('package.json')">true</UsesNpm>
```
```c#
var usesNpm = File.Exists(Path.Combine(Environment.CurrentDirectory, "package.json"))
    ? true
    : string.Empty;
```

**Note:** Learn more about the available `Condition` operators in the [official documentation](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-conditions)
{: .notice--info}

# `Items` (i.e. object arrays)

Am `Item` is defined by locating an XML node inside `ItemGroup` XML node.

```xml
<ItemGroup>
    <!-- single item -->
    <FavoriteThings Include="Raindrops on roses" />
    <!-- multiple items -->
    <FavoriteThings Include="Whiskers on kittens; bright copper kettles" />
<ItemGroup>
```
```c#
var favoriteThings = new[] { "Raindrops on roses" };
favoriteThings = favoriteThings
    .Append("Whiskers on kittens")
    .Append("bright copper kettles");
```

You can access the value of a `Item` (using `@`).

```xml
<ItemGroup>
    <CoolFiles Include="A.txt; B.txt">
<ItemGroup>

<Message Text="@(CoolFiles)" /> <!-- A.txt;B.txt -->
```
```c#
var coolFiles = new[] { "A.txt", "B.txt" };
Console.WriteLine(string.Join(";", coolFiles)); // "A.txt;B.txt"
```

MSBuild is designed to make working with files very easy. You can use wildcards to filter all the files included in the project.

```xml
<NonMinifiedFiles Include="*.js" Remove="*.min.js">
```
```c#
var nonMinifiedFiles = Directory.EnumerateFiles(Environment.CurrentDirectory, "*.js", SearchOption.AllDirectories)
    .Where(f => !f.EndsWith(".min.js"));
```

`Items` can be arrays of more complex objects. The child XML nodes of an `Item` are called "metadata" and can be accessed using the `%` operator.

```xml
<ItemGroup>
    <Stuff Include="Hide.cs" >
        <Display>false</Display>
    </Stuff>   
    <Stuff Include="Display.cs">
        <Display>true</Display>
    </Stuff>
</ItemGroup>
<Message Text="@(Stuff)" Condition=" '%(Display)' == 'true' "/> <!-- Display.cs -->
```
```c#
var stuff = new[] { new { Name = "Hide.cs", Display = false }, new { Name = "Display.cs", Display = true };
Console.WriteLine(string.Join(";", stuff.Where(s => s.Display))); // "Display.cs"
```

# Summary
MSBuild has the same primitives as general-purpose languages like C#.

In this post, we covered its support for variables. In the next post, we'll cover its support for functions!

<div class="notice--info" markdown="1">
[Previous post](/learn-msbuild-part-1) | [Next post](/learn-msbuild-part-3)
</div>

# Appendix: A quick note on variable names

A source of MSBuild consternation is the language's lack of variable scoping. There are no "private" variables, so you can overwrite variables defined by any MSBuild target, including the "standard library" that facilitates the compilation of your project's C#!

To avoid collisions, I recommend "namespacing" custom variables:
```xml
<!-- Very likely to collide! -->
<IsWebProject>...</IsWebProject>

<!-- Much better! -->
<MyOrganizationName_IsWebProject>...<MyOrganizationName_IsWebProject>

<!-- Even better! -->
<MyNuGetPackageName_IsWebProject>...<MyNuGetPackageName_IsWebProject>
```

This "feature" of language does have perks. It makes exposing a public API for disabling a build feature dead simple:

```xml
<!-- From a NuGet package the minifies your code, provide a default that can be overridden -->
<MinifyJavaScriptFiles Condition="'$(GenerateMinifiedSourceMaps)' ==''">true</MinifyJavaScriptFiles>

<!-- In a consuming project, disable minification in local dev for better performance -->
<MinifyJavaScriptFiles Condition="$(IsLocalDev)">false</MinifyJavaScriptFiles>
```

<div class="notice--info" markdown="1">
<h4 class="no_toc"><i class="fas fa-lightbulb"></i> Tip: Avoid naming collisions with the standard library!</h4>
Here is the documentation on built-in `Property` and `Item` names:

- [Common MSBuild project properties](https://docs.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-properties)

- [Reserved and well-known properties](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties)
</div>