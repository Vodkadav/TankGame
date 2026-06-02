import * as Sentry from "@sentry/cloudflare";

export interface Env {
  SENTRY_DSN_WORKER?: string;
}

const handler = {
  async fetch(request: Request, _env: Env, _ctx: ExecutionContext): Promise<Response> {
    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname === "/healthz") {
      return new Response("ok", { status: 200 });
    }

    // Deliberate failure used to verify Sentry capture end-to-end (M0 DoD).
    if (request.method === "GET" && url.pathname === "/test-throw") {
      throw new Error("Sentry test exception — /test-throw (M0 verification)");
    }

    return new Response("not found", { status: 404 });
  },
} satisfies ExportedHandler<Env>;

export default Sentry.withSentry(
  (env: Env) => ({
    dsn: env.SENTRY_DSN_WORKER,
    tracesSampleRate: 0,
  }),
  handler,
);
