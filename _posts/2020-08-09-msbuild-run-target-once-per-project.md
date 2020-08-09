---
#layout: post
title:  "Run an MSBuild target once per project instead of once per target framework"
date:   2020-08-09 5:00:00 -0500
tags: msbuild
---

Most online resources will recommend using `BeforeTargets` or `AfterTargets` to hook your target into the MSBuild lifecycle `Build` method.

```xml
<Target Name="MyPackage_BeforeBuild"
        BeforeTargets="Build">
    <!-- Do things -->
</Target>

<Target Name="MyPackage_AfterBuild"
        AfterTargets="Build">
    <!-- Do things -->
</Target>
```

But this breaks down if you add another target framework.

```xml
<PropertyGroup>
  <TargetFrameworks>net472;netcoreapp3.1</TargetFrameworks>
</PropertyGroup>

<Target Name="MyPackage_BeforeBuild"
        BeforeTargets="Build">
    <Message Text="Before build: '$(TargetFramework)'" />
</Target>
```
```
Before build: 'net472'
Before build: 'netcoreapp3.0'
Before build: ''
```

Oof. Your target now runs N+1 times (if the project has N target frameworks).

At best, this slows down your build. At worst, it'll break it due to a race condition (e.g. if each iteration attempts to write to the same file).

## Run a target once per project (if multi-targeted)

Most of the time you can replace `Build` with `DispatchToInnerBuilds`.

```xml
<PropertyGroup>
  <TargetFrameworks>net472;netcoreapp3.1</TargetFrameworks>
</PropertyGroup>

<Target Name="MyPackage_BeforeBuild"
        BeforeTargets="DispatchToInnerBuilds">
    <Message Text="Before build '$(TargetFramework)'" />
</Target>
```
```
Before build: ''
```

Success!

Well, except that `DispatchToInnerBuilds` only exists for multi-targeted projects, so it will not run in a single-targeted project.
```xml
<PropertyGroup>
  <TargetFramework>netcoreapp3.1</TargetFramework>
</PropertyGroup>

<Target Name="MyPackage_BeforeBuild"
        BeforeTargets="DispatchToInnerBuilds">
  <Message Text="Before build: '$(TargetFramework)'" />
</Target>
```
```
```

## Run a target once per project
This is the layout of a NuGet package that distributes a target that runs only once per project regardless of whether the project is single- or multi-targeted.

`MyPackage.nuspec`
```xml
...
<files>
  <!-- files in the build/ directory run per target framework -->
  <file src="MyPackage.targets" target="build" />
  
  <!-- files in the buildMultiTargeting/ directory run once 
       per project (regardless of # of target frameworks), 
       but *only* if the project is multi-targeted -->
  <file src="MyPackage.targets" target="buildMultiTargeting" />
  <file src="MyPackage.props" target="buildMultiTargeting" />
</files>
```

Creates this package layout:
```
buildMultitargeting/
  MyPackage.props
  MyPackage.targets
build/
  MyPackage.targets (same content as other MyPackage.targets)
```

`buildMultiTargeting/MyPackage.props`
```xml
<Project>
  <PropertyGroup>
    <!-- this file only executes in the "outer" build of a
         multi-targeted project, so we set this variable to
         keep track of that information --> 
    <IsOuterBuild>true</IsOuterBuild>
  </PropertyGroup>
</Project>
```

`build/MyPackage.targets`
```xml
<PropertyGroup>
  <IsOuterBuild 
    Condition="'$(IsOuterBuild)' == ''">false</IsOuterBuild>
   
  <!-- Uue DispatchToInnerBuilds if a multi-targetedBuild -->
  <MyBeforeTargets>BuildDependsOn</MyBeforeTargets>
  <MyBeforeTargets 
    Condition="$(IsOuterBuild)">DispatchToInnerBuilds</MyBeforeTargets>
   
   <MyAfterTargets>Build</MyAfterTargets>
   <MyAfterTargets 
     Condition="$(IsOuterBuild)">DispatchToInnerBuilds</MyAfterTargets>

  <!-- to prevent targets from being run extra times,
       enforce that only the outer build of a multi-targeted
       project or a single-targeted build can run -->
  <ShouldRunTarget>false</ShouldRunTarget>
  <ShouldRunTarget 
    Condition="'$(TargetFrameworks)' == '' 
      OR $(IsOuterBuild)'">true</ShouldRunTarget>
</PropertyGroup>

<Target Name="MyBeforeBuild"
        Condition="$(ShouldRunTarget)"
        BeforeTargets="$(MyAfterTargets)">
    <!-- Do thing -->
</Target>

<Target Name="MyAfterBuild"
        Condition="$(ShouldRunTarget)"
        AfterTargets="$(MyAfterTargets)">
    <!-- Do thing -->
</Target>
```

NOTE: This above snippets were edited for horizontal brevity. In production code, you should always prefix your MSBuild variables with the name of your NuGet package (e.g. ShouldRunTarget -> MyProject_ShouldRunTarget). This is because MSBuild allows overwriting of variable names, so you want to be careful not to pollute the global pool of names.