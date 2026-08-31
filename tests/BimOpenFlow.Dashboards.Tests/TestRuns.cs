using Ara3D.DataFlowEngine.Runs;

namespace BimOpenFlow.Dashboards.Tests;

public static class TestRuns
{
    public static readonly string GraphHash = new('a', 64);
    public static readonly string OutputHash = new('b', 64);

    /// <summary>A run with one recorded table "t.out" (cat: Text, val: Number) and one scalar "s.out".</summary>
    public static RunRecord Sample()
        => RunRecordJson.Parse($$"""
            {
              "runVersion": "0.1.0",
              "graphHash": "{{GraphHash}}",
              "engineVersion": "1.0.0",
              "timestampUtc": "2026-08-31T00:00:00.000Z",
              "inputs": [],
              "nodeOutputs": { "t.out": "{{OutputHash}}", "s.out": "{{OutputHash}}" },
              "recordedOutputs": {
                "t.out": {
                  "kind": "Table",
                  "columns": [
                    { "name": "cat", "kind": "Text", "cells": ["a", "b"] },
                    { "name": "val", "kind": "Number", "cells": [1.5, 2.5] }
                  ]
                },
                "s.out": { "kind": "Integer", "value": 42 }
              },
              "effects": []
            }
            """);
}
