import * as Sentry from "@sentry/cloudflare";
import { withSentry } from "@sentry/cloudflare";
import { MatchRoom } from "./MatchRoom";
import { createLobby, joinLobby } from "./lobby";
import { listOpenLobbies, LobbyDirectory } from "./lobbyDirectory";
import {
  checkRequestBudget,
  cloudflareAnalytics,
  monthStart,
  DEFAULT_DO_REQUEST_BUDGET,
  type BudgetAlerter,
} from "./budget";

interface Env {
  SENTRY_DSN_WORKER: string;
  MATCH_ROOM: DurableObjectNamespace;
  LOBBY_DIRECTORY: DurableObjectNamespace;
  LOBBY_KV: KVNamespace;
  // Read-only Analytics token + account for the request-budget cron (M3-T11). Optional: absent
  // locally, where the scheduled run is a safe no-op. DO_REQUEST_BUDGET overrides the default cap.
  CF_API_TOKEN?: string;
  CF_ACCOUNT_ID?: string;
  DO_REQUEST_BUDGET?: string;
}

const JOIN_ROUTE = /^\/lobby\/([^/]+)\/join$/;

// The WASM client on lundrea-arcade.web.app calls the lobby routes cross-origin; without these
// headers the browser blocks every response. Public game API, no credentials — "*" is right.
const CORS = { "Access-Control-Allow-Origin": "*" };

const handler: ExportedHandler<Env> = {
  async fetch(request, env, _ctx) {
    const url = new URL(request.url);

    if (request.method === "OPTIONS") {
      return new Response(null, {
        status: 204,
        headers: {
          ...CORS,
          "Access-Control-Allow-Methods": "GET, POST",
          "Access-Control-Allow-Headers": "Content-Type",
        },
      });
    }

    if (request.method === "GET" && url.pathname === "/healthz") {
      return new Response("ok", { status: 200 });
    }
    if (request.method === "GET" && url.pathname === "/test-throw") {
      throw new Error("Sentry test throw — M0-T11");
    }

    // Lobby registry (M3-T4): create a lobby code, or validate a join.
    if (request.method === "POST" && url.pathname === "/lobby") {
      const lobby = await createLobby(env.LOBBY_KV, env.MATCH_ROOM);
      return Response.json(lobby, { status: 201, headers: CORS });
    }

    // Lobby browser (multiplayer milestone): every currently-joinable lobby, for the in-client list.
    if (request.method === "GET" && url.pathname === "/lobbies") {
      const directory = env.LOBBY_DIRECTORY.get(env.LOBBY_DIRECTORY.idFromName("global"));
      const open = await listOpenLobbies(directory);
      return Response.json(open, { headers: CORS });
    }
    const joinMatch = JOIN_ROUTE.exec(url.pathname);
    if (request.method === "POST" && joinMatch) {
      const joined = await joinLobby(env.LOBBY_KV, joinMatch[1]);
      return joined
        ? Response.json(joined, { headers: CORS })
        : new Response("no such lobby", { status: 404, headers: CORS });
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

  // Cron-triggered request-budget alarm (M3-T11, ADR-0005 §4): read this month's Durable Object
  // request usage and warn Sentry at 80% of the free-tier budget. The remedy is to refuse new
  // lobbies, never to enable the paid plan. Wrapped by withSentry, so captureMessage is delivered.
  async scheduled(event, env, ctx) {
    const alerter: BudgetAlerter = { alert: (message) => Sentry.captureMessage(message, "warning") };
    const analytics = cloudflareAnalytics(
      env.CF_ACCOUNT_ID,
      env.CF_API_TOKEN,
      monthStart(new Date(event.scheduledTime)),
    );
    const budget = env.DO_REQUEST_BUDGET ? Number(env.DO_REQUEST_BUDGET) : DEFAULT_DO_REQUEST_BUDGET;
    ctx.waitUntil(checkRequestBudget(analytics, alerter, budget));
  },
};

export { MatchRoom, LobbyDirectory };

export default withSentry(
  (env: Env) => ({
    dsn: env.SENTRY_DSN_WORKER,
    tracesSampleRate: 0,
  }),
  handler,
);
