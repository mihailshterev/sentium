import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useConversationStore } from "@/features/assistant/state/assistant-conversation-store";
import { useNotifications } from "@/features/notifications/hooks/useInbox";
import { useOrchestrationRunStore } from "@/features/orchestration/state/orchestration-run-store";
import { TASK_STATUS_MUTATION_KEY } from "@/features/timeline/hooks/useTimeline";
import { useTimelineChatStore } from "@/features/timeline/state/timeline-chat-store";
import { unlockAudio } from "../audio/audio-context";
import { recordingSupported } from "../audio/recorder";
import { attachReplySpeaker } from "../audio/reply-speaker";
import { speechPlayer } from "../audio/speech-player";
import { useCompanionStore } from "../state/companion-store";
import { useQuickChatStore } from "../state/quick-chat-store";
import { useCompanionSettings, useSpeechStatus } from "./useCompanionSettings";

/** No pointer or key input for this long puts the Companion to sleep. */
const SLEEP_AFTER_MS = 10 * 60_000;
const ACTIVITY_EVENTS = ["pointerdown", "pointermove", "keydown", "wheel"] as const;
const UNREAD_SCAN_LIMIT = 20;

/**
 * Feeds the Companion from what already exists - the conversation stores, the orchestration run, the inbox, task
 * mutations and user activity - and speaks replies. Mount it once, above the routes, so nothing here restarts on
 * navigation.
 */
export function useCompanionRuntime(): void {
  const queryClient = useQueryClient();
  const settings = useCompanionSettings();
  const status = useSpeechStatus(settings.isVoiceEnabled);
  const setSignal = useCompanionStore((s) => s.setSignal);
  const setVoice = useCompanionStore((s) => s.setVoice);

  const assistantStreaming = useConversationStore((s) => s.isStreaming);
  const timelineStreaming = useTimelineChatStore((s) => s.isStreaming);
  const quickChatStreaming = useQuickChatStore((s) => s.isStreaming);
  const orchestrationRunning = useOrchestrationRunStore((s) => s.isRunning);
  const transcribing = useCompanionStore((s) => s.voicePhase === "transcribing");

  // The first sentence of a spoken reply is still being synthesised: not speaking yet, but not idle either.
  const [preparingSpeech, setPreparingSpeech] = useState(false);

  const thinking =
    assistantStreaming ||
    timelineStreaming ||
    quickChatStreaming ||
    orchestrationRunning ||
    transcribing ||
    preparingSpeech;
  useEffect(() => setSignal("thinking", thinking), [thinking, setSignal]);

  const { notifications } = useNotifications(true, UNREAD_SCAN_LIMIT);
  const alert = notifications.some((n) => !n.readAt && n.severity !== "Info");
  useEffect(() => setSignal("alert", alert), [alert, setSignal]);

  const { isVoiceEnabled, voice, speakingRate, isReviewBeforeSendingEnabled, isSpeakTypedRepliesEnabled } = settings;
  const canListen = isVoiceEnabled && status.transcription && recordingSupported();
  const canSpeak = isVoiceEnabled && status.synthesis;
  useEffect(() => {
    setVoice({
      canListen,
      canSpeak,
      voice,
      rate: speakingRate,
      reviewBeforeSending: isReviewBeforeSendingEnabled,
      speakTypedReplies: isSpeakTypedRepliesEnabled,
    });
    if (!canSpeak) {
      speechPlayer.stop();
    }
  }, [canListen, canSpeak, voice, speakingRate, isReviewBeforeSendingEnabled, isSpeakTypedRepliesEnabled, setVoice]);

  useEffect(() => {
    const detach = [
      attachReplySpeaker(useConversationStore),
      attachReplySpeaker(useQuickChatStore),
      attachReplySpeaker(useTimelineChatStore),
      speechPlayer.subscribe(({ speaking, preparing }) => {
        setSignal("speaking", speaking);
        setPreparingSpeech(preparing);
      }),
    ];
    return () => {
      detach.forEach((off) => off());
      speechPlayer.stop();
    };
  }, [setSignal]);

  // Pleased: a run that finished well, or a task ticked off.
  useEffect(() => {
    const { cheer } = useCompanionStore.getState();

    const offRuns = useOrchestrationRunStore.subscribe((state, previous) => {
      if (previous.isRunning && !state.isRunning && state.lastOutcome === "completed") {
        cheer();
      }
    });

    const offTasks = queryClient.getMutationCache().subscribe((event) => {
      if (event.type !== "updated" || event.action.type !== "success") {
        return;
      }
      const { options, state } = event.mutation;
      const ticked = (state.variables as { status?: string } | undefined)?.status === "Done";
      if (ticked && options.mutationKey?.[0] === TASK_STATUS_MUTATION_KEY[0]) {
        cheer();
      }
    });

    return () => {
      offRuns();
      offTasks();
    };
  }, [queryClient]);

  // Sleeping after long inactivity; any input wakes it and unlocks audio for replies that arrive later.
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout>;
    let lastSeen = 0;

    const onActivity = (event: Event) => {
      if (event.type !== "pointermove" && event.type !== "wheel") {
        unlockAudio();
      }
      const now = Date.now();
      if (now - lastSeen < 1000) {
        return;
      }
      lastSeen = now;
      setSignal("sleeping", false);
      clearTimeout(timer);
      timer = setTimeout(() => setSignal("sleeping", true), SLEEP_AFTER_MS);
    };

    timer = setTimeout(() => setSignal("sleeping", true), SLEEP_AFTER_MS);
    ACTIVITY_EVENTS.forEach((type) => window.addEventListener(type, onActivity, { passive: true }));
    return () => {
      clearTimeout(timer);
      ACTIVITY_EVENTS.forEach((type) => window.removeEventListener(type, onActivity));
    };
  }, [setSignal]);
}
