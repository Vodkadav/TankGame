# Progression & Unlockables — Design

Status: draft (Slice E research)
Constraint: **no money gating, ever.** No purchases, no ads, no battle pass, no rotating shop. Free-tier hosting only.

This is an opinionated design. The brief is "monetization-free as a constraint" — every choice below is filtered through that.

---

## 1. Earning model

**Two currencies. That's it.**

| Currency | How earned | What it unlocks |
|---|---|---|
| **XP (account-level)** | Awarded at end of every completed match. Base XP for participation + bonus for winning + small bonus per objective (kill, wall break, trap survived, assist). Capped per match so a 40-minute stomp doesn't pay 10× a fast match. | Account level. Account level gates **titles** and **emblems** (and a small number of skins tied to milestone levels). |
| **Mastery (per-weapon / per-map)** | Earned only by using that weapon (or playing that map). Small per-use grants, larger grants at "first kill with weapon", "first win on map", etc. | Cosmetic **skins** for that weapon, **nickname accents** themed to that weapon/map, and the **weapon's full unlock chain** (see §2). |

**Deliberately rejected:**

- Daily-login tokens / daily-challenge tokens. They create FOMO and punish irregular play. The two daughters who'll playtest this should not feel guilty for missing a day.
- A "premium" or "battle pass" currency, even a free one. The shape of a battle pass — a time-limited track you lose if you don't grind — is the FOMO problem itself, regardless of whether money is involved.
- Match-currency / coin shops. Adds a third economy that doesn't earn its complexity.

**Why two and not one:** XP alone makes every unlock feel like the same treadmill. Per-weapon mastery means a player who loves the rocket launcher gets rocket-themed rewards by playing the way they enjoy, while a generalist climbs account level. They are independent tracks, not converted between.

---

## 2. Unlockable taxonomy

### Skins (tank chassis, turret, tracks)

- **What it is:** Visual reskin of the player's tank. Body, turret, and tread can be mixed independently.
- **Cosmetic-only or gameplay-affecting:** Cosmetic-only. Same hitbox, same silhouette readability. **Hard rule:** no skin may make the tank harder to see against any map's background. Reviewer-gated.
- **Unlock path:** Mix of account-level milestones (level 5, 10, 20 …) and map-mastery rewards. A few skins drop from "first time you …" achievements.
- **Examples:**
  - *Desert Sand* — win 10 matches on the Sahara map.
  - *Forest Camo* — reach Forest map mastery level 3.
  - *Tournament Gold* — reach account level 25.
  - *Tread Lightly* (tread variant) — break 500 destructible walls lifetime.
  - *First Roll* — awarded at end of tutorial, so every player has at least one non-default skin within an hour.

### Weapons — **the hard problem**

- **What it is:** Different primary weapons (e.g. cannon, rocket launcher, shotgun-shell, ricochet round, mortar, laser).
- **Cosmetic-only or gameplay-affecting:** **Gameplay-affecting.** This is the pay-to-win-feeling risk even though nothing is paid. Three mitigations, applied together:

  1. **Sidegrades, never upgrades.** Every weapon is balanced to be roughly equally viable. No "tier 1 vs tier 5". Rocket trades fire rate for splash; shotgun trades range for close-range burst; ricochet trades raw damage for wall play. A starter cannon must remain a competitive choice at every level — if it stops being viable, the design is broken and gets rebalanced, not power-crept.
  2. **All weapons unlockable inside the first ~3 hours of play.** Not 30 hours. New players quickly have access to the full sandbox; what they keep unlocking after is *skins and accents for those weapons*, not new power.
  3. **Ranked / competitive mode uses a fixed weapon pool** that everyone has unlocked from match 1. Unlock progression only changes the *casual / quickplay* loadout space. This eliminates the "I lost because they had the unlocked weapon" complaint at the competitive tier entirely.

- **Unlock path:** Weapons unlock at account level milestones (every 2–3 levels, so 5–6 weapons across the first 3 hours). Per-weapon **mastery** then unlocks skins and accents for that weapon, *not* additional power.
- **Examples:**
  - *Standard Cannon* — starter, always available.
  - *Ricochet Round* — account level 3 (~20 min in).
  - *Mortar* — account level 6 (~1 hour in).
  - *Rocket Launcher* — account level 10 (~2 hours in).
  - *Laser* — account level 14 (~3 hours in) — last weapon, completes the sandbox.

### Emblems

- **What it is:** A small badge displayed next to the player's name in scoreboards and on the tank's chassis.
- **Cosmetic-only or gameplay-affecting:** Cosmetic-only.
- **Unlock path:** Account-level milestones + achievement-style "did a hard thing once" unlocks. Emblems are the natural home for bragging-rights items (because they're seen by other players).
- **Examples:**
  - *Bronze Tread* — account level 5.
  - *Wallbreaker* — destroy 1,000 walls lifetime.
  - *Trap Whisperer* — survive 50 traps without taking damage.
  - *Comeback Kid* — win a match after being last on the scoreboard at the halfway point, 10 times.
  - *Daughter-Approved* — beat the dev's high score on the tutorial gauntlet. (Easter egg — keep one or two of these.)

### Nickname accents

- **What it is:** A visual flourish applied to the player's displayed name — colour gradient, a leading/trailing glyph, a font weight, an animated shimmer for a few rare ones. **Not** the ability to use special Unicode that breaks readability for other players.
- **Cosmetic-only or gameplay-affecting:** Cosmetic-only. Readability constraint: name must remain legible at scoreboard size. Allowed accents are a curated palette, not free-form.
- **Unlock path:** Per-weapon mastery and per-map mastery. Each weapon and each map has one or two themed accents. Players who deep-specialise in one weapon get a visible signature.
- **Examples:**
  - *Rocket trail* (orange-red gradient) — Rocket Launcher mastery 5.
  - *Sahara dust* (sand-coloured shimmer) — Sahara map mastery 5.
  - *Ricochet stripes* (zig-zag underline) — Ricochet Round mastery 3.
  - *Veteran underline* — account level 50.

### Titles

- **What it is:** A short phrase displayed under the player's name on the scoreboard and pre-match lobby. Examples: "Wallbreaker", "Map Maker", "Last Tank Rolling".
- **Cosmetic-only or gameplay-affecting:** Cosmetic-only.
- **Unlock path:** Account-level milestones and major achievements. Titles are equippable (player picks one to display at a time) — a player accumulates a wardrobe over time.
- **Examples:**
  - *Recruit* — awarded after tutorial.
  - *Sharpshooter* — 100 kills from beyond half-map distance.
  - *Map Maker* — play every map at least 10 times.
  - *Survivor* — win 5 matches without dying once.
  - *The Old Guard* — account level 100. (A long-term goal, see §5.)

---

## 3. Onboarding & "first 3 hours"

The single biggest failure mode is a new player finishing a match and getting **nothing**. Below is the actual experience.

**Tutorial (first ~5 min):**

- Auto-grant: *Recruit* title, *First Roll* tread skin, default *Standard Cannon*.
- The player exits the tutorial already looking different from the literal-default tank in the lobby. This is the cheap, high-impact onboarding win.

**First match (5–15 min in):**

- Visible XP bar fills in real time during the match (kills, wall breaks, etc.).
- Level-up at end of match: account level 2. Confetti screen. Unlock a chassis skin variant.

**First hour (~3–4 matches in):**

- Account level 3–4. Unlocks: *Ricochet Round* weapon (level 3), *Bronze Tread* emblem (level 5 — pushes them to keep playing one more match), 1–2 skin variants.
- First per-weapon mastery rank hits, granting a nickname accent.

**First 3 hours (~10–12 matches in):**

- Account level 10–14. All weapons unlocked. Player has experienced the full sandbox.
- Mastery rank 2–3 on their favourite weapon — a visible nickname accent of their own.
- 2–3 emblems, 1 unlocked title beyond *Recruit*.
- Several skin combinations available — they look distinct from any other player they encounter.

The goal: by the end of session one, the player has visibly customised their tank and earned at least one thing they're proud of. By the end of session three (assumed ~3 hours), they have *the whole gameplay sandbox* and the rest is depth.

---

## 4. Anti-exploit

Honest take per vector — and where we accept the risk on a hobby budget.

| Exploit | Realistic mitigation | What we accept |
|---|---|---|
| **Smurfing** (high-skill player on alt account stomps newbies) | Skill-based matchmaking on the ranked queue, K-factor that adjusts quickly so a smurf rises out of the new-player bracket within ~10 matches. No matchmaking on the casual queue — let it be chaotic. | Casual smurfing happens. We don't try to stop it; we make ranked work so it doesn't poison the competitive ladder. |
| **AFK farming** (idle in match to collect participation XP) | Participation XP requires *some* activity threshold — fired a shot, moved a tile, took damage. Players reported as AFK by majority of match get zero XP for that match. Server-side check, not client-side. | A player who plays badly but actively still earns XP. That's fine — they played. |
| **Win-trading** (two friends queue together, one throws to feed the other) | Detection heuristic: same two account IDs appear in N matches together with extreme score imbalance + short match length. Flag for review, not auto-ban. Ranked has solo queue only (or duo-queue with stricter MMR matching) to make win-trading much harder. | Low-volume hobby game — actual detection cost has to stay tiny. A queue policy that makes it inconvenient is most of the win. |
| **Bot farming** (script plays matches for XP overnight) | Per-account daily XP soft cap. Above the cap, XP scales down to 25%. A grinder still earns; a 24/7 bot doesn't 10× them. Combined with AFK detection. | Sophisticated bots that actually play the game well are essentially impossible to stop on a hobby budget. The daily cap removes the *incentive* (you can't out-grind it). |
| **Alt-account abuse** (create new accounts to re-claim onboarding rewards, dodge ranked decay, etc.) | Free-to-play, no real-money attached, so alts are easy. Mitigations: ranked uses a phone-or-OAuth-verified account tier (anonymous accounts can play casual but not ranked); onboarding rewards are not transferable; account creation rate-limited per IP. | Casual queue alt-accounts are unavoidable. We let them exist. The verified-account requirement for ranked is the actual lever. |

**The general principle:** server-authoritative XP awards, daily soft cap on earnings, ranked queue carries the strict rules, casual queue carries the loose rules. Don't spend the hobby budget chasing exploits that affect only the casual experience.

---

## 5. Long-tail engagement without FOMO

The player who's been around 6 months needs something to do *without* needing daily logins or fear of missing limited content.

**Mastery levels — uncapped tail.** Per-weapon and per-map mastery go to rank 10 (cosmetics granted along the way), then continue uncapped with no further rewards except a numeric rank shown to other players. A player who has spent 6 months grinding ricochet-round mastery has a visible rank-43 marker. The *number itself* is the reward — bragging rights, no FOMO.

**Prestige (account level 100).** At level 100 the player can opt-in to prestige: reset account level to 1 in exchange for a permanent prestige emblem. Prestige is purely cosmetic and *optional*. The player keeps all unlocks. Each prestige tier is visually distinct.

**Community challenges.** Periodic (every 1–2 months, not weekly) server-wide challenges: "the community collectively breaks 1 million walls this month". When hit, everyone who participated gets a one-off emblem. No individual gate, no leaderboard — it's pure collective. Easy to skip without losing anything.

**Player-made map curation.** If/when user-generated maps exist, a "featured map of the month" rotation gives the long-tail player something fresh to learn without us shipping new content. Map mastery progression applies normally.

**Friend-group competition.** Leaderboards filtered to "your friends" — the most durable long-tail engagement loop in any game is "did I beat my friend this week". Costs us almost nothing to implement once basic leaderboards exist.

**What's not on the list, on purpose:** daily quests, weekly resets, rotating cosmetic shops, limited-time skins, seasonal passes. All of those are FOMO mechanics in disguise.

---

## 6. Monetization-free durability

**Why this can stay free, structurally:**

- Realtime matches are short (5–15 min), so bandwidth and compute scale linearly with concurrent players, not total players. Free-tier hosts (see Slice D) can sustain tens to low-hundreds of concurrent matches.
- Cosmetic generation is one-time art cost, not ongoing. New cosmetics ship when the dev makes them — there's no quota.
- No live-ops team. No event team. No store. The game runs itself between releases.

**Operating cost ceiling (rough order-of-magnitude — to be confirmed in Slice D):**

- Static hosting: free indefinitely (Cloudflare Pages / GitHub Pages).
- Realtime backend: free tier on Fly.io / Hathora / Colyseus Arena (estimated ~tens of concurrent matches).
- Database / progression storage: free tier on Supabase / Firebase (estimated ~thousands of accounts).
- Practical ceiling: ~50–100 concurrent players before any free tier starts being a problem. If we exceed that, we have a happy problem.

**Levers to pull BEFORE introducing payments — in order:**

1. **Capacity caps.** Match queue waits when the server is full. Honest about the constraint; no one is harmed.
2. **Aggressive optimisation.** Smaller match sizes, lower tick rate on non-ranked, regional sharding. Engineering effort, not money.
3. **Voluntary tip jar.** A "support the server" link in the credits screen. No in-game benefit for tippers — explicitly. Ko-fi / GitHub Sponsors / Liberapay. **This is the bright line.** No matter how much someone tips, they get zero gameplay or cosmetic advantage. Optionally a "supporter" emblem tied to *any* tip amount (so tippers feel seen without it becoming a price tier).
4. **Open source the server.** Players who want their own instance can run one. Federated model — the central server is just one of many.
5. **Donation drives for specific costs.** "Server bill this month is $X, we've raised $Y." Transparent.
6. **Capacity-share community model.** Players with spare cycles can volunteer to host regional match servers (Nakama / Colyseus support this pattern).

**Things we will never do, even at scale:**

- Sell cosmetics, weapons, or any unlockable.
- Sell XP boosts, queue-skip, or "supporter perks" with gameplay effect.
- Run ads, including "rewarded video" ads.
- Sell player data.
- Time-gated rotating shops, even if items are earned with in-game currency only — the rotation creates artificial scarcity which is the FOMO problem in another costume.

The design assumption is that this is a hobby project for the dev and his daughters. If it ever needs to scale beyond what donations cover, the right answer is to cap it, not to monetise it.

---

## Open questions for user decision

1. **Ranked mode in MVP?** §2 (weapons) and §4 (anti-exploit) lean heavily on "ranked queue with fixed weapon pool" to defuse pay-to-win-feeling and to be the strict-rules surface. If MVP is casual-only, the weapon balance has to be even tighter because there's no fallback queue. *Recommendation: ranked is M3+, casual queue first — but design weapons as if ranked exists.*
2. **Verified accounts (phone / OAuth) for ranked?** Mentioned in §4 as the lever against alt-account abuse on the competitive ladder. Adds friction. Acceptable?
3. **Prestige at level 100, or higher?** §5 picks 100 somewhat arbitrarily. Could be 50 (sooner gratification) or 200 (rarer flex). Depends on how long a level takes — TBD until we tune XP curves with playtest data.
4. **Community challenges cadence.** §5 suggests every 1–2 months. The dev's available time will determine whether this is realistic.
5. **Tip jar from day one, or only if costs become a problem?** §6 lever #3 — could be there from launch as "support the project" without any framing of need.
