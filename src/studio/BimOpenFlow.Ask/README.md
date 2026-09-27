# BimOpenFlow.Ask

The agent loop that lets a language model answer a question by calling an MCP server's tools.
It knows nothing about which server it drives: give it any `McpServer` and it offers that
server's `tools/list` to the model as function tools, routes each call through
`HandlePost` in process, and hands the result text back as the tool message. The loop ends
when the model replies without calling a tool.

Both the studio's Ask box (`src/studio/BimOpenFlow.Studio`) and the unattended IFC question
runner (`src/studio/BimOpenMcp.Ifc.Ask`) use it, which is why it is a library and not part of
either executable. Neither caller branches on provider: both hold an `IAskBackend` and get one
from `ChatSelection.CreateBackend`.

| File | Role |
|---|---|
| `IAskBackend.cs` | `IAskBackend` and `IAskConversation`, the interface both backends implement, and `AskSetup` (the tool server, its name to the model, hidden tools, and the turn limit) |
| `ChatBackend.cs` | Runs the tool loop in process over an `IChatModel`: today's `AskAgent`, reached through `IAskBackend` |
| `LoopbackPorts.cs` | A free TCP port on `127.0.0.1`, for the MCP listeners `ClaudeCliBackend` and the IFC ask start |
| `AskAgent.cs` | The loop, the tool list, one-line result summaries, repeat detection, and the `AskEvent` / `AskOutcome` records the caller reports |
| `IChatModel.cs` | One model turn in the loop's conversation shape (the chat-completions shape), which every API provider implements |
| `AnthropicChat.cs` | One Messages API call to Claude over `HttpClient`, translated from and back to the loop shape; caches the system prompt, replays thinking blocks, turns a refusal into an answer. Key from `ANTHROPIC_API_KEY` / `ANTHROPIC_API_KEY_FILE`, model from `ANTHROPIC_MODEL` (default `claude-opus-5`), effort from `ANTHROPIC_EFFORT`, endpoint from `ANTHROPIC_BASE_URL` |
| `OpenAiChat.cs` | One chat-completions call over `HttpClient`. Key from `OPENAI_API_KEY` / `OPENAI_API_KEY_FILE`, model from `OPENAI_MODEL`, reasoning effort from `OPENAI_REASONING_EFFORT` |
| `ClaudeCli/ClaudeCliLocator.cs` | Finds the `claude` executable: `ASK_CLAUDE_CLI`, else the first `claude.exe` / `claude.cmd` on `PATH`, else the desktop app's own copy |
| `ClaudeCli/ClaudeCliSettings.cs` | The executable, model, effort, work directory, and timeout for one `claude -p` run; model and effort default from `ASK_CLAUDE_MODEL` / `ASK_CLAUDE_EFFORT` |
| `ClaudeCli/ClaudeCliArguments.cs` | The pinned `claude -p` argument list, the MCP config JSON, and the trimmed child environment (no `ANTHROPIC_*`, no `CLAUDECODE` / `CLAUDE_*` except the ones that carry the login) |
| `ClaudeCli/ClaudeStream.cs` | Reads Claude Code's `stream-json` output into `AskEvent`s and a `ClaudeResult` |
| `ClaudeCli/ClaudeCliProcess.cs` | Runs one `claude` process: arguments, trimmed environment, stdin, stdout lines, timeout and cancellation |
| `ClaudeCli/ClaudeCliBackend.cs`, `ClaudeCli/ClaudeCliConversation.cs`, `ClaudeCli/ClaudeCliException.cs` | `IAskBackend` that hands the tool loop to the Claude Code command line over the tool server's HTTP endpoint |
| `ChatSelection.cs` | Which backend to use: `ASK_PROVIDER` names one (`claude-cli`, `anthropic`, `openai`); else `claude-cli` when its executable is found; else the first API provider with a key, Anthropic before OpenAI; else `NoProvider`. Carries the problem sentence when nothing is usable, and creates the backend or the API client for the resolved selection |
| `ApiKeys.cs` | A key from a variable or from the first line of the file another variable names |
| `EmbeddedText.cs` | Reads the guide files the prompts embed from `.claude/skills` |

No key is read at construction time: the caller resolves one and passes it in, so a test can
run the whole loop against a scripted `HttpMessageHandler` with no network and no key. Likewise,
`claude-cli` is chosen without touching any `ANTHROPIC_*` variable, so a leftover key in the
environment never sends a request to an API account once an executable is found.
