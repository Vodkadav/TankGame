import { withSentry } from "@sentry/cloudflare";

interface Env {
  SENTRY_DSN_WORKER: string;
}

const handler: ExportedHandler<Env> = {
  fetch(request, _env, _ctx) {
    const url = new URL(request.url);
    if (request.method === "GET" && url.pathname === "/healthz") {
      return new Response("ok", { status: 200 });
    }
    if (request.method === "GET" && url.pathname === "/test-throw") {
      throw new Error("Sentry test throw — M0-T11");
    }
    return new Response("not found", { status: 404 });
  },
};

export default withSentry(
  (env: Env) => ({
    dsn: env.SENTRY_DSN_WORKER,
    tracesSampleRate: 0,
  }),
  handler,
);
