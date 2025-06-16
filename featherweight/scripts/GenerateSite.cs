#:package Markdig@0.41.2
#:package System.ServiceModel.Syndication@9.0.6

using System.Text.RegularExpressions;
using Markdig;
using Markdig.Helpers;
using System.ServiceModel.Syndication;
using System.Xml;

var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

var homeDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..");
var generatedDirectory = Path.Combine(homeDirectory, "generated");
var blogPostHtmlTemplate = File.ReadAllText(Path.Combine(homeDirectory, "scripts", "BlogPost.template.html"));
var blogPostHomeTemplate = File.ReadAllText(Path.Combine(homeDirectory, "scripts", "BlogHome.template.html"));

// Clean up from previous runs of this script
foreach (var previouslyGeneratedFile in Directory.EnumerateFiles(generatedDirectory))
{
    File.Delete(previouslyGeneratedFile);
}

var blogPosts = new List<BlogPostMetadata>();

var markdownDirectory = Path.Combine(homeDirectory, "../_posts");
foreach (var markdownLocation in Directory.EnumerateFiles(markdownDirectory, "*.md"))
{
    // read Markdown content
    var content = File.ReadAllText(markdownLocation);

    // extract section like
    // ---
    // #layout: post
    // title:  "Query interception in Entity Framework Core"
    // date:   2020-07-25 7:00:00 -0500
    // tag: documentation
    // ---
    var lines = content.Split(Environment.NewLine).ToList();
    var title = lines
        .First(l => l.Contains("title:"))
        .Substring("title:".Length)
        .Trim()
        .Trim('"');
    var dateString = lines
        .First(l => l.Contains("date:"))
        .Substring("date:".Length)
        .Trim();
    var date = DateTime.Parse(dateString);

    // remove header
    var headerEndIndex = lines.FindIndex(startIndex: 1, l => l.Trim() == "---");
    content = string.Join(Environment.NewLine, lines.Skip(headerEndIndex + 1));

    // generate HTML content
    var htmlContent = Markdown.ToHtml(content, pipeline);

    // populate the blog post template
    var templatedContent = blogPostHtmlTemplate
        .Replace("<!-- TEMPLATE: TITLE -->", title)
        .Replace("<!-- TEMPLATE: DATE -->", date.ToString("MM-dd-yy"))
        .Replace("<!-- TEMPLATE: CONTENT -->", htmlContent);

    // support highlighting
    templatedContent = templatedContent
        .Replace("<pre>", "<pre class=\"prettyprint\">");

    // support links like [Next post](/learn-msbuild-part-2) 
    var linkRegex = new Regex(@"\[(?<displayText>[^\]]*)\]\((?<relativeLink>[^\)]*)\)");
    templatedContent = linkRegex.Replace(templatedContent, match =>
    {    
        var displayText = match.Groups["displayText"];
        var relativeLink = match.Groups["relativeLink"];
        return $"<a href=\".{relativeLink}.html\">{displayText}</a>";
    });

    var markdownFileName = Path.GetFileNameWithoutExtension(markdownLocation);
    var fileNameWithoutDate = string.Join(string.Empty, markdownFileName.SkipWhile(c => !c.IsAlpha()));
    var htmlFileName = fileNameWithoutDate + ".html";
    var htmlLocation = Path.Combine(homeDirectory, "generated", htmlFileName);

    File.WriteAllText(path: htmlLocation, contents: templatedContent);
    blogPosts.Add(new BlogPostMetadata(Title: title, PublishDate: date, FileName: htmlFileName));
}

// Generate blog post home

blogPosts.Sort((x, y) => -DateTime.Compare(x.PublishDate, y.PublishDate));
var listItemsHtml = blogPosts
    .Select(b => $"<li><a href=\"./{b.FileName}\">{b.Title}</a><small> ({b.PublishDate:MM/dd/yy})</small></li>");

var templatedBlogPostHome = blogPostHomeTemplate
    .Replace("<!-- TEMPLATE: ITEMS -->", string.Join(Environment.NewLine, listItemsHtml));
File.WriteAllText(path: Path.Combine(homeDirectory, "generated", "blog.html"), templatedBlogPostHome);

// Generate RSS feed

var feed = new SyndicationFeed(
    title: "What's Keeping Lizzy Busy?",
    description: "RSS feed for Lizzy Gallagher's tech blog",
    new Uri("https://lizzy-gallagher.github.io/rss.xml"),
    id: "FeedID",
    DateTime.Now)
{
    Items = blogPosts
        .Select(b => new SyndicationItem(title: b.Title, content: "test", itemAlternateLink: new Uri("https://lizzy-gallagher.github.io/featherweight/generated/" + b.FileName)))
};

using XmlWriter writer = XmlWriter.Create("../generated/rss.xml");
var rssFormatter = new Rss20FeedFormatter(feed);
rssFormatter.WriteTo(writer);

record BlogPostMetadata(string Title, DateTime PublishDate, string FileName);