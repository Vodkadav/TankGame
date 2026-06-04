import { withSentry } from "@sentry/cloudflare";
import { MatchRoom } from "./MatchRoom";
import { createLobby, joinLobby } from "./lobby";

interface Env {
  SENTRY_DSN_WORKER: string;
  MATCH_ROOM: DurableObjectNamespace;
  LOBBY_KV: KVNamespace;
}

const JOIN_ROUTE = /^\/lobby\/([^/]+)\/join$/;

const handler: ExportedHandler<Env> = {
  async fetch(request, env, _ctx) {
    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname === "/healthz") {
      return new Response("ok", { status: 200 });
    }
    if (request.method === "GET" && url.pathname === "/test-throw") {
      throw new Error("Sentry test throw — M0-T11");
    }

    // Lobby registry (M3-T4): create a lobby code, or validate a join.
    if (request.method === "POST" && url.pathname === "/lobby") {
      const lobby = await createLobby(env.LOBBY_KV, env.MATCH_ROOM);
      return Response.json(lobby, { status: 201 });
    }
    const joinMatch = JOIN_ROUTE.exec(url.pathname);
    if (request.method === "POST" && joinMatch) {
      const joined = await joinLobby(env.LOBBY_KV, joinMatch[1]);
      return joined ? Response.json(joined) : new Response("no such lobby", { status: 404 });
    }

    // /room/:code → the lobby's Durable Object (one DO per code). The DO handles the WebSocket
    // upgrade; in normal flow a client reaches this URL via the join response's wsUrl.
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
