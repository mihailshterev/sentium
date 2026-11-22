using System.Globalization;
using Microsoft.Agents.AI;
using Sentium.Praxis.Contracts;
using Sentium.Registry.Contracts;

namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// Tells the agent today's date in the user's time zone on every run. Without it a local model assumes the year its
/// training data ends in, so "this year" becomes the wrong year and real recent dates read as "the future".
/// </summary>
/// <remarks>
/// Only the date is given, not the time: this block joins the system message, which precedes the chat history, so a
/// value that changed every minute would invalidate llama-server's prompt cache for the whole conversation each turn.
/// The <c>datetime-utils</c> skill gives the exact time when an agent needs it.
/// </remarks>
internal sealed class CurrentDateContextProvider(
    ISettingsService settings,
    IPdpContextAccessor runContext,
    TimeProvider timeProvider,
    ILogger<CurrentDateContextProvider> logger) : AIContextProvider(null, null, null)
{
    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        var zone = await ResolveTimeZoneAsync(runContext.UserId, cancellationToken);
        return new AIContext { Instructions = BuildBlock(timeProvider.GetUtcNow(), zone) };
    }

    internal static string BuildBlock(DateTimeOffset utcNow, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(utcNow, zone);
        var date = local.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture);
        var offset = local.Offset;
        var sign = offset < TimeSpan.Zero ? "-" : "+";

        return $"""
            ### CURRENT DATE
            Today is {date} ({zone.Id}, UTC{sign}{offset:hh\:mm}). Your training data is older than this: resolve "today", "this year", "recently" and other relative dates from this date, and treat dates up to it as past, not future.
            """;
    }

    private async Task<TimeZoneInfo> ResolveTimeZoneAsync(Guid? userId, CancellationToken ct)
    {
        try
        {
            return (await settings.GetEffectiveCalendarAsync(userId, ct)).TimeZone;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the calendar settings of {UserId}; giving the agent the UTC date", userId);
            return TimeZoneInfo.Utc;
        }
    }
}
