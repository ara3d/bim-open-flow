using System.Text;

namespace BimOpenFlow.Publishing;

/// <summary>HTML text escaping, done once here for the whole emission layer.</summary>
public static class Html
{
    /// <summary>Escapes text for element content and double-quoted attribute values.</summary>
    public static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&#39;"); break;
                default: sb.Append(c); break;
            }
        return sb.ToString();
    }

    /// <summary>
    /// Makes JS/JSON safe to inline in a script element: "&lt;/script" can only
    /// occur inside a string or regex literal, where "&lt;\/script" is equivalent.
    /// </summary>
    public static string ScriptEscape(string js)
        => js.Replace("</script", "<\\/script");
}
