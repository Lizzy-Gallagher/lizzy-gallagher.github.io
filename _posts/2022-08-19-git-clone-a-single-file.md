---
title: "How to git clone a single file"
date: 2022-08-19 6:00:00 -0500
tags: git documentation
---

<div class="notice--warning" markdown="1">

##### <i class="fas fa-exclamation-triangle"></i> Disclaimer

This method uses [`git archive`](https://git-scm.com/docs/git-archive) which is not supported by all source control services.

| Service | Supported
|---|---
| GitHub | ❌^†^
| GitLab | ✔️
| Atlassian Bitbucket | ✔️

^†^[Official acknowledgement from GitHub](https://twitter.com/GitHubHelp/status/322818593748303873).
</div>

```bash
# Create .tar archive containing only the single file
git archive --remote <repositoryUri> <branchName>:<directoryPathFromGitRoot> <fileName> -o <archiveName>
# Extract the single file to the current directory
tar -xf <archiveName>
# (optional) Delete the archive
rm <archiveName>
```

#### Example

```bash
git archive --remote git@gitlab.com:gitlab-org/gitlab.git HEAD:.github/ ISSUE_TEMPLATE.md -o myArchive.tar
tar -xf myArchive.tar
rm myArchive.tar
```

Clones [ISSUE_TEMPLATE.md](https://gitlab.com/gitlab-org/gitlab/-/blob/master/.github/ISSUE_TEMPLATE.md) from the main GitLab repo to the current directory. Try it out! As of writing, this example is functional!

#### Parameters

With the following parameters:

- `repositoryUri`. The uri of your repository.
  - NOTE: when copying this uri from the UI, select the `ssh` option. Choosing the `https` option will cause your command to fail with `fatal: operation not supported by protocol`
- `branchName`. You can also use `HEAD` to get the default branch.
- `directoryPathFromGitRoot`. If the file is not at the root of the repository, the path to the directory containing the single file.
- `fileName`. File name without directory information.
- `archiveName`. A fun name for your temporary archive.

<div class="notice--success" markdown="1">

##### Tip <i class="fas fa-lightbulb"></i>
When crafting the parameters for your use case, I recommend using [7Zip](https://www.7-zip.org/) to inspect the `.tar` archives locally.
</div>

### Why did I care?

At work, I worked on a utility to perform "checkups" on .NET applications. One genre of "checkup" it runs is verifying whether files matched the latest version of some master copy. For example, each application has a CI configuration file based on a centrally maintained version. A checkup by our utility provided the application owners wanted an quick way to verify whether their configuration file was up-to-date with the master copy.

Initially, we included the text of the master copy in the utility itself, which was about as sustainable as any "hardcoded" solution usually is. When researching alternatives to "grab latest version of a master copy," I came across a helpful [StackOverflow](https://stackoverflow.com/questions/1125476/retrieve-a-single-file-from-a-repository), but still struggled to connect all the pieces. This quick write-up is to hopefully save someone else an afternoon of throwing `tar` options at a wall.

P.S. It was only testing these commands in writing this blog post that I learned `git archive` is not supported in GitHub. I found this [blog post](https://www.gilesorr.com/blog/git-archive-github.html) to be the clearest explanation of workarounds.

### Bonus! Implementation in C#

Our "checkup" application is a .NET console app, so we needed to run the above commands from C#. This was an excellent opportunity to use my favorite command running library: [MedallionShell](https://github.com/madelson/MedallionShell)!

<div class="notice--warning" markdown="1">

##### Disclaimer <i class="fas fa-exclamation-triangle"></i>

This implementation uses two utilities internal to Mastercard: `TempDirectoryProvider` and `CoreUtilities.UniqueId()`. It should be clear how you might write your a replacement in your own code.
</div>

```c#
using System.IO;
using Medallion.Shell;

...

/// <summary>
/// Provides the content for a file in source control.
/// </summary>
public class SourceControlledFileProvider
{
    public static string GetFileContents(string fileName, string repositoryUri, string directoryPathFromGitRoot)
    {
        var shell = new Shell(o => o.WorkingDirectory(TempDirectoryProvider.TempDirectory).ThrowOnError());

        var filePath = Path.Combine(TempDirectoryProvider.TempDirectory, fileName);
        if (!File.Exists(filePath))
        {
            // Download a single file from a remote git repository as a .tar archive.
            //
            // Options:
            // -o       - Write the archive to <file> instead of stdout
            // --remote - Retrieve a tar archive from a remote repository
            var archiveName = CoreUtilities.UniqueId() + ".tar";
            var archiveCommand = shell.Run(
                "git", 
                "archive", 
                "--remote", repositoryUri, 
                "HEAD:" + directoryPathFromGitRoot, 
                "-o", archiveName);
            archiveCommand.Wait();

            // Extract the file from the .tar archive.
            //
            // NOTE: This uses the Windows default "tar"
            // which does not have the same options as UNIX "tar".
            var tarCommand = shell.Run("tar", "-xf", archiveName );
            tarCommand.Wait();
        }

        return File.ReadAllText(filePath);
    }
}
```