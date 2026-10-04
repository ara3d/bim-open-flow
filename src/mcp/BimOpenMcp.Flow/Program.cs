using BimOpenFlow.Host;
using BimOpenMcp.Flow;

// The generic MCP server: only the "tables" profile. The studio serves the bim profile
// through the same tools with `bimopenflow-studio mcp`.
return FlowMcpProgram.Run(args, HostComposition.Generic);
