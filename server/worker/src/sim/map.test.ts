import { describe, it, expect } from "vitest";
import {
  MATERIAL_BRICK,
  MATERIAL_FLOOR,
  MATERIAL_STEEL,
  materialAt,
  parseMap,
  solidAtWorld,
  TILE_SIZE,
} from "./map";

describe("map", () => {
  it("parses Battlefield01 as a 28x16 grid with a steel border and the host spawn", () => {
    const map = parseMap();
    expect(map.width).toBe(28);
    expect(map.height).toBe(16);
    expect(map.spawn).toEqual({ x: 2, y: 1 });
    expect(materialAt(map, 0, 0)).toBe(MATERIAL_STEEL); // border corner
    expect(materialAt(map, 7, 2)).toBe(MATERIAL_BRICK); // a known brick cluster (row 2: "...xx...")
    expect(materialAt(map, 2, 1)).toBe(MATERIAL_FLOOR); // the spawn cell
  });

  it("treats out-of-bounds cells as steel (the implicit border)", () => {
    const map = parseMap();
    expect(materialAt(map, -1, 5)).toBe(MATERIAL_STEEL);
    expect(materialAt(map, 28, 5)).toBe(MATERIAL_STEEL);
  });

  it("reports solidity at a world point", () => {
    const map = parseMap();
    expect(solidAtWorld(map, 0.5 * TILE_SIZE, 0.5 * TILE_SIZE)).toBe(true); // cell (0,0) steel
    expect(solidAtWorld(map, 2.5 * TILE_SIZE, 1.5 * TILE_SIZE)).toBe(false); // spawn floor
  });
});
