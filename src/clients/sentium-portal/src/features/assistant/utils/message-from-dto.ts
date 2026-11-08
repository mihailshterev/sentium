import { stepsFromDto } from "@/shared/agent-activity";
import type { ConversationMessage, ConversationMessageDto } from "@/features/assistant/types/assistant";

export const messageFromDto = (dto: ConversationMessageDto): ConversationMessage => {
  const steps = dto.role === "assistant" ? stepsFromDto(dto.id, dto) : [];
  return {
    id: dto.id,
    role: dto.role,
    content: dto.content,
    timestamp: new Date(dto.timestamp),
    enhancedPrompt: dto.enhancedPrompt ?? undefined,
    ...(steps.length > 0 ? { steps } : {}),
  };
};
