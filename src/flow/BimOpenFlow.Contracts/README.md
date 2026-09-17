# BimOpenFlow.Contracts

Compiles the generated app-level contract types (`contracts/generated/csharp/`)
into one shared assembly so every host-side consumer (Host.Api, Publishing,
Dashboards, Reports) references the same types instead of re-linking the
generated file. Contains no handwritten code — edit `contracts/contracts.json`
and run `node contracts/generate.mjs`.
