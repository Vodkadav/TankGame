import { env, createExecutionContext, waitOnExecutionContext } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import worker from "./index";

describe("Worker", () => {
  it("GET /healthz returns 200 with body 'ok'", async () => {
    const request = new Request("http://example.com/healthz", { method: "GET" });
    const ctx = createExecutionContext();
    const response = await worker.fetch(request, env, ctx);
    await waitOnExecutionContext(ctx);
    expect(response.status).toBe(200);
    expect(await response.text()).toBe("ok");
  });

  it("GET /unknown returns 404", async () => {
    const request = new Request("http://example.com/unknown", { method: "GET" });
    const ctx = createExecutionContext();
    const response = await worker.fetch(request, env, ctx);
    await waitOnExecutionContext(ctx);
    expect(response.status).toBe(404);
  });
});
