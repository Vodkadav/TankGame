export default {
  fetch(request: Request, _env: unknown, _ctx: ExecutionContext): Response {
    const url = new URL(request.url);
    if (request.method === "GET" && url.pathname === "/healthz") {
      return new Response("ok", { status: 200 });
    }
    return new Response("not found", { status: 404 });
  },
} satisfies ExportedHandler;
