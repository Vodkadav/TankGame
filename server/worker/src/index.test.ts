import { createExecutionContext, waitOnExecutionContext, env } from "cloudflare:test";
import { describe, it, expect, vi, beforeEach } from "vitest";

const capturedExceptions: unknown[] = [];

vi.mock("@sentry/cloudflare", () => {
  return {
    withSentry: <H extends ExportedHandler>(
      _optionsCallback: (env: unknown) => unknown,
      handler: H,
    ): H => {
      const originalFetch = handler.fetch;
      if (originalFetch) {
        handler.fetch = (async (request, fetchEnv, ctx) => {
          try {
            return await originalFetch.call(handler, request, fetchEnv, ctx);
          } catch (e) {
            capturedExceptions.push(e);
            throw e;
          }
        }) as H["fetch"];
      }
      return handler;
    },
  };
});

beforeEach(() => {
  capturedExceptions.length = 0;
});

describe("Worker", () => {
  it("GET /healthz returns 200 ok (regression: Sentry wrap does not break existing routes)", async () => {
    const { default: worker } = await import("./index");
    const request = new Request("http://localhost/healthz");
    const ctx = createExecutionContext();
    const response = await worker.fetch!(request, env, ctx);
    await waitOnExecutionContext(ctx);

    expect(response.status).toBe(200);
    expect(await response.text()).toBe("ok");
    expect(capturedExceptions).toHaveLength(0);
  });

  it("unknown path returns 404", async () => {
    const { default: worker } = await import("./index");
    const request = new Request("http://localhost/unknown-path");
    const ctx = createExecutionContext();
    const response = await worker.fetch!(request, env, ctx);
    await waitOnExecutionContext(ctx);

    expect(response.status).toBe(404);
  });

  it("GET /test-throw throws and Sentry captures the exception", async () => {
    const { default: worker } = await import("./index");
    const request = new Request("http://localhost/test-throw");
    const ctx = createExecutionContext();

    await expect(worker.fetch!(request, env, ctx)).rejects.toThrow(
      "Sentry test throw — M0-T11",
    );
    await waitOnExecutionContext(ctx);

    expect(capturedExceptions).toHaveLength(1);
    expect(capturedExceptions[0]).toBeInstanceOf(Error);
    expect((capturedExceptions[0] as Error).message).toBe(
      "Sentry test throw — M0-T11",
    );
  });
});
