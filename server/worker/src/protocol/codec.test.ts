import { describe, it, expect } from "vitest";
import {
  encodeInput,
  decodeInput,
  encodeSnapshot,
  decodeSnapshot,
  encodeWelcome,
  encodeSnapshotMessage,
  encodeInputMessage,
  MSG_WELCOME,
  MSG_SNAPSHOT,
  MSG_INPUT,
  FIRE_BIT,
  type SnapshotFrame,
} from "./codec";

describe("protocol codec", () => {
  it("round-trips an InputFrame", () => {
    const original = { seq: 42, moveX: 0.25, moveY: -0.5, aim: 1.5, buttons: FIRE_BIT };
    expect(decodeInput(encodeInput(original))).toEqual(original);
  });

  it("round-trips a SnapshotFrame with tanks and wall deltas", () => {
    const original: SnapshotFrame = {
      tick: 100,
      ackSeq: 41,
      tanks: [
        { slot: 0, x: 64, y: 128, rotation: 0, turretRotation: 0.5, hp: 3, team: 0 },
        { slot: 1, x: 200, y: 96, rotation: 0.25, turretRotation: -1, hp: 2, team: 1 },
      ],
      wallDeltas: [
        { cellX: 5, cellY: 6, material: 1, hp: 2 },
        { cellX: 12, cellY: 0, material: 0, hp: 0 },
      ],
    };
    expect(decodeSnapshot(encodeSnapshot(original))).toEqual(original);
  });

  it("round-trips an empty snapshot", () => {
    const original: SnapshotFrame = { tick: 7, ackSeq: 7, tanks: [], wallDeltas: [] };
    expect(decodeSnapshot(encodeSnapshot(original))).toEqual(original);
  });

  // Cross-language parity anchor — these exact bytes are asserted byte-for-byte by the C# codec
  // (client/tests/Domain/ProtocolCodecTests.cs). If either side changes the layout, both fail.
  it("encodes a Welcome to the canonical byte vector", () => {
    // Parity anchor — ProtocolCodecTests.cs asserts the identical [MSG_WELCOME, slot] bytes.
    expect([...encodeWelcome(1)]).toEqual([MSG_WELCOME, 0x01]);
    expect([...encodeWelcome(0)]).toEqual([MSG_WELCOME, 0x00]);
  });

  it("tags a snapshot message with the snapshot kind byte", () => {
    const frame: SnapshotFrame = { tick: 7, ackSeq: 7, tanks: [], wallDeltas: [] };
    const message = encodeSnapshotMessage(frame);
    expect(message[0]).toBe(MSG_SNAPSHOT);
    expect(decodeSnapshot(message.subarray(1))).toEqual(frame);
  });

  // ADR-0019 step 3: inputs gained a kind tag — the relay forwards guest bytes to the host CLIENT,
  // whose one socket also carries the welcome, so every message must self-identify.
  it("encodes an input message to the tagged canonical byte vector", () => {
    const bytes = [...encodeInputMessage({ seq: 1, moveX: 1, moveY: -1, aim: 0.5, buttons: 1 })];
    expect(bytes).toEqual([
      MSG_INPUT, // 0x03
      0x01, 0x00, 0x00, 0x00, // seq = 1
      0x00, 0x00, 0x80, 0x3f, // moveX = 1.0
      0x00, 0x00, 0x80, 0xbf, // moveY = -1.0
      0x00, 0x00, 0x00, 0x3f, // aim = 0.5
      0x01, // buttons = fire
    ]);
  });

  it("encodes an InputFrame to the canonical byte vector", () => {
    const bytes = [...encodeInput({ seq: 1, moveX: 1, moveY: -1, aim: 0.5, buttons: 1 })];
    expect(bytes).toEqual([
      0x01, 0x00, 0x00, 0x00, // seq = 1
      0x00, 0x00, 0x80, 0x3f, // moveX = 1.0
      0x00, 0x00, 0x80, 0xbf, // moveY = -1.0
      0x00, 0x00, 0x00, 0x3f, // aim = 0.5
      0x01, // buttons = fire
    ]);
  });

  it("encodes a SnapshotFrame to the canonical byte vector", () => {
    const bytes = [
      ...encodeSnapshot({
        tick: 2,
        ackSeq: 1,
        tanks: [{ slot: 0, x: 64, y: 128, rotation: 0, turretRotation: 0.5, hp: 3, team: 1 }],
        wallDeltas: [{ cellX: 5, cellY: 6, material: 1, hp: 2 }],
      }),
    ];
    expect(bytes).toEqual([
      0x02, 0x00, 0x00, 0x00, // tick = 2
      0x01, 0x00, 0x00, 0x00, // ackSeq = 1
      0x01, // tankCount = 1
      0x00, // slot = 0
      0x00, 0x00, 0x80, 0x42, // x = 64.0
      0x00, 0x00, 0x00, 0x43, // y = 128.0
      0x00, 0x00, 0x00, 0x00, // rotation = 0.0
      0x00, 0x00, 0x00, 0x3f, // turret = 0.5
      0x03, // hp = 3
      0x01, // team = 1
      0x01, 0x00, // wallCount = 1
      0x05, 0x00, // cellX = 5
      0x06, 0x00, // cellY = 6
      0x01, // material = 1 (brick)
      0x02, // hp = 2
    ]);
  });
});
