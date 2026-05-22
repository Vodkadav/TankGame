# TankGame

TankGame is a real-time multiplayer tank-combat game built with Godot 4 (C#) on the client, a Cloudflare Worker + Durable Objects backend, and Supabase for persistence — playable via Android APK with lobby codes you can share with friends.

## Repo map

```
client/
  src/
    Domain/          Pure C# interfaces and value objects — no Godot refs
    GameLogic/       Simulation logic implementing Domain interfaces
    Data/            Data-access implementations
    Infrastructure/  Platform adapters (input, networking, Sentry)
    Presentation/    Godot scenes and scripts
  tests/             All client-side tests (GoDotTest, NetArchTest, unit)
server/
  worker/            Cloudflare Worker — /healthz, lobby routes, Durable Objects
  supabase/          Supabase migration SQL and seed files
shared/
  protocol/          Serialisable message types mirrored in C# and TypeScript
docs/
  adr/               Architecture Decision Records (Nygard format)
  credits/           Third-party asset attribution
  licenses/          Full license texts for bundled dependencies
  research/          Planning and design documents
  setup/             One-time environment setup guides
scripts/             Developer tooling (Pester tests, hook installers)
.github/
  workflows/         GitHub Actions CI and deploy workflows
```
