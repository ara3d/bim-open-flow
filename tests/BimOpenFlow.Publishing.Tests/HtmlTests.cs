namespace BimOpenFlow.Publishing.Tests;

public class HtmlTests
{
    [Test]
    public void Escape_HandlesAllSpecialCharacters()
        => Assert.That(Html.Escape("<a href=\"x\" title='y'>&z</a>"),
            Is.EqualTo("&lt;a href=&quot;x&quot; title=&#39;y&#39;&gt;&amp;z&lt;/a&gt;"));

    [Test]
    public void Escape_LeavesPlainTextAlone()
        => Assert.That(Html.Escape("plain text 123"), Is.EqualTo("plain text 123"));

    [Test]
    public void ScriptEscape_NeutralizesScriptCloseTag()
        => Assert.That(Html.ScriptEscape("var s = \"</script><script>alert(1)\";"),
            Is.EqualTo("var s = \"<\\/script><script>alert(1)\";"));

    [Test]
    public void Builder_EscapesTitleAndHeading()
    {
        var html = new HtmlDocumentBuilder("A & B <Report>").Build();
        Assert.That(html, Does.Contain("<title>A &amp; B &lt;Report&gt;</title>"));
        Assert.That(html, Does.Contain("<h1>A &amp; B &lt;Report&gt;</h1>"));
        Assert.That(html, Does.Not.Contain("<Report>"));
    }

    [Test]
    public void Builder_IsDeterministic()
    {
        static string Build()
            => new HtmlDocumentBuilder("Doc")
                .AddCss(".x { color: red; }")
                .AddSection("s1", "Section <1>", "<p>body</p>")
                .AddScript("console.log(\"hi\");")
                .Build();
        var first = Build();
        var second = Build();
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void Builder_UsesLfNewlinesOnly()
        => Assert.That(new HtmlDocumentBuilder("Doc").Build(), Does.Not.Contain("\r"));

    [Test]
    public void Builder_EmitsSectionsAndScriptsInOrder()
    {
        var html = new HtmlDocumentBuilder("Doc")
            .AddSection("first", "First", "<p>1</p>")
            .AddSection("second", "Second", "<p>2</p>")
            .AddScript("var a = 1;")
            .Build();
        Assert.That(html.IndexOf("id=\"first\"", StringComparison.Ordinal),
            Is.LessThan(html.IndexOf("id=\"second\"", StringComparison.Ordinal)));
        Assert.That(html.IndexOf("id=\"second\"", StringComparison.Ordinal),
            Is.LessThan(html.IndexOf("var a = 1;", StringComparison.Ordinal)));
        Assert.That(html, Does.Contain(HtmlTheme.Default));
    }
}
