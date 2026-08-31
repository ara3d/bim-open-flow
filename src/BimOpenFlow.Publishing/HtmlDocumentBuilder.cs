using System.Collections.Generic;
using System.Text;

namespace BimOpenFlow.Publishing;

/// <summary>
/// Assembles one self-contained HTML document: inline CSS theme, body
/// sections, and inline scripts at the end of the body. Deterministic:
/// the same calls produce the same bytes (LF newlines, no timestamps).
/// Raw-HTML arguments are trusted; text arguments are escaped here.
/// </summary>
public sealed class HtmlDocumentBuilder
{
    private readonly string _title;
    private readonly List<string> _css = new();
    private readonly List<string> _body = new();
    private readonly List<string> _scripts = new();

    public HtmlDocumentBuilder(string title, string theme = HtmlTheme.Default)
    {
        _title = title;
        _css.Add(theme);
    }

    public HtmlDocumentBuilder AddCss(string css)
    {
        _css.Add(css);
        return this;
    }

    /// <summary>Appends raw HTML to the body.</summary>
    public HtmlDocumentBuilder AddBody(string html)
    {
        _body.Add(html);
        return this;
    }

    /// <summary>Appends a section with an escaped heading and raw inner HTML.</summary>
    public HtmlDocumentBuilder AddSection(string id, string heading, string innerHtml)
        => AddBody(
            $"<section class=\"bof-section\" id=\"{Html.Escape(id)}\">\n" +
            $"<h2>{Html.Escape(heading)}</h2>\n{innerHtml}\n</section>");

    /// <summary>Appends an inline script emitted at the end of the body.</summary>
    public HtmlDocumentBuilder AddScript(string js)
    {
        _scripts.Add(Html.ScriptEscape(js));
        return this;
    }

    public string Build()
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append($"<title>{Html.Escape(_title)}</title>\n");
        sb.Append("<style>\n").AppendJoin('\n', _css).Append("\n</style>\n");
        sb.Append("</head>\n<body>\n");
        sb.Append($"<h1>{Html.Escape(_title)}</h1>\n");
        foreach (var block in _body)
            sb.Append(block).Append('\n');
        foreach (var script in _scripts)
            sb.Append("<script>\n").Append(script).Append("\n</script>\n");
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
