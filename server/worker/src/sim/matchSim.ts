// The authoritative match simulation (M3-T5, MVP). Pure and deterministic: it owns two tanks, a
// wall grid, and projectiles; clients send intent (InputFrame), the sim resolves outcome and emits
// snapshots. Mirrors the core of the client's GameLogic (movement, firing, projectile travel,
// brick break, tank damage) — enough to hit the M3 definition of done. Anti-cheat lives here: the
// server clamps move magnitude and fire rate, so a tampered client cannot move or shoot faster.
//
// Parity with the C# client is guarded by shared expectations rather than a generated schema (see
// ADR-0005 §3 and feature-roadmap §4); the sim is intentionally small for the MVP and grows as
// content lands.

import type { InputFrame, SnapshotFrame, TankState, WallDelta } from "../protocol/codec";
import { FIRE_BIT } from "../protocol/codec";
import {
  BRICK_HP,
  cellCentre,
  cellOf,
  type GameMap,
  MATERIAL_BRICK,
  MATERIAL_FLOOR,
  materialAt,
  parseMap,
  solidAtWorld,
} from "./map";

const TANK_SPEED = 200; // world units / second (server-authoritative; client only sends intent)
const FIRE_INTERVAL = 0.3; // min seconds between shots (the fire-rate clamp)
const PROJECTILE_SPEED = 600;
const TANK_MAX_HP = 3;
const COMBAT_RADIUS = 28; // a shot within this of an enemy tank hits it
const COLLISION_RADIUS = 24; // tank leading-edge for wall collision
const PROJECTILE_DAMAGE = 1;

// Slot spawn cells — mirror the client (P1 at the map '@', P2 at ArenaScene.Player2Spawn).
const GUEST_SPAWN_CELL = { x: 25, y: 7 };

interface SimTank {
  slot: number;
  x: number;
  y: number;
  rotation: number;
  turret: number;
  hp: number;
  team: number;
  fireCooldown: number;
  lastSeq: number;
  moveX: number;
  moveY: number;
  aim: number;
  fire: boolean;
}

interface SimProjectile {
  x: number;
  y: number;
  dirX: number;
  dirY: number;
  team: number;
}

export class MatchSim {
  readonly map: GameMap;
  tick = 0;
  private readonly tanks: SimTank[] = [];
  private projectiles: SimProjectile[] = [];
  private wallDeltas: WallDelta[] = [];

  constructor(
    map: GameMap = parseMap(),
    spawns: { host: { x: number; y: number }; guest: { x: number; y: number } } = {
      host: map.spawn,
      guest: GUEST_SPAWN_CELL,
    },
  ) {
    this.map = map;
    const host = cellCentre(spawns.host.x, spawns.host.y);
    this.tanks.push(this.spawnTank(0, host.x, host.y, 0));
    const guest = cellCentre(spawns.guest.x, spawns.guest.y);
    this.tanks.push(this.spawnTank(1, guest.x, guest.y, 1));
  }

  private spawnTank(slot: number, x: number, y: number, team: number): SimTank {
    return { slot, x, y, rotation: 0, turret: 0, hp: TANK_MAX_HP, team, fireCooldown: 0, lastSeq: 0, moveX: 0, moveY: 0, aim: 0, fire: false };
  }

  /** Records a client's latest intent for its slot, clamping the move magnitude (anti-cheat). */
  applyInput(slot: number, frame: InputFrame): void {
    const tank = this.tanks[slot];
    if (tank === undefined || frame.seq <= tank.lastSeq) {
      return; // unknown slot or a stale/duplicate frame
    }
    let { moveX, moveY } = frame;
    const magnitude = Math.hypot(moveX, moveY);
    if (magnitude > 1) {
      moveX /= magnitude; // a hacked client cannot exceed unit intent
      moveY /= magnitude;
    }
    tank.moveX = moveX;
    tank.moveY = moveY;
    tank.aim = frame.aim;
    tank.fire = (frame.buttons & FIRE_BIT) !== 0;
    tank.lastSeq = frame.seq;
  }

  /** Advances the whole match one fixed step. Accumulates wall changes for the next snapshot. */
  step(dt = 1 / 20): void {
    this.wallDeltas = [];
    for (const tank of this.tanks) {
      this.stepTank(tank, dt);
    }
    this.stepProjectiles(dt);
    this.tick++;
  }

  private stepTank(tank: SimTank, dt: number): void {
    if (tank.hp <= 0) {
      return; // a downed tank is inert (no respawn server-side in the MVP)
    }

    const speed = TANK_SPEED * dt;
    // Axis-separated movement so a tank slides along a wall instead of sticking (mirrors the client).
    if (tank.moveX !== 0) {
      const nextX = tank.x + tank.moveX * speed;
      if (!solidAtWorld(this.map, nextX + Math.sign(tank.moveX) * COLLISION_RADIUS, tank.y)) {
        tank.x = nextX;
      }
    }
    if (tank.moveY !== 0) {
      const nextY = tank.y + tank.moveY * speed;
      if (!solidAtWorld(this.map, tank.x, nextY + Math.sign(tank.moveY) * COLLISION_RADIUS)) {
        tank.y = nextY;
      }
    }
    if (tank.moveX !== 0 || tank.moveY !== 0) {
      tank.rotation = Math.atan2(tank.moveY, tank.moveX);
    }
    tank.turret = tank.aim;

    tank.fireCooldown -= dt;
    if (tank.fireCooldown <= 0 && tank.fire) {
      tank.fireCooldown = FIRE_INTERVAL; // the fire-rate clamp
      this.projectiles.push({ x: tank.x, y: tank.y, dirX: Math.cos(tank.turret), dirY: Math.sin(tank.turret), team: tank.team });
    }
  }

  private stepProjectiles(dt: number): void {
    const travel = PROJECTILE_SPEED * dt;
    const survivors: SimProjectile[] = [];

    for (const shot of this.projectiles) {
      const nextX = shot.x + shot.dirX * travel;
      const nextY = shot.y + shot.dirY * travel;
      const { cellX, cellY } = cellOf(nextX, nextY);
      const material = materialAt(this.map, cellX, cellY);

      if (material !== MATERIAL_FLOOR) {
        if (material === MATERIAL_BRICK) {
          this.damageBrick(cellX, cellY);
        }
        continue; // spent on the wall (steel stops it; brick is chipped/broken above)
      }

      const hitTank = this.tankHitAt(nextX, nextY, shot.team);
      if (hitTank !== undefined) {
        hitTank.hp = Math.max(0, hitTank.hp - PROJECTILE_DAMAGE);
        continue; // spent on the tank
      }

      shot.x = nextX;
      shot.y = nextY;
      survivors.push(shot);
    }

    this.projectiles = survivors;
  }

  private damageBrick(cellX: number, cellY: number): void {
    const index = cellY * this.map.width + cellX;
    const hp = Math.max(0, this.map.brickHp[index] - PROJECTILE_DAMAGE);
    this.map.brickHp[index] = hp;
    if (hp === 0) {
      this.map.materials[index] = MATERIAL_FLOOR; // broken through
    }
    this.wallDeltas.push({ cellX, cellY, material: this.map.materials[index], hp });
  }

  private tankHitAt(x: number, y: number, shooterTeam: number): SimTank | undefined {
    for (const tank of this.tanks) {
      if (tank.hp > 0 && tank.team !== shooterTeam && Math.hypot(tank.x - x, tank.y - y) <= COMBAT_RADIUS) {
        return tank;
      }
    }
    return undefined;
  }

  /** A snapshot for one recipient slot: the shared world plus that client's input ack. */
  snapshotFor(slot: number): SnapshotFrame {
    return {
      tick: this.tick,
      ackSeq: this.tanks[slot]?.lastSeq ?? 0,
      tanks: this.tanks.map(toTankState),
      wallDeltas: this.wallDeltas,
    };
  }

  // Read-only views for tests.
  tankAt(slot: number): TankState {
    return toTankState(this.tanks[slot]);
  }
  projectileCount(): number {
    return this.projectiles.length;
  }
  brickHpAt(cellX: number, cellY: number): number {
    return this.map.brickHp[cellY * this.map.width + cellX];
  }
}

function toTankState(tank: SimTank): TankState {
  return {
    slot: tank.slot,
    x: tank.x,
    y: tank.y,
    rotation: tank.rotation,
    turretRotation: tank.turret,
    hp: tank.hp,
    team: tank.team,
  };
}
