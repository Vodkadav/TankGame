import { createExecutionContext, waitOnExecutionContext, env } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import worker from "./index";

describe("Worker", () => {
  it("GET /healthz returns 200 ok", async () => {
    const request = new Request("http://localhost/healthz");
    const ctx = createExecutionContext();
    const response = await worker.fetch(request, env, ctx);
    await waitOnExecutionContext(ctx);

    expect(response.status).toBe(200);
    expect(await response.text()).toBe("ok");
  });

  it("unknown path returns 404", async () => {
    const request = new Request("http://localhost/unknown-path");
    const ctx = createExecutionContext();
    const response = await worker.fetch(request, env, ctx);
    await waitOnExecutionContext(ctx);

    expect(response.status).toBe(404);
  });
});
