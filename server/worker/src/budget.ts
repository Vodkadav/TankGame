// Request-budget alarm (M3-T11). Per ADR-0005 §4 and Decision 20 the match server stays on the
// Cloudflare free tier: a scheduled (cron) Worker reads Durable Object request usage and raises a
// Sentry alert at 80% of the monthly budget. The response to the ceiling is to refuse new lobbies,
// never to enable the paid plan. The decision logic here is pure and unit-tested against stubs; the
// live Cloudflare GraphQL Analytics client is thin wiring exercised only against the real account.

/** Month-to-date Durable Object request count. Stubbed in tests; the live impl queries CF GraphQL. */
export interface BudgetAnalytics {
  monthToDateRequests(): Promise<number>;
}

/** Sink for a budget alert. The live impl posts to Sentry; tests record the message. */
export interface BudgetAlerter {
  alert(message: string): void;
}

// Cloudflare's free-tier monthly Durable Object request inclusion. Overridable via env for headroom
// tuning; the alarm fires at THRESHOLD of whichever value is in force.
export const DEFAULT_DO_REQUEST_BUDGET = 1_000_000;
export const ALERT_THRESHOLD = 0.8;

/** True once usage reaches the alert threshold of the budget (a positive budget is required). */
export function overBudget(used: number, budget: number, threshold = ALERT_THRESHOLD): boolean {
  return budget > 0 && used / budget >= threshold;
}

/** Reads usage and alerts once it crosses the threshold. Returns whether an alert was raised. */
export async function checkRequestBudget(
  analytics: BudgetAnalytics,
  alerter: BudgetAlerter,
  budget = DEFAULT_DO_REQUEST_BUDGET,
): Promise<boolean> {
  const used = await analytics.monthToDateRequests();
  if (!overBudget(used, budget)) {
    return false;
  }

  const percent = Math.round((used / budget) * 100);
  alerter.alert(
    `Durable Object requests at ${percent}% of the monthly free-tier budget (${used}/${budget}). ` +
      `Refuse new lobbies before enabling the paid plan (ADR-0005 §4).`,
  );
  return true;
}

// Live Cloudflare GraphQL Analytics client (untested wiring — needs a real account token and DO
// traffic). Sums this month's Durable Object invocations for the account. A missing token/account
// reports zero so the scheduled run is a safe no-op locally rather than throwing.
export function cloudflareAnalytics(
  accountTag: string | undefined,
  apiToken: string | undefined,
  monthStartIso: string,
): BudgetAnalytics {
  return {
    async monthToDateRequests(): Promise<number> {
      if (!accountTag || !apiToken) {
        return 0;
      }

      const query = `query($account: String!, $since: String!) {
        viewer { accounts(filter: { accountTag: $account }) {
          durableObjectsInvocationsAdaptiveGroups(limit: 1, filter: { date_geq: $since }) {
            sum { requests }
          }
        } }
      }`;

      const response = await fetch("https://api.cloudflare.com/client/v4/graphql", {
        method: "POST",
        headers: { Authorization: `Bearer ${apiToken}`, "Content-Type": "application/json" },
        body: JSON.stringify({ query, variables: { account: accountTag, since: monthStartIso } }),
      });
      const body = (await response.json()) as {
        data?: { viewer?: { accounts?: { durableObjectsInvocationsAdaptiveGroups?: { sum?: { requests?: number } }[] }[] } };
      };
      const groups = body.data?.viewer?.accounts?.[0]?.durableObjectsInvocationsAdaptiveGroups ?? [];
      return groups.reduce((total, group) => total + (group.sum?.requests ?? 0), 0);
    },
  };
}

/** First day of the current UTC month as an ISO date (YYYY-MM-DD) — the analytics window start. */
export function monthStart(now: Date): string {
  const year = now.getUTCFullYear();
  const month = `${now.getUTCMonth() + 1}`.padStart(2, "0");
  return `${year}-${month}-01`;
}
