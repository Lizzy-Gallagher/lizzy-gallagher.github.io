#:package Markdig@0.41.2
#:package System.ServiceModel.Syndication@9.0.6

using System.Text.RegularExpressions;
using Markdig;
using Markdig.Helpers;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Security.Cryptography;
using System.Text;

var markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

var homeDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..");
var outputDirectory = Path.Combine(homeDirectory, "_site");
var markdownDirectory = Path.Combine(homeDirectory, "_posts");

var blogPostHtmlTemplate = File.ReadAllText(Path.Combine(homeDirectory, "scripts", "BlogPost.template.html"));
var blogHomeTemplate = File.ReadAllText(Path.Combine(homeDirectory, "scripts", "BlogHome.template.html"));

// Save off I/O actions (instead of doing them immediately), so that we can use the old
// copies of files to compute things like "did a new post get published".
var actions = new List<Action>();

//
// STEP 0: Clean up from previous runs of this script
//
foreach (var previouslyGeneratedFile in Directory.EnumerateFiles(outputDirectory))
{
    actions.Add(() => File.Delete(previouslyGeneratedFile));
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

    // support links like [Next post](/learn-msbuild-part-2) 
    var linkRegex = new Regex(@"\[(?<displayText>[^\]]*)\]\((?<relativeLink>[^\)]*)\)");
    htmlContent = linkRegex.Replace(htmlContent, match =>
    {
        var displayText = match.Groups["displayText"];
        var relativeLink = match.Groups["relativeLink"];
        return $"<a href=\"{relativeLink}.html\">{displayText}</a>";
    });

    // change relative links to absolute links (comment out when testing the HTML for a new blog post locally)
    htmlContent = htmlContent
        .Replace("src=\"/", "src=\"https://lizzy-gallagher.github.io/")
        .Replace("src=\".", "src=\"https://lizzy-gallagher.github.io/")
        .Replace("href=\"/", "href=\"https://lizzy-gallagher.github.io/_site/")
        .Replace("href=\".", "href=\"https://lizzy-gallagher.github.io/_site");

    // populate the template
    var templatedContent = blogPostHtmlTemplate
        .Replace("<!-- TEMPLATE: TITLE -->", title)
        .Replace("<!-- TEMPLATE: DATE -->", date.ToString("MM-dd-yy"))
        .Replace("<!-- TEMPLATE: CONTENT -->", htmlContent);

    // support syntax highlighting
    templatedContent = templatedContent
        .Replace("<pre>", "<pre class=\"prettyprint\">");

    var htmlFileName = string.Join(string.Empty, Path.GetFileNameWithoutExtension(markdownLocation).SkipWhile(c => !c.IsAlpha())) + ".html";;
    var htmlLocation = Path.Combine(outputDirectory, htmlFileName);

    var isUpdated = !File.Exists(htmlLocation) || File.ReadAllText(htmlLocation) != templatedContent;

    actions.Add(() => File.WriteAllText(path: htmlLocation, contents: templatedContent));
    blogPostMetadatas.Add(new BlogPostMetadata(Title: title, PublishDate: date, FileName: htmlFileName, HtmlContentForRssFeed: htmlContent, isUpdated: isUpdated));
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
actions.Add(() => File.WriteAllText(path: Path.Combine(outputDirectory, "blog.html"), templatedBlogHome));

//
// STEP 3: Generate RSS feed
//

var rssLocation = Path.Combine(outputDirectory, "rss.xml");

XmlReader? reader = null;
SyndicationFeed? existingFeed = null;
if (File.Exists(rssLocation))
{
    reader = XmlReader.Create(rssLocation);
    existingFeed = SyndicationFeed.Load(reader);
}

var feed = new SyndicationFeed(
    title: "What's Keeping Lizzy Busy?",
    description: "RSS feed for Lizzy Gallagher's tech blog",
    new Uri("https://lizzy-gallagher.github.io/rss.xml"),
    id: "https://lizzy-gallagher.github.io",
    DateTime.Now)
{
    Items = blogPostMetadatas
        .Select(b =>
        {
            var cDataContent = new XmlDocument().CreateCDataSection(b.HtmlContentForRssFeed).InnerText;

            // title determines uniqueness (so... new title == new post)
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(b.Title));
            var id = new Guid(hash).ToString();

            var previousEntry = existingFeed?.Items.SingleOrDefault(item => item.Id == id);
            var dateToUse = previousEntry == null || previousEntry.Summary.Text == cDataContent
                ? DateTime.Now
                : previousEntry.LastUpdatedTime;

            return new SyndicationItem(
                id: id,
                title: b.Title,
                content: cDataContent,
                lastUpdatedTime: DateTime.Now,
                itemAlternateLink: new Uri("https://lizzy-gallagher.github.io/_site/" + b.FileName));
        })
};

reader?.Close();

actions.Add(() =>
{
    using XmlWriter writer = XmlWriter.Create(rssLocation);
    var rssFormatter = new Rss20FeedFormatter(feed);
    rssFormatter.WriteTo(writer);
});

//
// STEP 4: Actually do the things!
//
actions.ForEach(a => a());

record BlogPostMetadata(string Title, DateTime PublishDate, string FileName, string HtmlContentForRssFeed, bool isUpdated);