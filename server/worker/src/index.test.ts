import { env, createExecutionContext, waitOnExecutionContext } from "cloudflare:test";
import { describe, it, expect, vi, beforeEach } from "vitest";

// State must be created via vi.hoisted: the vi.mock factory is hoisted above
// ordinary declarations, so plain top-level consts are still in their TDZ when
// ./index is imported and calls withSentry.
const h = vi.hoisted(() => ({
  captured: [] as Error[],
  optionsFn: undefined as undefined | ((env: Record<string, unknown>) => { dsn?: string }),
}));

// Mock the Sentry wrapper so we can observe that index.ts wraps the handler and
// that thrown errors flow into capture (real DSN delivery is deploy-verified).
vi.mock("@sentry/cloudflare", () => ({
  withSentry: (
    optsFn: (env: Record<string, unknown>) => { dsn?: string },
    handler: { fetch: (req: Request, env: unknown, ctx: unknown) => Promise<Response> },
  ) => {
    h.optionsFn = optsFn;
    return {
      async fetch(req: Request, e: unknown, ctx: unknown) {
        try {
          return await handler.fetch(req, e, ctx);
        } catch (err) {
          h.captured.push(err as Error);
          throw err;
        }
      },
    };
  },
}));

import worker from "./index";

describe("Worker", () => {
  beforeEach(() => {
    h.captured.length = 0;
  });

  it("GET /healthz returns 200 with body 'ok'", async () => {
    const ctx = createExecutionContext();
    const res = await worker.fetch(new Request("http://x/healthz"), env, ctx);
    await waitOnExecutionContext(ctx);
    expect(res.status).toBe(200);
    expect(await res.text()).toBe("ok");
  });

  it("GET /unknown returns 404", async () => {
    const ctx = createExecutionContext();
    const res = await worker.fetch(new Request("http://x/unknown"), env, ctx);
    await waitOnExecutionContext(ctx);
    expect(res.status).toBe(404);
  });
});

describe("Worker Sentry wiring", () => {
  beforeEach(() => {
    h.captured.length = 0;
  });

  it("maps SENTRY_DSN_WORKER from env into the Sentry options", () => {
    expect(h.optionsFn).toBeDefined();
    const opts = h.optionsFn!({ SENTRY_DSN_WORKER: "https://abc@o0.ingest.sentry.io/1" });
    expect(opts.dsn).toBe("https://abc@o0.ingest.sentry.io/1");
  });

  it("routes a thrown exception on /test-throw into Sentry capture", async () => {
    const ctx = createExecutionContext();
    await expect(
      worker.fetch(new Request("http://x/test-throw"), env, ctx),
    ).rejects.toThrow(/Sentry test exception/);
    expect(h.captured).toHaveLength(1);
  });
});
