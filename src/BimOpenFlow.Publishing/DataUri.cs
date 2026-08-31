using System;
using System.IO;

namespace BimOpenFlow.Publishing;

/// <summary>Embeds binary assets (fonts, images) as data: URIs.</summary>
public static class DataUri
{
    public static string FromBytes(byte[] bytes, string mimeType)
        => $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";

    public static string FromFile(string path, string mimeType)
        => FromBytes(File.ReadAllBytes(path), mimeType);
}
