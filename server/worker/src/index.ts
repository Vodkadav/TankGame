export default {
  fetch(request: Request, _env: unknown, _ctx: ExecutionContext): Response {
    return new Response("not found", { status: 404 });
  },
} satisfies ExportedHandler;
