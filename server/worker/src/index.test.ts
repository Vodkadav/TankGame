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

  // Regression: the WASM build on lundrea-arcade.web.app calls these routes cross-origin —
  // without CORS headers the browser blocks every response and Create/Refresh fail silently.
  it("POST /lobby response allows cross-origin reads", async () => {
    const { default: worker } = await import("./index");
    const request = new Request("http://localhost/lobby", { method: "POST" });
    const ctx = createExecutionContext();
    const response = await worker.fetch!(request, env, ctx);
    await waitOnExecutionContext(ctx);

    expect(response.status).toBe(201);
    expect(response.headers.get("access-control-allow-origin")).toBe("*");
  });

  it("GET /lobbies response allows cross-origin reads", async () => {
    const { default: worker } = await import("./index");
    const request = new Request("http://localhost/lobbies");
    const ctx = createExecutionContext();
    const response = await worker.fetch!(request, env, ctx);
    await waitOnExecutionContext(ctx);

    expect(response.status).toBe(200);
    expect(response.headers.get("access-control-allow-origin")).toBe("*");
  });

  it("POST /lobby/:code/join 404 still allows cross-origin reads (client shows 'gone', not a network error)", async () => {
    const { default: worker } = await import("./index");
    const request = new Request("http://localhost/lobby/NOPE/join", { method: "POST" });
    const ctx = createExecutionContext();
    const response = await worker.fetch!(request, env, ctx);
    await waitOnExecutionContext(ctx);

    expect(response.status).toBe(404);
    expect(response.headers.get("access-control-allow-origin")).toBe("*");
  });

  it("OPTIONS preflight returns 204 with the allowed methods", async () => {
    const { default: worker } = await import("./index");
    const request = new Request("http://localhost/lobby", { method: "OPTIONS" });
    const ctx = createExecutionContext();
    const response = await worker.fetch!(request, env, ctx);
    await waitOnExecutionContext(ctx);

    expect(response.status).toBe(204);
    expect(response.headers.get("access-control-allow-origin")).toBe("*");
    expect(response.headers.get("access-control-allow-methods")).toContain("POST");
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
