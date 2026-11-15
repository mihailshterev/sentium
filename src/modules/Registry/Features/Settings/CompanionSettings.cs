namespace Sentium.Registry.Features.Settings;

/// <summary>
/// A user's own Companion and voice preferences. The portal reads and applies them; the server only stores them.
/// </summary>
internal sealed class CompanionSettings
{
    public const double MinSpeakingRate = 0.5;
    public const double MaxSpeakingRate = 2.0;
    public const int MaxVoiceIdLength = 64;

    /// <summary>
    /// Shows the animated Companion in the portal.
    /// </summary>
    public bool IsCompanionEnabled { get; set; } = true;

    /// <summary>
    /// Offers push-to-talk and speaks replies aloud. Has no effect while the speech servers are unavailable.
    /// </summary>
    public bool IsVoiceEnabled { get; set; } = true;

    /// <summary>
    /// The chosen Voice id; <see langword="null"/> uses the deployment's default Voice.
    /// </summary>
    public string? Voice { get; set; }

    /// <summary>
    /// Speaking rate multiplier, where 1.0 is normal speed.
    /// </summary>
    public double SpeakingRate { get; set; } = 1.0;

    /// <summary>
    /// When true a transcript is placed in the input for the user to check; otherwise it is sent on release.
    /// </summary>
    public bool IsReviewBeforeSendingEnabled { get; set; }

    /// <summary>
    /// When true replies to typed messages are spoken too; otherwise only replies to spoken messages are.
    /// </summary>
    public bool IsSpeakTypedRepliesEnabled { get; set; }
}
