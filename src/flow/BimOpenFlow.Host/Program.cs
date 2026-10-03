using BimOpenFlow.Host;

// The generic host: only the "tables" profile. The bim profile is the studio's
// (bimopenflow-studio, src/studio/BimOpenFlow.Studio).
return await HostRunner.RunAsync(args, HostComposition.Generic);
