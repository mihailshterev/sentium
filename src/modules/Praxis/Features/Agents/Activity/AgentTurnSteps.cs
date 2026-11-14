using System.Text;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Conversations;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Praxis.Features.Agents.Activity;

/// <summary>
/// Builds the ordered activity timeline and the answer of one assistant turn from its raw messages, so a reloaded
/// conversation shows what the live stream showed.
/// </summary>
internal static class AgentTurnSteps
{
    /// <summary>
    /// Walks <paramref name="messages"/> in order: reasoning becomes thought steps (adjacent ones merged), each tool call a
    /// tool step that its result, approval request and approval decision update in place, and text followed by a later
    /// tool call or reasoning becomes a <c>text</c> step. The text after the last of those is the answer; when there is
    /// none, the last narration is promoted to the answer.
    /// </summary>
    public static (string Answer, List<MessageStep> Steps) Build(IEnumerable<ChatMessage> messages, IReadOnlyDictionary<string, long>? durations = null)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var steps = new List<MessageStep>();
        var text = new StringBuilder();

        void FlushText()
        {
            if (text.Length == 0)
            {
                return;
            }

            var narration = text.ToString().Trim();
            if (narration.Length > 0)
            {
                steps.Add(new MessageStep(StepKinds.Text, Text: narration));
            }

            text.Clear();
        }

        int FindTool(string callId) => steps.FindLastIndex(s => s.Kind == StepKinds.Tool && s.CallId == callId);

        void UpsertCall(FunctionCallContent call, string status)
        {
            var index = FindTool(call.CallId);
            if (index >= 0)
            {
                return;
            }

            FlushText();
            steps.Add(new MessageStep(StepKinds.Tool, CallId: call.CallId, Name: call.Name, Arguments: AgentActivityFormat.Arguments(call.Arguments), Status: status));
        }

        foreach (var message in messages)
        {
            var fromAssistant = message.Role == ChatRole.Assistant;

            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent t when fromAssistant:
                        text.Append(t.Text);
                        break;

                    case TextReasoningContent r when fromAssistant && !string.IsNullOrEmpty(r.Text):
                        FlushText();
                        if (steps.Count > 0 && steps[^1].Kind == StepKinds.Thought)
                        {
                            steps[^1] = steps[^1] with { Text = steps[^1].Text + r.Text };
                        }
                        else
                        {
                            steps.Add(new MessageStep(StepKinds.Thought, Text: r.Text));
                        }

                        break;

                    case ToolApprovalRequestContent { ToolCall: FunctionCallContent call }:
                        UpsertCall(call, ToolStatuses.AwaitingApproval);
                        break;

                    case ToolApprovalResponseContent { ToolCall: FunctionCallContent call } response:
                        UpsertCall(call, ToolStatuses.AwaitingApproval);
                        var pending = FindTool(call.CallId);
                        steps[pending] = steps[pending] with { Status = response.Approved ? ToolStatuses.Running : ToolStatuses.Rejected };
                        break;

                    case FunctionCallContent call when fromAssistant && !string.IsNullOrEmpty(call.CallId):
                        UpsertCall(call, ToolStatuses.Running);
                        break;

                    case FunctionResultContent result when !string.IsNullOrEmpty(result.CallId):
                        var index = FindTool(result.CallId);
                        if (index >= 0)
                        {
                            var step = steps[index];
                            steps[index] = step with
                            {
                                Status = step.Status == ToolStatuses.Rejected ? ToolStatuses.Rejected : AgentActivityFormat.Status(result),
                                Result = AgentActivityFormat.Result(result),
                                DurationMs = durations is not null && durations.TryGetValue(result.CallId, out var ms) ? ms : null,
                            };
                        }

                        break;
                }
            }
        }

        var answer = text.ToString();
        if (string.IsNullOrWhiteSpace(answer) && steps.Count > 0 && steps[^1].Kind == StepKinds.Text)
        {
            answer = steps[^1].Text ?? string.Empty;
            steps.RemoveAt(steps.Count - 1);
        }

        return (answer, steps);
    }
}
