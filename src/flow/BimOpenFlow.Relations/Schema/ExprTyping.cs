using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataFlowEngine.Expressions.Parsing;
using Ara3D.DataFlowEngine.Expressions.Typing;

namespace BimOpenFlow.Relations;

/// <summary>Types a plan expression against a schema. Only scalar-typed columns are visible
/// to expressions; a Date or Binary column is simply not in the environment.</summary>
public static class ExprTyping
{
    public static IReadOnlyDictionary<string, ScalarType> Environment(this Schema schema)
    {
        var env = new Dictionary<string, ScalarType>();
        foreach (var c in schema.Columns)
            if (c.Type.ToScalarType() is { } t)
                env.TryAdd(c.Name, t);
        return env;
    }

    /// <summary>The static type, or the type errors. A statically-null expression has Ok with a null type.</summary>
    public static (ScalarType? Type, IReadOnlyList<ExprError> Errors) TypeOf(this Expr expr, Schema schema)
    {
        var errors = new List<ExprError>();
        var typed = TypeChecker.Check(expr, schema.Environment(), errors);
        return (errors.Count == 0 ? typed.Type : null, errors);
    }
}
