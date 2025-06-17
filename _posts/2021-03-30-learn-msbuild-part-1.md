---
title: "Learn MSBuild - Part 1 - Motivation"
date: 2021-03-31 19:46:00 -0500
excerpt: "You can get pretty far as a .NET developer without understanding MSBuild. Sure, very few months you may need to frantically copy/paste a line of MSBuild from StackOverflow into a .csproj, but you can mostly live in blissful ignorance."
tags: msbuild
---
<div class="notice--info" markdown="1">
[Next post](/learn-msbuild-part-2)
</div>

You can get pretty far as a .NET developer without understanding MSBuild. Sure, very few months you may need to frantically copy/paste a line of MSBuild from StackOverflow into a .csproj, but you can mostly live in blissful ignorance.

As some who has had the (mis?)fortune of spending a lot of time writing MSBuild,  I wanted to put together a resource to empower others to understand it.

### Why learn MSBuild?

#### 1. MSBuild is a useful tool for your toolbox

**Does your application have a multi-step README for how to build the project?** Using MSBuild, you can smush all those steps together -- restoring npm packages, running webpack, transpiling TypeScript -- into a single `dotnet build` command.

**Does your organization need validation done in every application?** Using MSBuild, you can easily implement validations that can break the build if not satisfied. An ASP.NET project missing a `.ConfigureAwait(false)`? Fail the build!

**Want to the "go-to expert" at your organization in something?** Pick MSBuild. You'll be able to help .NET developers in their darkest days.

#### 2. MSBuild inspires fear due to unfamiliarity

I have seen the eyes of many excellent engineers glaze over when opening an MSBuild file. Hours sunk into throwing copy/pasted statements against the wall to see if anything sticks.

### How will this blog series teach me MSBuild?

It's easiest to learn new idea by connecting them to old ones, so this series is going to teach MSBuild by explaining it as a programming language.

This series contains 3 posts:

1. [Learn MSBuild - Part 2 - Variables](/learn-msbuild-part-2)
2. [Learn MSBuild - Part 3 - Functions](/learn-msbuild-part-3)
3. [Learn MSBuild - Part 4 - Real-world MSBuild](/learn-msbuild-part-4)

Ready?

<div class="notice--info" markdown="1">
[Next post](/learn-msbuild-part-2)
</div>
