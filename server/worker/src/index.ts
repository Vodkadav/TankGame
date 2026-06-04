import { withSentry } from "@sentry/cloudflare";
import { MatchRoom } from "./MatchRoom";

interface Env {
  SENTRY_DSN_WORKER: string;
  MATCH_ROOM: DurableObjectNamespace;
}

const handler: ExportedHandler<Env> = {
  fetch(request, env, _ctx) {
    const url = new URL(request.url);
    if (request.method === "GET" && url.pathname === "/healthz") {
      return new Response("ok", { status: 200 });
    }
    if (request.method === "GET" && url.pathname === "/test-throw") {
      throw new Error("Sentry test throw — M0-T11");
    }
    // /room/:code → the lobby's Durable Object (one DO per code). The DO handles the WebSocket
    // upgrade; the lobby-code → DO mapping and validation come with M3-T4.
    if (url.pathname.startsWith("/room/")) {
      const code = url.pathname.slice("/room/".length);
      if (code.length === 0) {
        return new Response("missing lobby code", { status: 400 });
      }
      const id = env.MATCH_ROOM.idFromName(code);
      return env.MATCH_ROOM.get(id).fetch(request);
    }
    return new Response("not found", { status: 404 });
  },
};

export { MatchRoom };

export default withSentry(
  (env: Env) => ({
    dsn: env.SENTRY_DSN_WORKER,
    tracesSampleRate: 0,
  }),
  handler,
);
