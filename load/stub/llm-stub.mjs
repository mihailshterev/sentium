/**
 * An OpenAI-compatible stand-in for the llama.cpp servers, for load tests.
 *
 * Unlike e2e/support/llm-stub.ts (which mimics a router with no models), this one declares the chat and embedding
 * models as loaded and answers chat completions and embeddings with a configurable latency model. The load suite
 * then measures Sentium itself - queuing, persistence, streaming, pgvector - not the speed of a real model.
 *
 * It never returns tool calls, so assistant turns don't stop for approvals and Sentinel's semantic-intent check
 * (which only runs on tool calls) never runs.
 *
 * Configuration (environment):
 *   PORT                 listen port (default 8080)
 *   STUB_CHAT_MODEL      chat model id (default gemma-4-e4b, the Testing environment's default model)
 *   STUB_EMBED_MODEL     embedding model id (default embeddinggemma-2)
 *   STUB_EMBED_DIM       embedding dimensions; must match codex.knowledge_chunks (default 768)
 *   STUB_TTFT_MS         time to first token (default 300)
 *   STUB_TOKENS          tokens per answer (default 60)
 *   STUB_TOKENS_PER_SEC  generation speed (default 40)
 *   STUB_EMBED_MS        latency of one embeddings request (default 25)
 *   STUB_JITTER          relative +/- jitter applied to every delay (default 0.2)
 */
import { createServer } from "node:http";
import { createHash } from "node:crypto";

const env = (name, fallback) => {
  const value = Number(process.env[name]);
  return Number.isFinite(value) && process.env[name] !== "" ? value : fallback;
};

const port = env("PORT", 8080);
const chatModel = process.env.STUB_CHAT_MODEL || "gemma-4-e4b";
const embedModel = process.env.STUB_EMBED_MODEL || "embeddinggemma-2";
const embedDim = env("STUB_EMBED_DIM", 768);
const ttftMs = env("STUB_TTFT_MS", 300);
const answerTokens = env("STUB_TOKENS", 60);
const tokensPerSec = env("STUB_TOKENS_PER_SEC", 40);
const embedMs = env("STUB_EMBED_MS", 25);
const jitter = env("STUB_JITTER", 0.2);

const CONTEXT_LENGTH = 8192;
const WORDS = [
  "the",
  "drill",
  "is",
  "on",
  "the",
  "garage",
  "shelf",
  "next",
  "to",
  "your",
  "toolbox",
  "and",
  "the",
  "manual",
  "says",
  "to",
  "charge",
  "it",
  "before",
  "use",
  "your",
  "timeline",
  "shows",
  "three",
  "tasks",
  "this",
  "week",
];

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const jittered = (ms) => Math.max(0, ms * (1 + (Math.random() * 2 - 1) * jitter));
const tokenDelay = () => jittered(1000 / tokensPerSec);

const json = (res, status, body) => {
  res.writeHead(status, { "Content-Type": "application/json" });
  res.end(JSON.stringify(body));
};

const readBody = async (req) => {
  const chunks = [];
  for await (const chunk of req) {
    chunks.push(chunk);
  }
  const text = Buffer.concat(chunks).toString("utf8");
  try {
    return text ? JSON.parse(text) : {};
  } catch {
    return {};
  }
};

const modelEntry = (id) => ({
  id,
  object: "model",
  owned_by: "load-stub",
  status: { value: "loaded" },
  meta: { n_ctx: CONTEXT_LENGTH },
});

const answerText = () => {
  const words = [];
  for (let i = 0; i < answerTokens; i++) {
    words.push(WORDS[i % WORDS.length]);
  }
  return words.join(" ");
};

const usage = (completionTokens) => ({
  prompt_tokens: 200,
  completion_tokens: completionTokens,
  total_tokens: 200 + completionTokens,
});

/** Callers asking for structured output (learning curation, squad plans) get valid, empty JSON. */
const wantsJson = (body) => body.response_format && body.response_format.type !== "text";

async function chatCompletion(req, res) {
  const body = await readBody(req);
  const id = `chatcmpl-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 8)}`;
  const created = Math.floor(Date.now() / 1000);
  const model = body.model || chatModel;

  await sleep(jittered(ttftMs));

  if (!body.stream) {
    const content = wantsJson(body) ? "{}" : answerText();
    if (!wantsJson(body)) {
      await sleep(tokenDelay() * answerTokens);
    }
    return json(res, 200, {
      id,
      object: "chat.completion",
      created,
      model,
      choices: [{ index: 0, message: { role: "assistant", content }, finish_reason: "stop" }],
      usage: usage(answerTokens),
    });
  }

  res.writeHead(200, { "Content-Type": "text/event-stream", "Cache-Control": "no-cache", Connection: "keep-alive" });
  const send = (delta, finishReason = null, extra = {}) =>
    res.write(
      `data: ${JSON.stringify({
        id,
        object: "chat.completion.chunk",
        created,
        model,
        choices: [{ index: 0, delta, finish_reason: finishReason }],
        ...extra,
      })}\n\n`,
    );

  let closed = false;
  req.on("close", () => {
    closed = true;
  });

  send({ role: "assistant", content: "" });
  const words = answerText().split(" ");
  for (let i = 0; i < words.length && !closed; i++) {
    send({ content: (i === 0 ? "" : " ") + words[i] });
    await sleep(tokenDelay());
  }
  if (closed) {
    return;
  }
  send({}, "stop");
  if (body.stream_options?.include_usage) {
    res.write(
      `data: ${JSON.stringify({ id, object: "chat.completion.chunk", created, model, choices: [], usage: usage(words.length) })}\n\n`,
    );
  }
  res.end("data: [DONE]\n\n");
}

/** A deterministic unit vector per text, so equal texts embed equally and searches return stable neighbours. */
function embed(text) {
  const vector = new Float32Array(embedDim);
  let seed = createHash("sha256").update(text).digest().readUInt32LE(0) || 1;
  let norm = 0;
  for (let i = 0; i < embedDim; i++) {
    seed ^= seed << 13;
    seed ^= seed >>> 17;
    seed ^= seed << 5;
    const value = ((seed >>> 0) / 0xffffffff) * 2 - 1;
    vector[i] = value;
    norm += value * value;
  }
  norm = Math.sqrt(norm);
  for (let i = 0; i < embedDim; i++) {
    vector[i] /= norm;
  }
  return vector;
}

async function embeddings(req, res) {
  const body = await readBody(req);
  const inputs = Array.isArray(body.input) ? body.input : [body.input ?? ""];
  const base64 = body.encoding_format === "base64";

  await sleep(jittered(embedMs));

  return json(res, 200, {
    object: "list",
    model: body.model || embedModel,
    data: inputs.map((input, index) => {
      const vector = embed(String(input));
      return {
        object: "embedding",
        index,
        embedding: base64 ? Buffer.from(vector.buffer).toString("base64") : Array.from(vector),
      };
    }),
    usage: { prompt_tokens: inputs.length * 16, total_tokens: inputs.length * 16 },
  });
}

const server = createServer((req, res) => {
  const path = new URL(req.url ?? "/", "http://localhost").pathname.replace(/^\/v1(?=\/)/, "");

  switch (`${req.method} ${path}`) {
    case "GET /health":
      return json(res, 200, { status: "ok" });
    case "GET /models":
      return json(res, 200, { object: "list", data: [modelEntry(chatModel), modelEntry(embedModel)] });
    case "POST /models/load":
    case "POST /models/unload":
      req.resume();
      return json(res, 200, { success: true });
    case "GET /metrics":
      res.writeHead(200, { "Content-Type": "text/plain; version=0.0.4" });
      return res.end("");
    case "POST /chat/completions":
      return chatCompletion(req, res).catch((error) => {
        console.error(error);
        if (!res.headersSent) {
          json(res, 500, { error: { message: String(error) } });
        } else {
          res.end();
        }
      });
    case "POST /embeddings":
      return embeddings(req, res).catch((error) => {
        console.error(error);
        json(res, 500, { error: { message: String(error) } });
      });
    default:
      // Drain the body so keep-alive connections stay usable.
      req.resume();
      return json(res, 404, {
        error: { code: 404, message: `not found: ${req.method} ${path}`, type: "not_found_error" },
      });
  }
});

server.keepAliveTimeout = 65_000;
server.listen(port, "0.0.0.0", () => {
  console.log(
    `Load LLM stub on :${port} (chat=${chatModel}, embed=${embedModel}/${embedDim}d, ttft=${ttftMs}ms, ` +
      `${answerTokens} tokens @ ${tokensPerSec}/s, embed=${embedMs}ms)`,
  );
});

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
