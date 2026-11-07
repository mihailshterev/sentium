/**
 * A deterministic stand-in for Ollama, started by Playwright next to Sentium.Web.
 *
 * It behaves like a real Ollama server that has no models installed - exactly the state the suite
 * asserts against - without pulling a multi-GB image or depending on non-deterministic inference.
 * Anything that would need a model answers the way Ollama does when the model is missing.
 * Chat responses are mocked per test in the browser instead (see support/mock-sse.ts).
 *
 * Usage: node support/ollama-stub.ts [port]
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
    case "GET /":
    case "HEAD /":
      res.writeHead(200, { "Content-Type": "text/plain" });
      res.end("Ollama is running");
      return;
    case "GET /api/version":
      return json(res, 200, { version: "0.0.0-e2e-stub" });
    case "GET /api/tags":
    case "GET /api/ps":
      return json(res, 200, { models: [] });
    default:
      // Drain the body so keep-alive connections stay usable.
      req.resume();
      return json(res, 404, { error: "model not found (e2e Ollama stub has no models)" });
  }
});

server.listen(port, "127.0.0.1", () => {
  console.log(`Ollama stub listening on http://127.0.0.1:${port}`);
});

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
