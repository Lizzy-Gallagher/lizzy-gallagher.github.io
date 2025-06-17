#:package Markdig@0.41.2
#:package System.ServiceModel.Syndication@9.0.6

using System.Text.RegularExpressions;
using Markdig;
using Markdig.Helpers;
using System.ServiceModel.Syndication;
using System.Xml;

var markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

var homeDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..");
var outputDirectory = Path.Combine(homeDirectory, "_site");
var markdownDirectory = Path.Combine(homeDirectory, "_posts");

var blogPostHtmlTemplate = File.ReadAllText(Path.Combine(homeDirectory, "scripts", "BlogPost.template.html"));
var blogHomeTemplate = File.ReadAllText(Path.Combine(homeDirectory, "scripts", "BlogHome.template.html"));

//
// STEP 0: Clean up from previous runs of this script
//
foreach (var previouslyGeneratedFile in Directory.EnumerateFiles(outputDirectory))
{
    File.Delete(previouslyGeneratedFile);
}

//
// STEP 1: Generate blog posts HTML files from Markdown files
//
var blogPostMetadatas = new List<BlogPostMetadata>();
foreach (var markdownLocation in Directory.EnumerateFiles(markdownDirectory, "*.md"))
{
    // read Markdown content
    var content = File.ReadAllText(markdownLocation);

    // extract values from metadata header like:
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

    // strip the metadata header
    var headerEndIndex = lines.FindIndex(startIndex: 1, l => l.Trim() == "---");
    content = string.Join(Environment.NewLine, lines.Skip(headerEndIndex + 1));

    // generate HTML content
    var htmlContent = Markdown.ToHtml(content, markdownPipeline);

    // populate the template
    var templatedContent = blogPostHtmlTemplate
        .Replace("<!-- TEMPLATE: TITLE -->", title)
        .Replace("<!-- TEMPLATE: DATE -->", date.ToString("MM-dd-yy"))
        .Replace("<!-- TEMPLATE: CONTENT -->", htmlContent);

    // support syntax highlighting
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

    var htmlFileName = string.Join(string.Empty, Path.GetFileNameWithoutExtension(markdownLocation).SkipWhile(c => !c.IsAlpha())) + ".html";;
    var htmlLocation = Path.Combine(outputDirectory, htmlFileName);
    File.WriteAllText(path: htmlLocation, contents: templatedContent);

    blogPostMetadatas.Add(new BlogPostMetadata(Title: title, PublishDate: date, FileName: htmlFileName));
}

//
// STEP 2: Generate blog homepage
//

// sort posts so most recent post is first
blogPostMetadatas.Sort((x, y) => -DateTime.Compare(x.PublishDate, y.PublishDate));
var listItemsHtml = blogPostMetadatas
    .Select(b => $"<li><a href=\"./{b.FileName}\">{b.Title}</a><small> ({b.PublishDate:MM/dd/yy})</small></li>");

var templatedBlogHome = blogHomeTemplate
    .Replace("<!-- TEMPLATE: ITEMS -->", string.Join(Environment.NewLine, listItemsHtml));
File.WriteAllText(path: Path.Combine(outputDirectory, "blog.html"), templatedBlogHome);

//
// STEP 3: Generate RSS feed
//

var feed = new SyndicationFeed(
    title: "What's Keeping Lizzy Busy?",
    description: "RSS feed for Lizzy Gallagher's tech blog",
    new Uri("https://lizzy-gallagher.github.io/rss.xml"),
    id: "FeedID",
    DateTime.Now)
{
    Items = blogPostMetadatas
        .Select(b => new SyndicationItem(title: b.Title, content: "test", itemAlternateLink: new Uri("https://lizzy-gallagher.github.io/_site/" + b.FileName)))
};

using XmlWriter writer = XmlWriter.Create(Path.Combine(outputDirectory, "rss.xml"));
var rssFormatter = new Rss20FeedFormatter(feed);
rssFormatter.WriteTo(writer);

record BlogPostMetadata(string Title, DateTime PublishDate, string FileName);