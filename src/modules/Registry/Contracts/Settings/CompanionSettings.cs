using System.ComponentModel.DataAnnotations;
using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// A user's own Companion and voice preferences. The portal reads and applies them; the server only stores them.
/// </summary>
[SettingsGroup(
    SettingsKeys.Companion,
    SettingsScope.Personal,
    "companion",
    "Companion & voice",
    "The animated Companion in the sidebar, push-to-talk and spoken replies. Speech runs on the home server; voice controls only appear while its speech services are up.",
    Order = 2)]
public sealed class CompanionSettings
{
    public const double MinSpeakingRate = 0.5;
    public const double MaxSpeakingRate = 2.0;
    public const int MaxVoiceIdLength = 64;

    /// <summary>
    /// Shows the animated Companion in the portal.
    /// </summary>
    [Setting(
        "Show the Companion",
        "The animated character that shows what the assistant is doing and opens a quick chat.",
        Keywords = ["character", "mascot", "avatar", "sidebar"])]
    public bool IsCompanionEnabled { get; set; } = true;

    /// <summary>
    /// Offers push-to-talk and speaks replies aloud. Has no effect while the speech servers are unavailable.
    /// </summary>
    [Setting(
        "Voice",
        "Hold the microphone button to talk, and hear the reply spoken. The microphone is open only while you hold it.",
        Keywords = ["speech", "microphone", "push to talk", "tts", "stt", "audio"])]
    public bool IsVoiceEnabled { get; set; } = true;

    /// <summary>
    /// The chosen Voice id; <see langword="null"/> uses the deployment's default Voice.
    /// </summary>
    [Setting(
        "Voice to speak with",
        "Which synthesised voice reads replies.",
        Keywords = ["speech", "tts", "kokoro", "speaker"],
        Control = SettingControlKind.Voice)]
    [MaxLength(MaxVoiceIdLength)]
    public string? Voice { get; set; }

    /// <summary>
    /// Speaking rate multiplier, where 1.0 is normal speed.
    /// </summary>
    [Setting(
        "Speaking rate",
        "How fast replies are spoken.",
        Keywords = ["speech", "speed", "tempo", "tts"],
        Step = 0.1,
        MinLabel = "Slow (0.5×)",
        MaxLabel = "Fast (2×)")]
    [Range(MinSpeakingRate, MaxSpeakingRate)]
    public double SpeakingRate { get; set; } = 1.0;

    /// <summary>
    /// When true a transcript is placed in the input for the user to check; otherwise it is sent on release.
    /// </summary>
    [Setting(
        "Review before sending",
        "Put what you said in the message box to check, instead of sending it when you release the button.",
        Keywords = ["speech", "transcript", "push to talk", "confirm"])]
    public bool IsReviewBeforeSendingEnabled { get; set; }

    /// <summary>
    /// When true replies to typed messages are spoken too; otherwise only replies to spoken messages are.
    /// </summary>
    [Setting(
        "Speak replies to typed messages",
        "When off, only replies to messages you spoke are read aloud.",
        Keywords = ["speech", "tts", "read aloud"])]
    public bool IsSpeakTypedRepliesEnabled { get; set; }
}
