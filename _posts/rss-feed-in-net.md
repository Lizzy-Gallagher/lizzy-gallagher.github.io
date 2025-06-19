---
title:  "What I wish I'd known when rolling my own RSS feed in .NET"
date:   2025-06-19 12:00:00 -0500
---

I recently rewrote this website to be more lightweight and throw off the shackles of a third-party static site generator.

When you have a hand-rolled website, it follows that you'll need a hand-rolled RSS feed. I love RSS and have strong RSS _opinions_ on what makes a feed great: full text and working images.

Since there are already a number of posts online about how to write an RSS feed in .NET, I'll keep this post to just the work I did to satisfy the "edge" cases I ran into due to these opinions.

<div class="notice--warning" markdown="1">

##### <i class="fas fa-exclamation-triangle"></i> Disclaimer

All the heavy lifting is done by the [`System.ServiceModel.Syndication` NuGet package](https://www.nuget.org/packages/System.ServiceModel.Syndication).

I'm not going to echo their [excellent documentation](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/how-to-create-a-basic-rss-feed) here. Start there and that might be all you need!
</div>

#### What do I need to know about RSS?

It's not magic. It's just an opinionated XML file^1^!

The XML needs to contain a `<channel>` element with one or more `<item>` elements. `<channel>` should contain metadata about your blog as a whole. `<item>` should contain metadata about a specific blog post.

The fields are fairly self-explanatory. For example, I imagine the following is fairly digestible:

```xml
<?xml version="1.0" encoding="utf-8"?>
<rss xmlns:a10="http://www.w3.org/2005/Atom" version="2.0">
    <channel>
        <title>What's Keeping Lizzy Busy?</title>
        <link>https://lizzy-gallagher.github.io/rss.xml</link>
        <description>RSS feed for Lizzy Gallagher's tech blog</description>
        <lastBuildDate>Wed, 18 Jun 2025 20:48:07 -0400</lastBuildDate>
        <a10:id>https://lizzy-gallagher.github.io</a10:id>
        <item>
            <guid isPermaLink="false">1408cefb-6afc-1b46-2c00-662a918b8b7b</guid>
            <link>https://lizzy-gallagher.github.io/_site/git-clone-a-single-file.html</link>
            <title>How to git clone a single file</title>
            <description>...</description>
            <a10:updated>2025-06-18T20:48:07-04:00</a10:updated>
        </item>
        ...
    </channel>
</rss>
```

However, straightforward as it might be, I have a few tips:

- Most RSS readers will render HTML if you give it to them. However, the environment is sandboxed (i.e. no JavaScript, CSS), but they will make HTTP requests for images as long as the urls are absolute.
- The `<description>` of an `<item>` can be _either_ a snippet of the overall content _or_ the complete content. If you choose to go with a snippet, the `<link>` element should contain the direct link to the full content. I mention this because I kept looking for a `<content>` element, but really `<description>` just plays double-duty.
- As far as I can tell, a channel's `<id>` is used to differentiate it from other RSS feeds that might have the same name. To save myself future headache from fending off the many upstart tech bloggers also named Lizzy, I used my website's url as the ID. I may change hosting at some point which _might_ cause a future headache, but _eh_, I'll cross that bridge when I get to it.

^1^ The [official specification](https://www.rssboard.org/rss-specification) is quite approachable, and I would recommend it to the curious. Although `System.ServiceModel.Syndication` is a workhorse, it was built to support both Atom and RSS. At times, this can make their API a bit confusing since it's naming doesn't alway match the official specification / outputted file. The official specification also gives lots of good advice as to _how_ to populate fields, so that's nice too.

#### How to include entire content of the RSS feed

It was non-negotiable that my RSS feed would include the full content of each blog post in its pure HTML form. I prefer to read my RSS feeds offline, so it's annoying when an otherwise interesting post drops that only includes a teaser sentence or too.

In order to do this, we need to embed the HTML body content into the RSS's XML. Importantly, we do not need to include the whole HTML _document_ but instead just the body. This is because, most RSS readers will render HTML elements but will not respect the `<script>` tags, etc. that are present in the `<head>` element.

You have two choices:

1. Encode all the HTML (e.g. `&lt;` instead of `<`).
2. Use a `CDATA` section.

Fun fact: I thought I had been fancy and using `CDATA` (option 2). However, in the course of writing this this blog post, I realized that I'd accidentally been doing the first...

`CDATA` is a tool in XML to delineate a specific section of the document as "character data", text that is meant to be taken _literally_. This means that it can use characters like `<` and `>` unescaped without messing up the outer XML document.

For those (like me!) who are unfamiliar with CDATA, it looks like the following:

```xml
<![CDATA[
Look at me!! I can contain things like <> and </element> and it won't mess up the XML of the outer document!
]]>
```

Here's how you _would_ create a `CDATA` section in .NET:

```c#
// <![CDATA[<div>I am HTML!</div>]]>
var cDataContent = new XmlDocument().CreateCDataSection(unencodedHtmlContent).OuterText;
```

However, here's what I was doing:

```c#
// <div>I am HTML!</div>
var cDataContent = new XmlDocument().CreateCDataSection(unencodedHtmlContent).InnerText;
```

That's what we in the business call a no-op... 🤦🏻‍♀️

What had been _actually_ encoding my HTML was the default serialization behavior of the `XmlWriter` which I had been using to save the generated RSS XML file.

```c#
using XmlWriter writer = XmlWriter.Create(rssLocation); // does the encoding for you!
var rssFormatter = new Rss20FeedFormatter(feed);
rssFormatter.WriteTo(writer);
```

#### How to test your RSS feed for correctness

I knew I didn't want to have to deploy my new RSS XML file in order to test it. As someone scared of pushing a bug to prod (even when prod is my just a dev blog), I wanted to iterate without deploying.

Thankfully, since RSS is a specification, you can validate correctness against that specification. Once you've generated a XML file, you can upload it to this [free validator from W3](https://validator.w3.org/feed/#validate_by_input).

I was never able to get to zero errors, mostly due to built-in namespacing decisions that Microsoft made that aren't able to be disabled.

And then of course, once you feel confident enough to deploy, you can subscribe via your favorite RSS reader:

![Showcase of RSS feed](/assets/images/rss-example.jpeg)

#### How to populate `<updated>` for an `<item>`

Each `<item>`'s `<updated>` element seem to be very important to the user experience when using an RSS reader, e.g. the ordering of items. I wanted to spend some time getting this right instead of using `DateTime.Now` and moving on with my life.

It was easy enough to write down the blog's publish date in the metadata of corresponding markdown file.

```text
---
title: How to fix "resolve operation has already ended" exceptions in lambda Autofac registrations
date: 2021-04-04 17:35:00 -0500
---
```

Great, this covered the initial creation of an `<item>`, but what about if the blog post had been updated? Could I get this to be reflected in `<updated>`?

To start, I'd need a way to identify matching entries in the existing RSS XML file. `<id>` to the rescue!

I used the title as `<id>`, the unique identifier of the blog post. As I write this blog post, I realize that I probably should have used file name or something less likely to change than a title. Who among us has not thought of a catchier title five minutes after publishing a blog post?

```c#
var hash = MD5.HashData(Encoding.UTF8.GetBytes(b.Title));

// NOTE: I was not sure if there were special character restrictions (e.g. !), so I just GUID-ed it
var id = new Guid(hash).ToString(); 
```

And then I used a content check to determine whether the blog post has been updated since last generating the feed:

```c#
var previousEntry = existingFeed?.Items.SingleOrDefault(item => item.Id == id);

// NOTE 1: yes, my encoding buffs, even though the RSS feed's HTML content is encoded, previousEntry.Summary.Text
// is decoded. Therefore this comparison is "<div>I am HTML!</div>" == "<div>I am HTML!</div>"

// NOTE 2: yes, my reference vs. value equality buffs, in .NET string's "==" has an override to preform a
// case sensitive, ordinal value comparison (instead of reference comparison)

var lastUpdatedTime = previousEntry == null || previousEntry.Summary.Text == b.HtmlContentForRssFeed
    ? b.PublishDate
    : DateTime.Now;
```

This check was not particularly robust. For example, if I were to change the classname of a CSS element, it would register as the content being changed. I can't currently think of a better way to determine whether the _meaningful_ content of the blog post changed, so this is how it's going to be.

In summary, I would generate the initial date by using the metadata from the Markdown file. Each time the RSS feed is regenerated, the code checks whether the content of a blog post had changed. If it did, use `DateTime.Now`. If it didn't, keep using that initial date.

#### Appendix: All the code that makes this work

If you find this interesting, you can see the rest of the code to "generate this website" in [`GenerateSite.cs`](https://github.com/Lizzy-Gallagher/lizzy-gallagher.github.io/blob/master/scripts/GenerateSite.cs).

```c#
var rssLocation = Path.Combine(outputDirectory, "rss.xml");

// Handle the case where the rss file does not exist
XmlReader? reader = null;
SyndicationFeed? existingFeed = null;
if (File.Exists(rssLocation))
{
    reader = XmlReader.Create(rssLocation);
    existingFeed = SyndicationFeed.Load(reader);
}

// Create the <channel> for the RSS feed
var feed = new SyndicationFeed(
    title: "What's Keeping Lizzy Busy?",
    description: "RSS feed for Lizzy Gallagher's tech blog",
    new Uri("https://lizzy-gallagher.github.io/rss.xml"),
    id: "https://lizzy-gallagher.github.io",
    DateTime.Now)
{
    // Create the <item>s for the channel
    Items = blogPostMetadatas
        .Select(b =>
        {
            // title determines uniqueness (so... new title == new post)
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(b.Title));
            var id = new Guid(hash).ToString();

            var previousEntry = existingFeed?.Items.SingleOrDefault(item => item.Id == id);
            var lastUpdatedTime = previousEntry == null || previousEntry.Summary.Text == b.HtmlContentForRssFeed
                ? b.PublishDate
                : DateTime.Now;

            return new SyndicationItem(
                id: id,
                title: b.Title,
                content: b.HtmlContentForRssFeed,
                lastUpdatedTime: lastUpdatedTime,
                itemAlternateLink: new Uri("https://lizzy-gallagher.github.io/_site/" + b.FileName));
        })
};

reader?.Close();

// Not shown: logic that deletes the existing rss.xml

using XmlWriter writer = XmlWriter.Create(rssLocation);
var rssFormatter = new Rss20FeedFormatter(feed);
rssFormatter.WriteTo(writer);
```
