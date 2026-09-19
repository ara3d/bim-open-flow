# BimOpenFlow.Ask

The agent loop that lets a language model answer a question by calling an MCP server's tools.
It knows nothing about which server it drives: give it any `McpServer` and it offers that
server's `tools/list` to the model as function tools, routes each call through
`HandlePost` in process, and hands the result text back as the tool message. The loop ends
when the model replies without calling a tool.

Both the studio's Ask box (`src/studio/BimOpenFlow.Studio`) and the unattended IFC question
runner (`src/studio/BimOpenMcp.Ifc.Ask`) use it, which is why it is a library and not part of
either executable.

| File | Role |
|---|---|
| `AskAgent.cs` | The loop, the tool list, one-line result summaries, repeat detection, and the `AskEvent` / `AskOutcome` records the caller reports |
| `IChatModel.cs` | One model turn in the loop's conversation shape (the chat-completions shape), which every provider implements |
| `AnthropicChat.cs` | One Messages API call to Claude over `HttpClient`, translated from and back to the loop shape; caches the system prompt, replays thinking blocks, turns a refusal into an answer. Key from `ANTHROPIC_API_KEY` / `ANTHROPIC_API_KEY_FILE`, model from `ANTHROPIC_MODEL` (default `claude-opus-5`), effort from `ANTHROPIC_EFFORT`, endpoint from `ANTHROPIC_BASE_URL` |
| `OpenAiChat.cs` | One chat-completions call over `HttpClient`. Key from `OPENAI_API_KEY` / `OPENAI_API_KEY_FILE`, model from `OPENAI_MODEL`, reasoning effort from `OPENAI_REASONING_EFFORT` |
| `ChatSelection.cs` | Which provider to use: `ASK_PROVIDER`, else the first with a key, Anthropic before OpenAI; carries the problem sentence when there is no usable key |
| `ApiKeys.cs` | A key from a variable or from the first line of the file another variable names |
| `EmbeddedText.cs` | Reads the guide files the prompts embed from `.claude/skills` |

No key is read at construction time: the caller resolves one and passes it in, so a test can
run the whole loop against a scripted `HttpMessageHandler` with no network and no key.
