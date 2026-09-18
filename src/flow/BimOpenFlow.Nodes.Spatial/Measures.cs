using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>The distance measures the within and nearest nodes share: surface-to-surface
/// between boxes (zero when they intersect) or between box centers.</summary>
public static class Measures
{
    public const string Param = "measure";
    public const string Box = "box";
    public const string Center = "center";

    public static ParamSpec Spec { get; } = new(Param, ParamKind.Enum, Box, [Box, Center]);

    public static string Read(ParamValues parameters, string kind)
        => parameters.RequiredEnum(Param, kind, Box, Box, Center);

    public static double Distance(string measure, Box a, Box b)
        => measure == Center ? a.CenterDistance(b) : a.Distance(b);
}
