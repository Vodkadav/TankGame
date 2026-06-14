// Wire protocol — the TypeScript mirror of the C# `TankGame.Domain.Net.ProtocolCodec`.
// Little-endian, fixed-layout binary; the two languages MUST produce identical bytes (guarded by
// the shared byte-vector test in codec.test.ts and ProtocolCodecTests.cs). See ADR-0005.

export interface InputFrame {
  seq: number;
  moveX: number;
  moveY: number;
  aim: number;
  buttons: number;
}

export interface TankState {
  slot: number;
  x: number;
  y: number;
  rotation: number;
  turretRotation: number;
  hp: number;
  team: number;
}

export interface WallDelta {
  cellX: number;
  cellY: number;
  material: number;
  hp: number;
}

// One live shot's state, so a guest can see shots in flight (ADR-0019 step 4). `rotation` is the
// travel heading (atan2(dir.x, dir.y)); `style` is 0 bullet / 1 missile; `layer` is the elevation
// layer the shot rides (ADR-0018).
export interface ProjectileState {
  x: number;
  y: number;
  rotation: number;
  style: number;
  layer: number;
}

export interface SnapshotFrame {
  tick: number;
  ackSeq: number;
  tanks: TankState[];
  wallDeltas: WallDelta[];
  projectiles: ProjectileState[];
}

export const INPUT_FRAME_SIZE = 17;
export const TANK_STATE_SIZE = 19;
export const WALL_DELTA_SIZE = 6;
export const PROJECTILE_STATE_SIZE = 14;
export const FIRE_BIT = 1 << 0;

// Every message is tagged with a leading kind byte: a Welcome (sent once on connect, carrying the
// player's assigned slot), a Snapshot, or an Input. Inputs are tagged too (ADR-0019 step 3) — the
// relay forwards guest bytes to the HOST CLIENT, whose one socket also carries the welcome, so each
// message must self-identify. The C# client mirrors these tags (TankGame.Domain.Net.ProtocolCodec)
// and the byte vectors in codec.test.ts are cross-language parity anchors.
export const MSG_WELCOME = 1;
export const MSG_SNAPSHOT = 2;
export const MSG_INPUT = 3;

/** A welcome message: `[MSG_WELCOME, slot]`. Tells a freshly-joined client which slot it controls. */
export function encodeWelcome(slot: number): Uint8Array {
  return new Uint8Array([MSG_WELCOME, slot & 0xff]);
}

/** An input message: `[MSG_INPUT, ...encodeInput(frame)]` — what a guest puts on the socket. */
export function encodeInputMessage(frame: InputFrame): Uint8Array {
  const payload = encodeInput(frame);
  const message = new Uint8Array(payload.length + 1);
  message[0] = MSG_INPUT;
  message.set(payload, 1);
  return message;
}

/** A snapshot message: `[MSG_SNAPSHOT, ...encodeSnapshot(frame)]`. */
export function encodeSnapshotMessage(frame: SnapshotFrame): Uint8Array {
  const payload = encodeSnapshot(frame);
  const message = new Uint8Array(payload.length + 1);
  message[0] = MSG_SNAPSHOT;
  message.set(payload, 1);
  return message;
}

export function encodeInput(frame: InputFrame): Uint8Array {
  const buffer = new Uint8Array(INPUT_FRAME_SIZE);
  const view = new DataView(buffer.buffer);
  view.setUint32(0, frame.seq, true);
  view.setFloat32(4, frame.moveX, true);
  view.setFloat32(8, frame.moveY, true);
  view.setFloat32(12, frame.aim, true);
  view.setUint8(16, frame.buttons);
  return buffer;
}

export function decodeInput(data: Uint8Array): InputFrame {
  const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
  return {
    seq: view.getUint32(0, true),
    moveX: view.getFloat32(4, true),
    moveY: view.getFloat32(8, true),
    aim: view.getFloat32(12, true),
    buttons: view.getUint8(16),
  };
}

export function encodeSnapshot(frame: SnapshotFrame): Uint8Array {
  const size =
    4 +
    4 +
    1 +
    frame.tanks.length * TANK_STATE_SIZE +
    2 +
    frame.wallDeltas.length * WALL_DELTA_SIZE +
    2 +
    frame.projectiles.length * PROJECTILE_STATE_SIZE;
  const buffer = new Uint8Array(size);
  const view = new DataView(buffer.buffer);
  let offset = 0;

  view.setUint32(offset, frame.tick, true);
  offset += 4;
  view.setUint32(offset, frame.ackSeq, true);
  offset += 4;

  view.setUint8(offset, frame.tanks.length);
  offset += 1;
  for (const tank of frame.tanks) {
    view.setUint8(offset, tank.slot);
    offset += 1;
    view.setFloat32(offset, tank.x, true);
    offset += 4;
    view.setFloat32(offset, tank.y, true);
    offset += 4;
    view.setFloat32(offset, tank.rotation, true);
    offset += 4;
    view.setFloat32(offset, tank.turretRotation, true);
    offset += 4;
    view.setUint8(offset, tank.hp);
    offset += 1;
    view.setUint8(offset, tank.team);
    offset += 1;
  }

  view.setUint16(offset, frame.wallDeltas.length, true);
  offset += 2;
  for (const wall of frame.wallDeltas) {
    view.setUint16(offset, wall.cellX, true);
    offset += 2;
    view.setUint16(offset, wall.cellY, true);
    offset += 2;
    view.setUint8(offset, wall.material);
    offset += 1;
    view.setUint8(offset, wall.hp);
    offset += 1;
  }

  view.setUint16(offset, frame.projectiles.length, true);
  offset += 2;
  for (const shot of frame.projectiles) {
    view.setFloat32(offset, shot.x, true);
    offset += 4;
    view.setFloat32(offset, shot.y, true);
    offset += 4;
    view.setFloat32(offset, shot.rotation, true);
    offset += 4;
    view.setUint8(offset, shot.style);
    offset += 1;
    view.setUint8(offset, shot.layer);
    offset += 1;
  }

  return buffer;
}

export function decodeSnapshot(data: Uint8Array): SnapshotFrame {
  const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
  let offset = 0;

  const tick = view.getUint32(offset, true);
  offset += 4;
  const ackSeq = view.getUint32(offset, true);
  offset += 4;

  const tankCount = view.getUint8(offset);
  offset += 1;
  const tanks: TankState[] = [];
  for (let i = 0; i < tankCount; i++) {
    const slot = view.getUint8(offset);
    offset += 1;
    const x = view.getFloat32(offset, true);
    offset += 4;
    const y = view.getFloat32(offset, true);
    offset += 4;
    const rotation = view.getFloat32(offset, true);
    offset += 4;
    const turretRotation = view.getFloat32(offset, true);
    offset += 4;
    const hp = view.getUint8(offset);
    offset += 1;
    const team = view.getUint8(offset);
    offset += 1;
    tanks.push({ slot, x, y, rotation, turretRotation, hp, team });
  }

  const wallCount = view.getUint16(offset, true);
  offset += 2;
  const wallDeltas: WallDelta[] = [];
  for (let i = 0; i < wallCount; i++) {
    const cellX = view.getUint16(offset, true);
    offset += 2;
    const cellY = view.getUint16(offset, true);
    offset += 2;
    const material = view.getUint8(offset);
    offset += 1;
    const hp = view.getUint8(offset);
    offset += 1;
    wallDeltas.push({ cellX, cellY, material, hp });
  }

  const projectileCount = view.getUint16(offset, true);
  offset += 2;
  const projectiles: ProjectileState[] = [];
  for (let i = 0; i < projectileCount; i++) {
    const x = view.getFloat32(offset, true);
    offset += 4;
    const y = view.getFloat32(offset, true);
    offset += 4;
    const rotation = view.getFloat32(offset, true);
    offset += 4;
    const style = view.getUint8(offset);
    offset += 1;
    const layer = view.getUint8(offset);
    offset += 1;
    projectiles.push({ x, y, rotation, style, layer });
  }

  return { tick, ackSeq, tanks, wallDeltas, projectiles };
}
