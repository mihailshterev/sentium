import { queryOptions } from "@tanstack/react-query";
import { fetchSpeechStatus } from "./speech.service";

export const speechKeys = {
  status: () => ["speech-status"] as const,
};

export const speechQueries = {
  status: () => queryOptions({ queryKey: speechKeys.status(), queryFn: () => fetchSpeechStatus(), staleTime: 30_000 }),
};
