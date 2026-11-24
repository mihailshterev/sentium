namespace Sentium.Praxis.Features.Conversations;

/// <param name="Messages">Oldest first.</param>
/// <param name="HasEarlierMessages">Whether messages older than these exist.</param>
internal sealed record MessagePageResponse(IReadOnlyList<MessageResponse> Messages, bool HasEarlierMessages);
