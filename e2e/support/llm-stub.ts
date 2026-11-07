/**
 * A deterministic stand-in for the llama.cpp inference servers, started by Playwright next to Sentium.Web.
 *
 * It answers like a llama-server router with no models declared - exactly the state the suite asserts
 * against - without pulling multi-GB models or depending on non-deterministic inference. The same stub
 * serves as both the chat and the embeddings endpoint. Anything that would need a model answers the way
 * llama-server does when the model is unknown. Chat responses are mocked per test in the browser instead
 * (see support/mock-sse.ts).
 *
 * Usage: node support/llm-stub.ts [port]
 */
import { createServer, type ServerResponse } from "node:http";

const port = Number(process.argv[2] ?? 21434);

const json = (res: ServerResponse, status: number, body: unknown): void => {
  res.writeHead(status, { "Content-Type": "application/json" });
  res.end(JSON.stringify(body));
};

const server = createServer((req, res) => {
  const url = new URL(req.url ?? "/", "http://localhost");

  switch (`${req.method} ${url.pathname}`) {
    case "GET /health":
    case "GET /v1/health":
      return json(res, 200, { status: "ok" });
    case "GET /models":
    case "GET /v1/models":
      return json(res, 200, { object: "list", data: [] });
    default:
      // Drain the body so keep-alive connections stay usable.
      req.resume();
      return json(res, 404, {
        error: { code: 404, message: "model not found (e2e llm stub declares no models)", type: "not_found_error" },
      });
  }
});

server.listen(port, "127.0.0.1", () => {
  console.log(`LLM stub listening on http://127.0.0.1:${port}`);
});

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
