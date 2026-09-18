# BimOpenFlow.Ask

The agent loop that lets a language model answer a question by calling an MCP server's tools.
It knows nothing about which server it drives: give it any `McpServer` and it offers that
server's `tools/list` to the model as OpenAI function tools, routes each call through
`HandlePost` in process, and hands the result text back as the tool message. The loop ends
when the model replies without calling a tool.

Both the studio's Ask box (`src/studio/BimOpenFlow.Studio`) and the unattended IFC question
runner (`src/studio/BimOpenMcp.Ifc.Ask`) use it, which is why it is a library and not part of
either executable.

| File | Role |
|---|---|
| `AskAgent.cs` | The loop, the tool list, one-line result summaries, repeat detection, and the `AskEvent` / `AskOutcome` records the caller reports |
| `OpenAiChat.cs` | One chat-completions call over `HttpClient`, plus resolving the key (`OPENAI_API_KEY`, `OPENAI_API_KEY_FILE`), model (`OPENAI_MODEL`) and reasoning effort (`OPENAI_REASONING_EFFORT`) |

No key is read at construction time: the caller resolves one and passes it in, so a test can
run the whole loop against a scripted `HttpMessageHandler` with no network and no key.
