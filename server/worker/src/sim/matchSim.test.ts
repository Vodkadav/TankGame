import { describe, it, expect } from "vitest";
import { MatchSim } from "./matchSim";
import { FIRE_BIT } from "../protocol/codec";
import { parseMap } from "./map";

function input(seq: number, moveX: number, moveY: number, aim: number, fire: boolean) {
  return { seq, moveX, moveY, aim, buttons: fire ? FIRE_BIT : 0 };
}

describe("MatchSim", () => {
  it("spawns two tanks on opposing teams", () => {
    const sim = new MatchSim();
    expect(sim.tankAt(0).team).toBe(0);
    expect(sim.tankAt(0).hp).toBe(3);
    expect(sim.tankAt(1).team).toBe(1);
  });

  it("moves a tank along its input", () => {
    const sim = new MatchSim();
    const startX = sim.tankAt(0).x;

    sim.applyInput(0, input(1, 1, 0, 0, false));
    sim.step(); // 200 u/s * (1/20) = 10 units east

    expect(sim.tankAt(0).x).toBeCloseTo(startX + 10, 3);
  });

  it("clamps an over-unit move vector so a hacked client cannot move faster", () => {
    const honest = new MatchSim();
    honest.applyInput(0, input(1, 1, 0, 0, false));
    honest.step();

    const cheat = new MatchSim();
    cheat.applyInput(0, input(1, 100, 0, 0, false)); // magnitude 100
    cheat.step();

    expect(cheat.tankAt(0).x).toBeCloseTo(honest.tankAt(0).x, 3);
  });

  it("ignores a stale input sequence", () => {
    const sim = new MatchSim();
    sim.applyInput(0, input(5, 1, 0, 0, false));
    sim.applyInput(0, input(3, -1, 0, 0, false)); // older seq — dropped
    sim.step();

    expect(sim.tankAt(0).x).toBeGreaterThan(160); // moved east per seq 5, not west
  });

  it("stops a tank at a wall instead of driving through it", () => {
    const sim = new MatchSim();
    sim.applyInput(0, input(1, -1, 0, 0, false)); // drive west into the steel border
    for (let i = 0; i < 40; i++) {
      sim.step();
    }
    const x = sim.tankAt(0).x;
    expect(x).toBeGreaterThanOrEqual(88); // leading edge cannot enter the border (cell 0)
    expect(x).toBeLessThan(160); // but it did move toward the wall
  });

  it("fires on the turret aim, rate-limited", () => {
    const sim = new MatchSim();
    sim.applyInput(0, input(1, 0, 0, 0, true)); // hold fire, aim +X

    sim.step(); // fires shot 1
    sim.step(); // cooldown not elapsed — no shot
    sim.step();
    // 3 ticks at 1/20 = 0.15s < the 0.3s interval, so exactly one shot exists (still travelling)
    expect(sim.projectileCount()).toBe(1);
  });

  it("breaks a brick the shots hit, emitting wall deltas", () => {
    // Small arena: host at (1,1), a brick straight east at (3,1).
    const map = parseMap(["#####", "#@.x#", "#####"].join("\n"));
    const sim = new MatchSim(map, { host: { x: 1, y: 1 }, guest: { x: 0, y: 0 } });
    sim.applyInput(0, input(1, 0, 0, 0, true)); // aim +X, hold fire

    const seenDeltas = [];
    for (let i = 0; i < 40; i++) {
      sim.step();
      seenDeltas.push(...sim.snapshotFor(0).wallDeltas);
    }

    expect(sim.brickHpAt(3, 1)).toBe(0); // broken through after enough hits
    expect(seenDeltas.some((d) => d.cellX === 3 && d.cellY === 1 && d.material === 0)).toBe(true);
  });

  it("damages an enemy tank a shot reaches", () => {
    const map = parseMap(["######", "#@...#", "######"].join("\n"));
    // Guest sits east of the host, same row; the host's +X shot reaches it.
    const sim = new MatchSim(map, { host: { x: 1, y: 1 }, guest: { x: 4, y: 1 } });
    sim.applyInput(0, input(1, 0, 0, 0, true)); // aim +X, hold fire

    for (let i = 0; i < 12; i++) {
      sim.step();
    }

    expect(sim.tankAt(1).hp).toBe(2); // one shot landed on the enemy
  });

  it("snapshots each client's own input ack", () => {
    const sim = new MatchSim();
    sim.applyInput(0, input(9, 0, 0, 0, false));
    sim.applyInput(1, input(4, 0, 0, 0, false));
    sim.step();

    expect(sim.snapshotFor(0).ackSeq).toBe(9);
    expect(sim.snapshotFor(1).ackSeq).toBe(4);
    expect(sim.snapshotFor(0).tick).toBe(1);
  });
});
