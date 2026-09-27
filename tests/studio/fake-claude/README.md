# tests/studio/fake-claude

A stand-in for the Claude Code command line, so `ClaudeCliBackend` can be tested without an
account, a login, or a network call to Anthropic. `claude.cmd` is the same kind of `.cmd` shim
`npm install -g @anthropic-ai/claude-code` puts on `PATH`; it forwards every argument, unchanged,
to `fake-claude.mjs` with Node. `BimOpenFlow.Studio.Tests` and `BimOpenMcp.Ifc.Ask.Tests` copy this
folder into their test output and run the `.cmd` from there, so `ClaudeCliProcess`'s
`cmd.exe /c <path>` launch path is exercised the same way it would be against the real CLI.

The fake speaks the real CLI's `--output-format stream-json` lines, but instead of reasoning it
plays back a script: it reads `<cwd>/fake-claude-script.json`, `{"runs":[{"steps":[...]}, ...]}`,
and serves run *k* on its *k*-th invocation (counted from the lines already in the call log, so a
resumed conversation's second `SendAsync` gets `runs[1]`). It still calls the real MCP tool server
named in `--mcp-config` over HTTP for every `call` step, so a test's assertions about the store see
real effects, not a stub.

Before anything else it appends one line to `<cwd>/fake-claude-calls.jsonl`:
`{"args":[...],"stdin":"...","env":["<names starting ANTHROPIC or CLAUDE>"]}`, which is how a test
checks the exact command line, the user text on stdin, and that no `ANTHROPIC_*` or `CLAUDE*`
variable reached the child.

Each step in a run:

| Step | Prints |
|---|---|
| `{"text": s}` | an assistant text line |
| `{"call": tool, "args": {...}}` | a `tool_use` line, then a real `tools/call` POST, then a `tool_result` line |
| `{"result": s}` | a success `result` (`num_turns` = calls so far + 1, 100 input and 20 output tokens per turn) |
| `{"error": s}` | `{"type":"result","subtype":"success","is_error":true,"result":s,"terminal_reason":"api_error"}`, the shape Spike S1 found for "Not logged in" (`subtype` stays `"success"`) |
| `{"maxTurns": true}` | a `result` with `subtype: "error_max_turns"` |
| `{"exit": n, "stderr": s}` | writes `s` to stderr and exits with code `n`, no `result` line |
| `{"sleep": ms}` | waits `ms` milliseconds, ticking `<cwd>/fake-claude-heartbeat.txt` every 100ms so a test that cancels mid-sleep can tell the process tree was actually killed rather than having exited on its own |

What this cannot show: a real login, a real model's choice of tool calls, or Claude Code's actual
behaviour on a network failure or a truncated MCP result.
