/**
 * Runtime status the inference server reports for a declared chat model. `loading` and `unloaded`
 * transitions are asynchronous; other values (e.g. `sleeping`) may appear and are shown as-is.
 */
export type ModelStatus = "loaded" | "loading" | "unloaded" | "sleeping" | (string & {});

/** A chat model declared in the inference server's presets (infra/llm/models.ini). */
export interface InferenceModel {
  id: string;
  status: ModelStatus;
  /** Context window per request in tokens, once the server has loaded the model. */
  contextLength: number | null;
}

export interface ModelsResponse {
  /** The effective default model (Registry setting, else configuration). */
  defaultModel: string;
  models: InferenceModel[];
}
