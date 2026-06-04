// The server's copy of the arena. The client renders the same Battlefield01 text map
// (client/src/GameLogic/Battlefield01.cs); both sides must agree on wall layout, so the string is
// duplicated here verbatim. Materials match the wire encoding: 0 floor, 1 brick, 2 steel.

export const MATERIAL_FLOOR = 0;
export const MATERIAL_BRICK = 1;
export const MATERIAL_STEEL = 2;

export const TILE_SIZE = 64;
export const BRICK_HP = 3;

// '#' steel · 'x' brick · 'b' bush (passable floor — concealment is client-only) · '.' floor · '@' spawn.
const BATTLEFIELD01 = [
  "############################",
  "#.@........................#",
  "#......xx...........#......#",
  "#......xx...........#......#",
  "#...bb....x................#",
  "#....................bbx...#",
  "#............##............#",
  "#...x........##............#",
  "#...x......................#",
  "#...x.......bb....xx.......#",
  "#.................x........#",
  "#...............x..........#",
  "#.....##.bb......bb..x.....#",
  "#..........x...............#",
  "#.........xx...............#",
  "############################",
].join("\n");

export interface GameMap {
  width: number;
  height: number;
  materials: Uint8Array; // indexed [y * width + x]
  brickHp: Uint8Array; // same layout; 0 where not a brick
  spawn: { x: number; y: number }; // the '@' cell
}

export function parseMap(text: string = BATTLEFIELD01): GameMap {
  const rows = text.split("\n").filter((r) => r.length > 0);
  const height = rows.length;
  const width = rows[0].length;
  const materials = new Uint8Array(width * height);
  const brickHp = new Uint8Array(width * height);
  const spawn = { x: 1, y: 1 };

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const ch = rows[y][x];
      const index = y * width + x;
      if (ch === "#") {
        materials[index] = MATERIAL_STEEL;
      } else if (ch === "x") {
        materials[index] = MATERIAL_BRICK;
        brickHp[index] = BRICK_HP;
      } else {
        // '.', 'b', '@' are all passable floor for the server (bush concealment is client-only).
        materials[index] = MATERIAL_FLOOR;
        if (ch === "@") {
          spawn.x = x;
          spawn.y = y;
        }
      }
    }
  }

  return { width, height, materials, brickHp, spawn };
}

// Out-of-bounds reads as steel — the implicit border, matching the client's GridArena.
export function materialAt(map: GameMap, cellX: number, cellY: number): number {
  if (cellX < 0 || cellY < 0 || cellX >= map.width || cellY >= map.height) {
    return MATERIAL_STEEL;
  }
  return map.materials[cellY * map.width + cellX];
}

export function cellOf(worldX: number, worldY: number): { cellX: number; cellY: number } {
  return { cellX: Math.floor(worldX / TILE_SIZE), cellY: Math.floor(worldY / TILE_SIZE) };
}

export function cellCentre(cellX: number, cellY: number): { x: number; y: number } {
  return { x: (cellX + 0.5) * TILE_SIZE, y: (cellY + 0.5) * TILE_SIZE };
}

export function solidAtWorld(map: GameMap, worldX: number, worldY: number): boolean {
  const { cellX, cellY } = cellOf(worldX, worldY);
  return materialAt(map, cellX, cellY) !== MATERIAL_FLOOR;
}
