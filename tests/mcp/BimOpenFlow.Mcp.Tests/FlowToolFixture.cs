using System.Text.Json;
using Ara3D.MCP;
using BimOpenFlow.Host;
using BimOpenFlow.Mcp;

namespace BimOpenFlow.Mcp.Tests;

/// <summary>Fresh services over temp directories for every test class.</summary>
public abstract class FlowToolFixture
{
    protected string Root = null!;
    protected FlowServices Services = null!;

    [SetUp]
    public void CreateServices()
    {
        Root = Path.Combine(Path.GetTempPath(), "bof-mcp-tests-" + Guid.NewGuid().ToString("N"));
        var modelsDir = Path.Combine(Root, "models");
        Directory.CreateDirectory(modelsDir);
        File.WriteAllBytes(Path.Combine(modelsDir, "sample.bos"), "hello"u8.ToArray());
        Services = FlowServices.Create(new HostConfig(
            [modelsDir], Path.Combine(Root, "cache"), Path.Combine(Root, "analyses"), Port: 0));
    }

    [TearDown]
    public void DeleteRoot()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Tool payloads are anonymous objects; assert on their MCP JSON form.</summary>
    protected static JsonElement Json(object payload)
    {
        using var doc = JsonDocument.Parse(McpJson.Serialize(payload));
        return doc.RootElement.Clone();
    }

    /// <summary>Authors camera -> sort via the edit tools, as an agent would.</summary>
    protected void AuthorCameraSort(string id)
    {
        FlowEditTools.AddNode(Services, id, "cam", "view3d.camera", version: null);
        FlowEditTools.SetParam(Services, id, "cam", "name", "front");
        FlowEditTools.AddNode(Services, id, "sort", "table.sort", version: null);
        FlowEditTools.SetParam(Services, id, "sort", "by", "name");
        FlowEditTools.Connect(Services, id, "cam.camera", "sort.table");
    }
}
