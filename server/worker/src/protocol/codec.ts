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

export interface SnapshotFrame {
  tick: number;
  ackSeq: number;
  tanks: TankState[];
  wallDeltas: WallDelta[];
}

export const INPUT_FRAME_SIZE = 17;
export const TANK_STATE_SIZE = 19;
export const WALL_DELTA_SIZE = 6;
export const FIRE_BIT = 1 << 0;

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
    4 + 4 + 1 + frame.tanks.length * TANK_STATE_SIZE + 2 + frame.wallDeltas.length * WALL_DELTA_SIZE;
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

  return { tick, ackSeq, tanks, wallDeltas };
}
