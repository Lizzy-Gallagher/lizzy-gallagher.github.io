---
#layout: post
title:  "So you want to use an undocumented feature"
date:   2020-08-02 7:00:00 -0500
toc: true
tags: documentation
---
It happens. You're building something complex and unique, but not *so* complex and unique that zero other humans could have run into the same problem, right?

So you search for hours and find a feature (or package (or tool)) that is perfect for your use case... except that has no documentation. Maybe it's referenced in a bullet in the changelog or pops up in intellisense, but as far as you can tell there are no (answered) StackOverflow questions, no blog posts, and no mention in the readme or docs.

So what do you do?

## Ask "Why is this feature undocumented?"

This question has a slew of possible answers:

**The entire project is lacking documentation.** Not all FOSS projects prioritize documentation, especially those written for a niche set of consumers.

**The entire project is a rewrite of a better-documented project into a different language.** A maintainer may only document the places where the  the port/wrapper/rewrite departs from the public API of the original.

**The feature exists for feature-parity, and the maintainers don't want to encourage new usage.** From time to time, a project wants to reboot (e.g. .NET Framework -> .NET Core). To ensure long-time consumers can migrate, maintainers may include features that are no longer recommended. Omitting documentation is a fair way to discourage usage by new consumers.

**The feature is a pet project.** If a project is amenable to feature contributions, some contributor-suggested features may be merged with the contributor walking away satisfied but leaving a niche, undocumented feature in her wake.

**The feature is new and the community hasn't found it yet.** Even in popular libraries, it takes time for new features to be adopted, blogged about, and asked about on StackOverflow.

There isn't a hard rule here, but be open to the possibility that the answer to this question might dicate that you shouldn't use this feature.

## Seek out usages

Yes, we all prefer the minimal examples and best practices in official documentation, blog posts, or StackOverflow questions.

But when these are unavailable, the next best thing is real-world usages in applications similar to your own.

When doing this search, you need to resist the urge to use your favorite search engine (none of the major ones index source code) and instead use the advanced features of your favorite code-hosting platform (e.g. GitHub). 

Most platforms will let you narrow the search with filters:

**By file extensions** Beyond the obvious, these filters can be especially helpful if the project you are investigating is a rewrite/port/wrapper of a project in another language. If you can't find usages of the feature in the C# library, you can seek out usages in the Java or C++ versions.

**To specific repositories.** An A+ unit test suite can be as useful as A- documentation. If you filter to the project's own repository, you can find the feature's unit tests. Unit tests excel at revealing the behavior of the feature with edge cases.

**By popularity of repositories.** To be fair, I rarely filter results by "stars" but see the value in removing some noisy results (e.g. unchanged forks of the original project).

Again, be open to the possibility that if you are struggling to find usages of this feature *and especially if unit tests are low-quality or missing*, this implies that you should not use this feature.

## Read the source code

If the usages you found did not answer your questions, now is the time to sit down with some tea and read the source code.

Reading code is a valuable skill. Like all skills, you need to practice it in order to improve.<sup>1</sup>

When possible, I recommend cloning the code locally to click through references and usages. Spend time understanding doc comments and examining static members (which might not show up in intellisense).

## Take it for a test drive

If you take anything from this blog post, let it be "you should test out features in minimal environments".

**What is a minimal environment?** Something you can iterate in quickly without interference from existing code. 

This can be a scripting environment (e.g. LinqPad for C#, Jupyter notebooks for Python) or a single-file console application or a web route that loads a single JavaScript file.

Using a minimal sandbox saves you the effort spent integrating with your existing database, style rules, etc. It also saves you the time spent answering "is this not working because of the code using the new feature or because of the integration code I wrote?".

Whatever you choose for your sandbox, it should be something *you* are comfortable with and that you can attach a debugger to. The fine-grained ability of a debugger to examine internals during execution is indispensible when trying to understand a new feature.

## A real-world example: Query interception in Entity Framework Core

Recently at work, I investigated "query interception" in EntityFramework Core, a feature which at the time had a sum total of two paragraphs written about it on the internet. And I wrote a [blog post](https://lizzy-gallagher.github.io/query-interception-entity-framework/) about it!

Here's how I followed my own advice:

### Why was the feature undocumented?

Query interception is a niche feature that was blocking many long-time consumers from migrating from EF 6 to EF Core. It was added in the third major version release of EF Core (a telling sign that it was not considered a "core" feature).

Like my own team, consumers have built complicated cathedrals on the foundation on query interception and were not excited about migrating without the promise that they could lift and shift their setup.

### Seek out usages

I struck out when searching for real-world usages on GitHub, but I did find the excellent EntityFramework Core test suite. 

An open question I had was how EF Core query interception handled exceptions thrown at various stages of the query lifecycle. I pleasantly surpised to find an entire file devoted to testing the exception-handling behavior!

### Read the source code

The base class for query interception has 16 overrideable methods, some of which are async versions of each other. 

An open question I had (unanswered by my search for usages) was whether it was sufficient to override just one method from an async/sync pair. Reading the code, I learned that neither method of a async/async method pair called the other, so overriding both would be a must!

Also, I discovered a super valuable static method for supressing query execution. I had not seen usage of this method in the tests (it was tested in a separate file), so I would not have known about this method without reading the source!

### Take it for a test drive

Here are the steps to my minimal environment:
- Created a console app from a built-in Visual Studio template
- Created a SQLite database (the easiest flavor of database, literally one file on the filesystem)
- Added code to set up EF Core (copied from the EF Core SQLite documentation)
- Added code to empty and re-populate the database at the start of each test run

So in about 10-15 minutes, I had a sandbox in which to run many tiny experiments.

## Conclusion

Being able to learn about features without relying on documentation is a valuable skill to have in your software development toolbox. 

Although this post has primarily focuses on using open-source features (with publically available code), the advice also applies to working with legacy code at your organization.

Finally, consider documenting the outcome of your research!

You could:
- volunteer to contribute documentation (if the project is open-source)
- answer StackOverflow questions
- write a [blog post](https://lizzy-gallagher.github.io/query-interception-entity-framework/)!

It was this impulse to document the outcome of researching a feature that inspired me to start this blog!

---

<sup>1</sup> Once, while waxing about Computer Science curriculums, my team lead lamented that classes overemphasize writing code over reading code, even though a large part of a developer's time is spent inside complex projects where understanding context is essential. My team lead proposed that to remedy this imbalance, CS classes could require "book reports" where students would write up their understanding of some project. I think it's a neat idea.