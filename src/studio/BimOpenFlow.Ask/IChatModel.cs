using System.Text.Json.Nodes;

namespace BimOpenFlow.Ask;

/// <summary>One model turn for the agent loop. The conversation and tool list are in the
/// loop's own shape, which is the OpenAI chat-completions shape: messages with role
/// 'system', 'user', 'assistant' (content plus tool_calls) or 'tool' (tool_call_id plus
/// content), and tools as {type:'function', function:{name, description, parameters}}.
/// The reply is a chat-completions response: choices[0].message and usage
/// {prompt_tokens, completion_tokens}. A provider with another wire format translates
/// both ways and may stash what it needs to replay on the assistant message it returns,
/// because the loop appends that message verbatim and sends the whole conversation back.</summary>
public interface IChatModel
{
    /// <summary>The provider's name for status lines: 'openai' or 'anthropic'.</summary>
    string Provider { get; }

    string Model { get; }

    Task<JsonObject> CompleteAsync(JsonArray messages, JsonArray tools, CancellationToken ct);
}
