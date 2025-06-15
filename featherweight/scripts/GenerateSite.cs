#:package Humanizer@2.14.1
#:package Markdig@0.41.2

// using Humanizer;

// var dotNet9Released = DateTimeOffset.Parse("2024-12-03");
// var since = DateTimeOffset.Now - dotNet9Released;

// Console.WriteLine($"It has been {since.Humanize()} since .NET 9 was released.");

using System.Reflection;
using System.Runtime.CompilerServices;
using Humanizer;
using Markdig;

var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

var homeDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..");

var htmlTemplate = File.ReadAllText(Path.Combine(homeDirectory, "scripts", "BlogPost.template.html"));

var markdownDirectory = Path.Combine(Directory.GetCurrentDirectory(), "../../_posts");
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
    var lines = content.Split(Environment.NewLine);
    var title = lines
        .First(l => l.Contains("title:"))
        .Substring("title:".Length)
        .Trim()
        .Trim('"');
    var dateString = lines
        .First(l => l.Contains("date:"))
        .Substring("date:".Length)
        .Trim();
    var date = DateTime.Parse(dateString)
        .ToString("MM-dd-yy");

    // generate HTML content
    var htmlContent = Markdown.ToHtml(content, pipeline);

    // populate the blog post template
    var templatedContent = htmlTemplate
        .Replace("<!-- TEMPLATE: TITLE -->", title)
        .Replace("<!-- TEMPLATE: DATE -->", date)
        .Replace("<!-- TEMPLATE: CONTENT -->", htmlContent);

    var markdownFileName = Path.GetFileNameWithoutExtension(markdownLocation);
    var htmlLocation = Path.Combine(homeDirectory, "generated", markdownFileName + ".html");

    File.WriteAllText(path: htmlLocation, contents: templatedContent);
}
//File.ReadAllText()