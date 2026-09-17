using Ara3D.DataFlowEngine.Runs;

namespace BimOpenFlow.Reports.Tests;

public static class TestRuns
{
    public static readonly string GraphHash = new('a', 64);
    public static readonly string OutputHash = new('b', 64);
    public static readonly string InputHash = new('c', 64);

    /// <summary>A run whose "checks.out" table follows the verdict convention
    /// (Pass, Fail, NeedsReview) and whose "data.out" table does not.</summary>
    public static RunRecord WithVerdicts()
        => RunRecordJson.Parse($$"""
            {
              "runVersion": "0.1.0",
              "graphHash": "{{GraphHash}}",
              "engineVersion": "1.0.0",
              "timestampUtc": "2026-08-31T00:00:00.000Z",
              "inputs": [
                { "node": "src", "param": "path", "contentHash": "{{InputHash}}", "source": "model.ifc" }
              ],
              "nodeOutputs": { "checks.out": "{{OutputHash}}", "data.out": "{{OutputHash}}" },
              "recordedOutputs": {
                "checks.out": {
                  "kind": "Table",
                  "columns": [
                    { "name": "wall", "kind": "Text", "cells": ["w1", "w2", "w3", "w4"] },
                    { "name": "verdict", "kind": "Text", "cells": ["Pass", "Fail", "NeedsReview", "Pass"] },
                    { "name": "checkId", "kind": "Text", "cells": ["C-1", "C-1", "C-1", "C-1"] },
                    { "name": "checkTitle", "kind": "Text", "cells": ["Width", "Width", "Width", "Width"] },
                    { "name": "citation", "kind": "Text", "cells": ["NBC 9.5", "NBC 9.5", "NBC 9.5", "NBC 9.5"] }
                  ]
                },
                "data.out": {
                  "kind": "Table",
                  "columns": [
                    { "name": "name", "kind": "Text", "cells": ["a", "b", "c"] },
                    { "name": "area", "kind": "Number", "cells": [1.5, 2.5, 3.5] }
                  ]
                }
              },
              "effects": []
            }
            """);

    /// <summary>A run with only a plain (non-verdict) table and a scalar.</summary>
    public static RunRecord WithoutVerdicts()
        => RunRecordJson.Parse($$"""
            {
              "runVersion": "0.1.0",
              "graphHash": "{{GraphHash}}",
              "engineVersion": "1.0.0",
              "timestampUtc": "2026-08-31T00:00:00.000Z",
              "inputs": [],
              "nodeOutputs": { "data.out": "{{OutputHash}}" },
              "recordedOutputs": {
                "data.out": {
                  "kind": "Table",
                  "columns": [
                    { "name": "name", "kind": "Text", "cells": ["a", "b", "c"] },
                    { "name": "area", "kind": "Number", "cells": [1.5, 2.5, 3.5] }
                  ]
                },
                "total.out": { "kind": "Number", "value": 7.5 }
              },
              "effects": []
            }
            """);

    /// <summary>A table with the four verdict columns but an unknown verdict string.</summary>
    public static RunRecord WithBadVerdictText()
        => RunRecordJson.Parse($$"""
            {
              "runVersion": "0.1.0",
              "graphHash": "{{GraphHash}}",
              "engineVersion": "1.0.0",
              "timestampUtc": "2026-08-31T00:00:00.000Z",
              "inputs": [],
              "nodeOutputs": { "checks.out": "{{OutputHash}}" },
              "recordedOutputs": {
                "checks.out": {
                  "kind": "Table",
                  "columns": [
                    { "name": "verdict", "kind": "Text", "cells": ["Maybe"] },
                    { "name": "checkId", "kind": "Text", "cells": ["C-1"] },
                    { "name": "checkTitle", "kind": "Text", "cells": ["Width"] },
                    { "name": "citation", "kind": "Text", "cells": ["NBC 9.5"] }
                  ]
                }
              },
              "effects": []
            }
            """);
}
