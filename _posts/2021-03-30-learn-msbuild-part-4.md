---
title: "Learn MSBuild - Part 4 - Real-world MSBuild"
date: 2021-03-31 19:48:00 -0500
excerpt: To reinforce what we've learned, we are going to dissect a real-world `.targets` file.
tags: msbuild
---

<div class="notice--info" markdown="1">
[Previous post](/learn-msbuild-part-3)
</div>

This is the file post in our series on learning MSBuild by understanding it as a programming language. To reinforce what we've learned, we are going to dissect a real-world `.targets` file: [TSLint.MSBuild.targets](https://github.com/JoshuaKGoldberg/TSLint.MSBuild/blob/master/src/build/TSLint.MSBuild.targets). 

<div class="notice--warning" markdown="1">

<h4 class="no_toc"><i class="fas fa-exclamation-triangle"></i> Disclaimer</h4>
I am not the author of `TSLint.MSBuild.targets`. I chose this NuGet package because
1. It is open-source
2. The context is easy to gain: the package lints TypeScript files on build.
3. I think the MSBuild is approachable while being non-trivial
</div>

# A Tour through `TSLint.MSBuild`

Instead of walking through the `TSLint.MSBuild.targets` file line-by-line, we'll instead tour various sections. The full text of file is available at the bottom of this post.

## Use the `Condition` attribute of a `Property` to only define the variable if it has not already been defined
Recall that the `Condition` attribute can be used to evaluate "has this `Property` already been defined?"
```xml
<PropertyGroup>
    <TSLintTimestampFile 
        Condition="'$(TSLintTimestampFile)' == ''">
        $(OutputPath)\$(MSBuildProjectName).TSLint.timestamp.txt
    </TSLintTimestampFile>
</PropertyGroup>
```

## Use string templating to compose a complex `Property`
The string templating support for `Properties` can be used like the `$PATH` variable to compose long sequences.
```xml
<!-- Build the TSLint arguments -->
<PropertyGroup>
    <TSLintArgs></TSLintArgs>
    <TSLintArgs Condition="'$(TSLintConfig)' != ''">$(TSLintArgs) --config "$(TSLintConfig)"</TSLintArgs>
    <TSLintArgs Condition="'@(TSLintExclude)' != ''">$(TSLintArgs) --exclude "$(TSLintExcludeJoined)"</TSLintArgs>
    ...
</PropertyGroup>
```

## Use the `Inputs` attribute so a `Target` can execute incrementally
Recall that MSBuild supports "incremental builds" and `Targets` support timestamp based caching.

```xml
<Target
    AfterTargets="CompileTypeScript"
    ...
    Inputs="@(TSLintInclude);@(TypeScriptCompile)"
    Name="TSLint"
    Outputs="$(TSLintTimestampFile)">

    ...

</Target>
```

## Use `Tasks` to report on status and validate information

The built-in `Message` `Task` is a go to for reporting status to the consumer.
```xml
<Message Text="Running TSLint..." Importance="high" />
```

The built-in `Error` `Task` is useful for validating inputs...
```xml
<Error Condition="'$(TSLintFileListDisabled)' == 'true' And '$(TSLintProject)' == ''" Text="You disabled file listing on the command line using TSLintFileDisabled, but did not specify a project file with TSLintProject." />
```
... and reporting if something doesn't work as expected.
```xml
<!-- Return an error if TSLint returned an exit code and we should break on errors -->
<Error Condition="'$(TSLintDisabled)' != 'true' and '$(TSLintErrorCode)' != '0' and '$(TSLintBreakBuildOnError)' == 'true'" Text="TSLint checks failed" />
```

## Use the `Exec` built-in `Task` to run an executable
The built-in `Exec` `Task` makes it simple to delegate more complex behavior to external executables.

```xml
<!-- Run TSLint using the Node executable -->
<Exec
    Command="&quot;$(TSLintNodeExe)&quot; &quot;$(TSLintCli)&quot; $(TSLintArgs)"
    Condition="'$(TSLintDisabled)' != 'true'"
    ConsoleToMsBuild="true"
    ...
    Timeout="$(TSLintTimeout)">
        ...
</Exec>
```

# Summary

MSBuild has the same primitives as general-purpose languages like C#.

In this post, we reviewed what what's possible using `Properties`, `Items`, `Tasks`, and `Targets` by looking at a real-life open-source example.

Hopefully this series has been a helpful explanation of MSBuild as a programming language. If anything was unclear or you'd like to hear more, please feel free to reach out to me on [Twitter](https://twitter.com/LizzyIsNotBusy)!

<div class="notice--info" markdown="1">
[Previous post](/learn-msbuild-part-3)
</div>

# Appendix: Full source of `TSLint.MSBuild.targets`
Source available at:
https://github.com/JoshuaKGoldberg/TSLint.MSBuild/blob/master/src/build/TSLint.MSBuild.targets
```xml
<?xml version="1.0" encoding="utf-8" ?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">

  <!-- Ensures that if this file changes it forces a TypeScript rebuild -->
  <PropertyGroup>
    <TypeScriptAllProjects>$(TypeScriptAllProjects);$(MSBuildThisFileFullPath)</TypeScriptAllProjects>
    <TSLintTimestampFile Condition="'$(TSLintTimestampFile)' == ''">$(OutputPath)\$(MSBuildProjectName).TSLint.timestamp.txt</TSLintTimestampFile>
    <TSLintTimestampFile Condition="'$(TSLintForceBuild)' == 'true'">$([System.DateTime]::UtcNow.Ticks)</TSLintTimestampFile>
  </PropertyGroup>

  <Target
    AfterTargets="CompileTypeScript"
    Condition="('@(TSLintInclude)' != '' or '@(TypeScriptCompile)' != '') and ('$(BuildingProject)' == 'true' or '$(TSLintRunWhenNotBuilding)' == 'true')"
    Inputs="@(TSLintInclude);@(TypeScriptCompile)"
    Name="TSLint"
    Outputs="$(TSLintTimestampFile)">

    <Message Text="Running TSLint..." Importance="high" />

    <ItemGroup Condition="'$(TSLintExcludeTypeScriptCompile)' != 'true'">
      <TSLintInclude Include="@(TypeScriptCompile)" />
    </ItemGroup>

    <PropertyGroup>
      <TSLintBreakBuildOnError Condition="'$(TSLintBreakBuildOnError)' == ''">false</TSLintBreakBuildOnError>
      <TSLintCreateTimestampFile Condition="'$(TSLintCreateTimestampFile)' == ''">true</TSLintCreateTimestampFile>
      <TSLintFormat Condition="'$(TSLintFormat)' == ''">msbuild</TSLintFormat>
      <TSLintNodeExe Condition="'$(TSLintNodeExe)' == ''">$([System.IO.Path]::GetFullPath("$(MSBuildThisFileDirectory)..\tools\node-12.4.0.exe"))</TSLintNodeExe>
      <TSLintTimeout Condition="'$(TSLintTimeout)' == ''">10000000</TSLintTimeout>
      <TSLintVersion Condition="'$(TSLintVersion)' == ''">*.*.*</TSLintVersion>
    </PropertyGroup>

    <!-- Grab the first matching TSLint CLI in a NuGet packages install -->
    <ItemGroup Condition="'$(TSLintCli)' == ''">
      <TSLintPotentialCli Include="$(SolutionDir)packages\tslint.$(TSLintVersion)\tools\node_modules\tslint\lib\tslintCli.js" />
      <TSLintPotentialCli Include="$(SolutionDir)packages\tslint\$(TSLintVersion)\tools\node_modules\tslint\lib\tslintCli.js" />
      <TSLintPotentialCli Include="$(MSBuildThisFileDirectory)..\..\tslint.$(TSLintVersion)\tools\node_modules\tslint\lib\tslintCli.js" />
      <TSLintPotentialCli Include="$(MSBuildThisFileDirectory)..\..\..\tslint\$(TSLintVersion)\tools\node_modules\tslint\lib\tslintCli.js" />
      <!-- support for tslint 5.10 and below -->
      <TSLintPotentialCli Include="$(SolutionDir)packages\tslint.$(TSLintVersion)\tools\node_modules\tslint\lib\tslint-cli.js" />
      <TSLintPotentialCli Include="$(SolutionDir)packages\tslint\$(TSLintVersion)\tools\node_modules\tslint\lib\tslint-cli.js" />
      <TSLintPotentialCli Include="$(MSBuildThisFileDirectory)..\..\tslint.$(TSLintVersion)\tools\node_modules\tslint\lib\tslint-cli.js" />
      <TSLintPotentialCli Include="$(MSBuildThisFileDirectory)..\..\..\tslint\$(TSLintVersion)\tools\node_modules\tslint\lib\tslint-cli.js" />
      <TSLintPotentialCli Include="$(ProjectDir)node_modules\tslint\bin\tslint" />
    </ItemGroup>
    <PropertyGroup Condition="'$(TSLintCli)' == ''">
      <TSLintCliProperty>@(TSLintPotentialCli);</TSLintCliProperty>
      <TSLintCli>$(TSLintCliProperty.Substring(0, $(TSLintCliProperty.IndexOf(';'))))</TSLintCli>
    </PropertyGroup>

    <!-- TSLintExclude might include special characters, so those should be escaped -->
    <PropertyGroup>
      <TSLintExclude Condition="'@(TSLintExclude)' != ''">@(TSLintExclude.Replace("*", "%2A"))</TSLintExclude>
      <TSLintExclude Condition="'@(TSLintExclude)' != ''">@(TSLintExclude.Replace(".", "%2E"))</TSLintExclude>
      <TSLintExclude Condition="'@(TSLintExclude)' != ''">@(TSLintExclude.Replace('"', "%22"))</TSLintExclude>
      
      <TSLintExcludeJoined Condition="'@(TSLintExclude)' != ''">@(TSLintExclude, '" --exclude "')</TSLintExcludeJoined>
    </PropertyGroup>

    <!-- Build the TSLint arguments -->
    <PropertyGroup>
      <TSLintArgs></TSLintArgs>
      <TSLintArgs Condition="'$(TSLintConfig)' != ''">$(TSLintArgs) --config "$(TSLintConfig)"</TSLintArgs>
      <TSLintArgs Condition="'@(TSLintExclude)' != ''">$(TSLintArgs) --exclude "$(TSLintExcludeJoined)"</TSLintArgs>
      <TSLintArgs>$(TSLintArgs) --format "$(TSLintFormat)"</TSLintArgs>
      <TSLintArgs Condition="'$(TSLintProject)' != ''">$(TSLintArgs) --project "$(TSLintProject)"</TSLintArgs>
      <TSLintArgs Condition="'$(TSLintTypeCheck)' != ''">$(TSLintArgs) --type-check "$(TSLintTypeCheck)"</TSLintArgs>
      <TSLintArgs Condition="'@(TSLintRulesDirectory)' != ''">$(TSLintArgs) --rules-dir "@(TSLintRulesDirectory, '" --rules-dir "')"</TSLintArgs>
      <TSLintArgs Condition="'$(TSLintExtraArgs)' != ''">$(TSLintArgs) $(TSLintExtraArgs)</TSLintArgs>
      <TSLintArgs Condition="'$(TSLintFileListDisabled)' != 'true' And '@(TSLintInclude)' != ''">$(TSLintArgs) "@(TSLintInclude, '" "')"</TSLintArgs>
    </PropertyGroup>

    <Error Condition="'$(TSLintFileListDisabled)' == 'true' And '$(TSLintProject)' == ''" Text="You disabled file listing on the command line using TSLintFileDisabled, but did not specify a project file with TSLintProject." />

    <MakeDir Directories="$(OutputPath)" />

    <!-- Run TSLint using the Node executable -->
    <Exec
      Command="&quot;$(TSLintNodeExe)&quot; &quot;$(TSLintCli)&quot; $(TSLintArgs)"
      Condition="'$(TSLintDisabled)' != 'true'"
      ConsoleToMsBuild="true"
      EchoOff="true"
      IgnoreExitCode="true"
      Timeout="$(TSLintTimeout)">
      <Output TaskParameter="ConsoleOutput" ItemName="TSLintOutput" />
      <Output TaskParameter="ExitCode" PropertyName="TSLintErrorCode" />
    </Exec>

    <Touch
      Condition="'$(TSLintCreateTimestampFile)' == 'true' and $(TSLintErrorCode) == 0"
      AlwaysCreate="true"
      Files="$(TSLintTimestampFile)" />

    <!-- Return an error if TSLint returned an exit code and we should break on errors -->
    <Error Condition="'$(TSLintDisabled)' != 'true' and '$(TSLintErrorCode)' != '0' and '$(TSLintBreakBuildOnError)' == 'true'" Text="TSLint checks failed" />
  </Target>
</Project>
```