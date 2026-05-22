# Cloudflare Setup Guide

Status: setup guide (one-time, per developer)
Date: 2026-05-21
Audience: project owner setting up Cloudflare for the first time

This guide takes you from "no Cloudflare account" to "GitHub Actions can deploy our Pages site and Worker on push to main." You only do this once.

## What we are setting up and why

Per `docs/research/decisions.md` and `multiplayer-hosting.md`, our entire backend stack lives on Cloudflare's free tier:

| Component | Service | Used for |
|---|---|---|
| Static client (Godot HTML5 bundle) | **Cloudflare Pages** | Hosts the game URL players visit |
| Match server (one per lobby) | **Cloudflare Workers + Durable Objects** | WebSocket game server, hibernates between ticks |
| Lobby code → Durable Object mapping | **Workers KV** | Resolves 6-char lobby codes to a specific match |
| Large asset files (>25 MiB) — later | **Cloudflare R2** | Asset CDN, free egress within CF |

You need: a Cloudflare account, an API token with the right scopes, your Account ID, and (later) a registered Pages project + Worker. The GitHub Actions workflow will use these to deploy on every push.

**Time to complete:** ~15 minutes, no payment required.

---

## Step 1 — Create the Cloudflare account

1. Go to <https://dash.cloudflare.com/sign-up>.
2. Sign up with the email you want associated with this project (your normal email is fine — no separate email needed for the free tier).
3. Verify the email.
4. **Do not add a domain yet** — the signup flow nudges you to add one. Skip / dismiss it. We'll use `<project>.pages.dev` until M4 per the plan.

When you land in the dashboard, you should see a sidebar with "Workers & Pages", "R2", "KV", etc.

---

## Step 2 — Note your Account ID

The Account ID is a hex string Cloudflare uses to identify your account in API calls.

1. In the dashboard, click **Workers & Pages** in the left sidebar.
2. On the right side of that page, under "Account details", you will see **Account ID** with a copy button.
3. Copy it. It looks like `abcd1234ef567890...` (32 hex chars).

**Save this value** — we'll add it as `CF_ACCOUNT_ID` in GitHub Actions secrets later.

---

## Step 3 — Create the Pages project (placeholder, no real content yet)

We create the Pages project up front so the deploy workflow has somewhere to deploy. The project will be empty until M0-T6 lands the first build.

1. In the dashboard, go to **Workers & Pages** → click **Create application** → tab **Pages** → **Create using direct upload**.
2. **Project name:** `tankgame` (this is what becomes `tankgame.pages.dev`). If the name is taken, try `tankgame-vd` or similar — note what you choose, we'll store it as a secret.
3. **Production branch:** leave default (`main`) for now.
4. Click **Create project**.
5. Upload literally any tiny placeholder (e.g. a zip containing one `index.html` with the text "soon"). This is throwaway — M0-T6 replaces it.

After creation you can visit `https://tankgame.pages.dev` (or whatever you named it) and see the placeholder. Good — the URL is live.

**Save the project name** as `CF_PAGES_PROJECT`.

> Why "direct upload" not "connect Git"? Because we want GitHub Actions in our own repo to drive the deploy — that gives us preview deploys on PRs, the smoke-test step, and lets us hold all secrets in one place. Connecting Pages to GitHub directly works, but it bypasses our CI gate and gives Cloudflare access tokens to the whole repo. Direct-upload via API is the cleaner path.

---

## Step 4 — Create the Worker (placeholder)

Same idea — register the Worker name so the deploy workflow can target it.

1. Dashboard → **Workers & Pages** → **Create application** → tab **Workers** → **Create Worker**.
2. **Name:** `tankgame-worker` (becomes `tankgame-worker.<your-subdomain>.workers.dev`). If taken, pick a variant and note it.
3. Accept the default starter code; click **Deploy**.
4. After deploy, visit the URL Cloudflare shows you. You should see "Hello World" or similar. Good — the Worker exists.

The first time you create a Worker, Cloudflare assigns you a **workers.dev subdomain** (e.g. `vodkadav.workers.dev`). It is shown on the Workers overview page. Take note of it; the Worker URL becomes `tankgame-worker.<that-subdomain>.workers.dev`.

> M3 onwards we will need **Durable Objects** enabled and a **KV namespace** created. Free tier covers both, but DOs are enabled per-script in `wrangler.toml`, not via the dashboard — M3 tickets handle that. You don't need to do anything DO-related now.

---

## Step 5 — Generate the API token (GitHub Actions will use this)

This is the security-critical step. The token is the credential GitHub Actions presents when deploying.

1. Go to <https://dash.cloudflare.com/profile/api-tokens>.
2. Click **Create Token**.
3. At the bottom, choose **Create Custom Token** (do NOT use the "Edit Cloudflare Workers" preset — its scope is wider than we need).
4. Fill in:
   - **Token name:** `tankgame-github-actions`
   - **Permissions** (add each row — click "Add more" between them):
     - `Account` → `Cloudflare Pages` → **Edit**
     - `Account` → `Workers Scripts` → **Edit**
     - `Account` → `Workers KV Storage` → **Edit**
     - `Account` → `Account Settings` → **Read** (Wrangler needs this to look up the account)
     - `User` → `User Details` → **Read** (Wrangler verifies token validity with this)
   - **Account Resources:** restrict to **Include → your specific account** (the one named after your signup email)
   - **Zone Resources:** leave as `All zones` (we don't have a custom domain yet; this becomes relevant at M4)
   - **TTL:** leave **Never expires** (you can revoke manually if compromised; rotating annually is good hygiene)
5. Click **Continue to summary** → **Create Token**.
6. **Copy the token immediately** — Cloudflare shows it once. If you lose it, you create a new one.

**Save the token** as `CF_API_TOKEN`. Treat it like a password — never commit it, never paste it in chat, never share it.

> Why custom token, not the preset? The "Edit Cloudflare Workers" preset includes some scopes we don't need (e.g. `Workers Tail`) and is missing **Cloudflare Pages: Edit**, which we DO need. Custom is more secure and more functional.

---

## Step 6 — Verify the token works

Before storing the token in GitHub, sanity-check it locally.

Open PowerShell:

```powershell
$env:CF_API_TOKEN = "paste-the-token-here-temporarily"
curl.exe -H "Authorization: Bearer $env:CF_API_TOKEN" https://api.cloudflare.com/client/v4/user/tokens/verify
```

Expected response:

```json
{
  "result": { "id": "...", "status": "active" },
  "success": true,
  "errors": [],
  "messages": [{"code":10000,"message":"This API Token is valid and active"}]
}
```

If you see `success: true`, the token is good. **Then clear it from your shell:**

```powershell
$env:CF_API_TOKEN = $null
```

(So it doesn't sit in your shell history. We will only ever paste it into GitHub Actions secrets from here on.)

---

## Step 7 — Add the secrets to GitHub

These are the values M0-T6 (the deploy workflow) needs to reference.

1. Go to <https://github.com/Vodkadav/TankGame/settings/secrets/actions>.
2. For each of the values below, click **New repository secret**, paste the value, save:

| Secret name | Value | Source |
|---|---|---|
| `CF_API_TOKEN` | the token from Step 5 | Cloudflare API tokens page |
| `CF_ACCOUNT_ID` | your 32-char hex Account ID | Step 2 |
| `CF_PAGES_PROJECT` | the Pages project slug, e.g. `tankgame` | Step 3 |

These are now available to workflows as `${{ secrets.CF_API_TOKEN }}`, `${{ secrets.CF_ACCOUNT_ID }}`, `${{ secrets.CF_PAGES_PROJECT }}`. Never echoed in logs (GitHub masks them automatically).

---

## Step 8 — Confirm the Workers subdomain in wrangler.toml

When M0-T2 lands, the Worker's `wrangler.toml` will look something like:

```toml
name = "tankgame-worker"
main = "src/index.ts"
compatibility_date = "2026-05-21"
```

You don't need to touch this — `wrangler deploy` (in the GitHub Actions workflow) picks up your account context from `CF_API_TOKEN` and `CF_ACCOUNT_ID`. The Worker name in `wrangler.toml` must match what you registered in Step 4.

---

## What we are NOT doing now (and when we will)

| Task | When | Notes |
|---|---|---|
| Custom domain (e.g. `tankgame.example`) | M4 | Costs a domain registration (~$10/yr at Cloudflare Registrar) but is free hosting once added. We use `<project>.pages.dev` until then. |
| Durable Objects bindings | M3 | Added to `wrangler.toml` by M3-T3. Free tier covers it. |
| Workers KV namespace | M3 | Created via `wrangler kv namespace create` in the M3 deploy workflow. Free tier. |
| Cloudflare R2 bucket | Only if a single asset exceeds 25 MiB | Likely never for MVP. |
| Workers Paid plan | **Never** per Decision 20 (hard rate-limit to stay free) | If quota is hit, we rate-limit, not pay. Federation is the long-term lever. |

---

## What to send back when this is done

Once Steps 1–7 are complete, tell me:
1. The Cloudflare Pages project slug you used (e.g. `tankgame` or `tankgame-vd`).
2. The Worker name you used (e.g. `tankgame-worker`).
3. Your workers.dev subdomain (e.g. `vodkadav`) so I can put the placeholder URL in the M0 README.
4. **Confirmation** that `CF_API_TOKEN`, `CF_ACCOUNT_ID`, `CF_PAGES_PROJECT` are all set as repository secrets in `Vodkadav/TankGame`.

I do not need to see the secret values — just the confirmation. After that, M0-T6 (the deploy workflow) can be dispatched with everything wired correctly on the first try.

---

## Troubleshooting

**"Free trial expired" banner in dashboard.** Cloudflare sometimes shows trial banners for paid features (Workers Unbound, Pages Functions on paid tier). Ignore — we are on the free tier of the regular Workers + Pages products, which has no trial.

**Worker creation refused with "subdomain not set".** Cloudflare may prompt you to claim your `*.workers.dev` subdomain first. Go to Workers & Pages → click the profile-style chip in the top-right → **Set up your workers.dev subdomain**. Pick anything. Then retry.

**Token verification returns `success: false`.** Most common cause: the token was created without `User → User Details: Read`. Re-create the token with that permission — Wrangler refuses to start without it.

**`wrangler deploy` in CI fails with "Could not infer account ID".** The Worker exists but the token's account scope was set to "All accounts" — change it to the specific account in the token settings.
