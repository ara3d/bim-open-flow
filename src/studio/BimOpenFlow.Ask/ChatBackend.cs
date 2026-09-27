using System.Text.Json.Nodes;

namespace BimOpenFlow.Ask;

/// <summary>Runs the tool loop in process over an IChatModel: today's AskAgent, reached through
/// IAskBackend so the studio and the IFC ask never branch on provider.</summary>
public sealed class ChatBackend(AskSetup setup, IChatModel chat) : IAskBackend
{
    /// <summary>One AskAgent (Hidden = setup.Hidden, maxTurns = setup.MaxTurns) and one message
    /// list per conversation; SendAsync is AskAgent.RunAsync(messages, user, emit, ct).</summary>
    public IAskConversation Start(string system)
    {
        var agent = new AskAgent(setup.Tools, chat, setup.MaxTurns) { Hidden = setup.Hidden };
        var messages = AskAgent.NewConversation(system);
        return new Conversation(agent, messages);
    }

    private sealed class Conversation(AskAgent agent, JsonArray messages) : IAskConversation
    {
        public Task<AskOutcome> SendAsync(string user, Func<AskEvent, Task> emit, CancellationToken ct)
            => agent.RunAsync(messages, user, emit, ct);
    }
}
