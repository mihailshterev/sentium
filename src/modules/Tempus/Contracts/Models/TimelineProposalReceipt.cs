namespace Sentium.Tempus.Contracts.Models;

/// <param name="AlreadyPending">An identical proposal was already waiting, so no new one was created.</param>
/// <param name="Superseded">Older pending proposals this one replaced.</param>
public sealed record TimelineProposalReceipt(Guid ProposalId, bool AlreadyPending, int Superseded);
