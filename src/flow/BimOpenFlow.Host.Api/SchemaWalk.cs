using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Contracts;
using PortType = Ara3D.DataFlowEngine.Abstractions.PortType;

namespace BimOpenFlow.Host.Api;

/// <summary>One run of the schema pass over a validated document whose graph inputs are resolved.
/// Holds what is known so far: the port results and the constant values flowing along wires.</summary>
internal sealed class SchemaWalk(GraphDocument doc, INodeRegistry registry, ISchemaCatalog catalog)
{
    private static readonly IReadOnlyDictionary<string, string> NoParams = new Dictionary<string, string>();

    private readonly Dictionary<string, PortRef> _edgeInto = doc.Edges.ToDictionary(e => e.To, e => e.FromRef);
    private readonly Dictionary<string, InferredPort> _ports = new();
    private readonly Dictionary<string, FlowValue> _values = new();
    private readonly Dictionary<string, string> _labels = new();

    public IReadOnlyList<InferredPort> Run()
        => doc.Sort().SelectMany(Visit).ToList();

    private static bool HasSchema(PortSpec port)
        => port.Type is PortType.Table or PortType.Relation;

    private static string Key(string node, string port)
        => $"{node}.{port}";

    private IEnumerable<InferredPort> Visit(GraphNode node)
    {
        var flow = registry.Find(node.Kind, node.Version)!;
        var spec = flow.Spec;
        var parameters = doc.Values.GetValueOrDefault(node.Id) ?? NoParams;
        _labels[node.Id] = $"'{node.Id}' ({node.Kind})";
        var known = KnownValues.Of(flow, parameters);
        for (var i = 0; i < spec.Outputs.Count; i++)
            if (known[i] is { } value)
                _values[Key(node.Id, spec.Outputs[i].Name)] = value;
        foreach (var output in spec.Outputs.Where(HasSchema))
        {
            var port = PortOf(node, flow, parameters, output.Name);
            _ports[Key(node.Id, output.Name)] = port;
            yield return port;
        }
    }

    private InferredPort PortOf(GraphNode node, IFlowNode flow, IReadOnlyDictionary<string, string> parameters, string output)
        => flow.Spec.Schemas.FirstOrDefault(s => s.Output == output) switch
        {
            null => InferredPort.Unknown(node.Id, output, $"{_labels[node.Id]} declares no schema"),
            StaticSchemaSpec s => InferredPort.Of(node.Id, output, SchemaResult.Of(s.Schema), s.Kinds),
            DynamicSchemaSpec => InferredPort.Dynamic(node.Id, output,
                $"{_labels[node.Id]} is dynamic: its columns depend on the data, so they are unknown until run"),
            _ when flow is not ISchemaNode => InferredPort.Unknown(node.Id, output,
                $"{_labels[node.Id]} declares an inferred schema but has no rule"),
            _ => Inferred(node, (ISchemaNode)flow, parameters, output),
        };

    private InferredPort Inferred(GraphNode node, ISchemaNode flow, IReadOnlyDictionary<string, string> parameters, string output)
    {
        var inputs = new List<SchemaInput>();
        foreach (var port in flow.Spec.Inputs)
        {
            if (!_edgeInto.TryGetValue(Key(node.Id, port.Name), out var source))
            {
                if (!port.Optional)
                    return InferredPort.Unknown(node.Id, output, $"input '{port.Name}' of {_labels[node.Id]} is not connected");
                inputs.Add(new(port.Type, false));
            }
            else if (HasSchema(port))
            {
                var upstream = _ports[source.ToString()];
                if (upstream.Status != SchemaStatus.Ok)
                    return InferredPort.Unknown(node.Id, output, BecauseOf(upstream));
                inputs.Add(new(port.Type, true, SchemaResult.Of(upstream.Schema!)));
            }
            else inputs.Add(new(port.Type, true, null, _values.GetValueOrDefault(source.ToString())));
        }
        var bound = new Dictionary<string, FlowValue>();
        foreach (var param in flow.Spec.Params.Where(p => p.Bindable is not null))
        {
            if (!_edgeInto.TryGetValue(Key(node.Id, param.Name), out var source))
                continue;
            if (!_values.TryGetValue(source.ToString(), out var value))
                return InferredPort.Unknown(node.Id, output,
                    $"parameter '{param.Name}' of {_labels[node.Id]} is wired from {source}, whose value is not known before a run");
            bound[param.Name] = value;
        }
        return InferredPort.Of(node.Id, output, Infer(flow, output, inputs, new ParamValues(parameters, bound)));
    }

    private SchemaResult Infer(ISchemaNode flow, string output, IReadOnlyList<SchemaInput> inputs, ParamValues values)
    {
        try
        {
            values.Validate(flow.Spec.Params);
            return flow.Infer(output, inputs, values, catalog);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return SchemaResult.Fail(e.Message);
        }
    }

    /// <summary>Why a port downstream of <paramref name="upstream"/> has no schema: the same reason an
    /// unknown port gives (it already names its origin), else the failure or the dynamic node.</summary>
    private string BecauseOf(InferredPort upstream)
        => upstream.Status switch
        {
            SchemaStatus.Error => $"unknown because {_labels[upstream.Node]} failed: {upstream.Errors![0]}",
            SchemaStatus.Dynamic => $"unknown until run, because {_labels[upstream.Node]} is dynamic",
            _ => upstream.Reason!,
        };
}
