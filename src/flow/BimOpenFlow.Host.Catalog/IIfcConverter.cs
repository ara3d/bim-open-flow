using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;

namespace BimOpenFlow.Host.Catalog;

/// <summary>Seam for the IFC to BOS conversion so tests can substitute a stub.</summary>
public interface IIfcConverter
{
    void Convert(string ifcPath, string bosPath);
}

/// <summary>Real conversion via IfcToBosConverter. Disposes the IfcFile the
/// converter opens, so a long-lived host does not leak a pinned file buffer
/// and a native web-ifc model per conversion.</summary>
public sealed class IfcToBosFileConverter : IIfcConverter
{
    public void Convert(string ifcPath, string bosPath)
    {
        var converter = new IfcToBosConverter(new FilePath(ifcPath));
        try
        {
            converter.SaveToBos(new FilePath(bosPath));
        }
        finally
        {
            converter.IfcFile?.Dispose();
        }
    }
}
