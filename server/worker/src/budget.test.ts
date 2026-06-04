import { describe, it, expect } from "vitest";
import {
  overBudget,
  checkRequestBudget,
  monthStart,
  ALERT_THRESHOLD,
  DEFAULT_DO_REQUEST_BUDGET,
  type BudgetAnalytics,
  type BudgetAlerter,
} from "./budget";

function analytics(used: number): BudgetAnalytics {
  return { monthToDateRequests: () => Promise.resolve(used) };
}

function recorder(): BudgetAlerter & { messages: string[] } {
  const messages: string[] = [];
  return { messages, alert: (message) => messages.push(message) };
}

describe("request-budget alarm", () => {
  it("flags usage at or above the threshold", () => {
    const budget = 1000;
    expect(overBudget(800, budget)).toBe(true); // exactly 80%
    expect(overBudget(950, budget)).toBe(true);
    expect(overBudget(799, budget)).toBe(false);
  });

  it("never flags against a non-positive budget", () => {
    expect(overBudget(500, 0)).toBe(false);
  });

  it("uses an 80% threshold of the free-tier budget by default", () => {
    expect(ALERT_THRESHOLD).toBe(0.8);
    expect(overBudget(DEFAULT_DO_REQUEST_BUDGET * 0.8, DEFAULT_DO_REQUEST_BUDGET)).toBe(true);
    expect(overBudget(DEFAULT_DO_REQUEST_BUDGET * 0.79, DEFAULT_DO_REQUEST_BUDGET)).toBe(false);
  });

  it("alerts with the usage percentage once over budget", async () => {
    const alerter = recorder();

    const raised = await checkRequestBudget(analytics(850), alerter, 1000);

    expect(raised).toBe(true);
    expect(alerter.messages).toHaveLength(1);
    expect(alerter.messages[0]).toContain("85%");
    expect(alerter.messages[0]).toContain("850/1000");
  });

  it("stays silent below the threshold", async () => {
    const alerter = recorder();

    const raised = await checkRequestBudget(analytics(500), alerter, 1000);

    expect(raised).toBe(false);
    expect(alerter.messages).toHaveLength(0);
  });

  it("computes the UTC month-start window", () => {
    expect(monthStart(new Date("2026-06-04T12:34:56Z"))).toBe("2026-06-01");
    expect(monthStart(new Date("2026-12-31T23:59:59Z"))).toBe("2026-12-01");
  });
});
