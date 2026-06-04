// Lobby registry (M3-T4): allocate a shareable code for a new match and validate joins. A code
// maps 1:1 to a MatchRoom Durable Object (the code is the DO's name); KV records the mapping so a
// join can be rejected for an unknown/expired code. Narrow interfaces keep the logic unit-testable
// without a live KV or DO namespace.

export interface LobbyStore {
  get(key: string): Promise<string | null>;
  put(key: string, value: string): Promise<void>;
}

export interface RoomIdSource {
  idFromName(name: string): { toString(): string };
}

// No ambiguous characters (O/0, I/1) — codes get typed by hand on a phone.
const CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
const CODE_LENGTH = 6;

export function randomLobbyCode(): string {
  const bytes = new Uint8Array(CODE_LENGTH);
  crypto.getRandomValues(bytes);
  let code = "";
  for (const byte of bytes) {
    code += CODE_ALPHABET[byte % CODE_ALPHABET.length];
  }
  return code;
}

function lobbyKey(code: string): string {
  return `lobby:${code}`;
}

/** Allocates a fresh lobby, retrying on the (rare) chance a generated code is already taken. */
export async function createLobby(
  store: LobbyStore,
  rooms: RoomIdSource,
  makeCode: () => string = randomLobbyCode,
  maxAttempts = 8,
): Promise<{ code: string; doId: string }> {
  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    const code = makeCode();
    if ((await store.get(lobbyKey(code))) === null) {
      const doId = rooms.idFromName(code).toString();
      await store.put(lobbyKey(code), doId);
      return { code, doId };
    }
  }
  throw new Error("could not allocate a unique lobby code");
}

/** Resolves a join: the WebSocket URL for a known code, or null if the lobby does not exist. */
export async function joinLobby(
  store: LobbyStore,
  code: string,
): Promise<{ code: string; wsUrl: string } | null> {
  const doId = await store.get(lobbyKey(code));
  return doId === null ? null : { code, wsUrl: `/room/${code}` };
}
