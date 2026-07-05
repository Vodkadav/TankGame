// The pre-game lobby state machine (multiplayer milestone, web-export.md §5). One MatchRoom DO owns a
// LobbyState; clients send LobbyCommands over a JSON control channel and the DO broadcasts the new
// state. Up to 4 players pick a mode (free-for-all or two teams), ready up, and any player can start
// the match — a short countdown then hands off to the binary game relay. This module is the pure,
// engine-free heart of that flow so it is exhaustively unit-testable (the DO is a thin shell over it).

// 4 seats per room (owner ask 2026-07-05, "extendable later"): the client room UI, the spawn table
// and the relay cap all size themselves off this one constant.
export const MAX_PLAYERS = 4;
export const COUNTDOWN_SECONDS = 3;
export const MIN_PLAYERS_TO_START = 2;

export type GameMode = "ffa" | "team";
export type LobbyPhase = "waiting" | "countdown" | "started";

export interface Player {
  slot: number;
  name: string;
  team: number; // FFA: team === slot (everyone their own); Team: 0 or 1
  ready: boolean;
}

export interface LobbyState {
  mode: GameMode;
  phase: LobbyPhase;
  hostSlot: number;
  countdown: number; // seconds left while phase === "countdown"; 0 otherwise
  map: string; // map id the host picked; "" = random, resolved by the host client at launch
  players: Player[]; // always sorted by slot
}

export type LobbyCommand =
  | { type: "join"; slot: number; name: string }
  | { type: "leave"; slot: number }
  | { type: "setName"; slot: number; name: string }
  | { type: "setReady"; slot: number; ready: boolean }
  | { type: "setTeam"; slot: number; team: number }
  | { type: "setMode"; slot: number; mode: GameMode }
  | { type: "setMap"; slot: number; map: string }
  | { type: "start"; slot: number }
  | { type: "tick" };

export function emptyLobby(mode: GameMode = "ffa", map = ""): LobbyState {
  return { mode, phase: "waiting", hostSlot: 0, countdown: 0, map, players: [] };
}

export function reduce(state: LobbyState, command: LobbyCommand): LobbyState {
  switch (command.type) {
    case "join":
      return join(state, command.slot, command.name);
    case "leave":
      return leave(state, command.slot);
    case "setName":
      return editPlayer(state, command.slot, (p) => ({ ...p, name: cleanName(command.name) }));
    case "setReady":
      return editPlayer(state, command.slot, (p) => ({ ...p, ready: command.ready }));
    case "setTeam":
      return state.mode === "team"
        ? editPlayer(state, command.slot, (p) => ({ ...p, team: clampTeam(command.team) }))
        : state;
    case "setMode":
      return setMode(state, command.slot, command.mode);
    case "setMap":
      return setMap(state, command.slot, command.map);
    case "start":
      return start(state, command.slot);
    case "tick":
      return tick(state);
  }
}

function join(state: LobbyState, slot: number, name: string): LobbyState {
  if (state.phase !== "waiting" || state.players.length >= MAX_PLAYERS || hasSlot(state, slot)) {
    return state;
  }

  const firstIn = state.players.length === 0;
  const team = state.mode === "ffa" ? slot : smallerTeam(state.players);
  const players = sortBySlot([...state.players, { slot, name: cleanName(name), team, ready: false }]);
  return { ...state, players, hostSlot: firstIn ? slot : state.hostSlot };
}

function leave(state: LobbyState, slot: number): LobbyState {
  if (!hasSlot(state, slot)) {
    return state;
  }

  const players = state.players.filter((p) => p.slot !== slot);
  if (players.length === 0) {
    return emptyLobby(state.mode); // the room empties back to a fresh waiting lobby
  }

  const hostSlot = slot === state.hostSlot ? players[0].slot : state.hostSlot;
  // A departure that drops below the start threshold aborts an in-progress countdown.
  const aborts = state.phase === "countdown" && players.length < MIN_PLAYERS_TO_START;
  return {
    ...state,
    players,
    hostSlot,
    phase: aborts ? "waiting" : state.phase,
    countdown: aborts ? 0 : state.countdown,
  };
}

function setMode(state: LobbyState, slot: number, mode: GameMode): LobbyState {
  if (slot !== state.hostSlot || state.phase !== "waiting") {
    return state; // only the host re-picks the mode, and only before the match starts
  }

  // Re-derive teams for the new mode: FFA gives everyone their own team (= slot); Team splits the
  // roster as evenly as possible by join order.
  const players = state.players.map((p, i) => ({ ...p, team: mode === "ffa" ? p.slot : i % 2 }));
  return { ...state, mode, players };
}

function setMap(state: LobbyState, slot: number, map: string): LobbyState {
  if (slot !== state.hostSlot || state.phase !== "waiting") {
    return state; // like the mode: only the host picks the map, and only before the match starts
  }
  return { ...state, map: map.trim().slice(0, 64) };
}

function start(state: LobbyState, slot: number): LobbyState {
  if (!hasSlot(state, slot) || state.phase !== "waiting" || state.players.length < MIN_PLAYERS_TO_START) {
    return state;
  }
  return { ...state, phase: "countdown", countdown: COUNTDOWN_SECONDS };
}

function tick(state: LobbyState): LobbyState {
  if (state.phase !== "countdown") {
    return state;
  }
  const countdown = state.countdown - 1;
  return countdown <= 0
    ? { ...state, phase: "started", countdown: 0 }
    : { ...state, countdown };
}

function editPlayer(state: LobbyState, slot: number, edit: (p: Player) => Player): LobbyState {
  if (state.phase !== "waiting" || !hasSlot(state, slot)) {
    return state;
  }
  return { ...state, players: state.players.map((p) => (p.slot === slot ? edit(p) : p)) };
}

function hasSlot(state: LobbyState, slot: number): boolean {
  return state.players.some((p) => p.slot === slot);
}

function smallerTeam(players: Player[]): number {
  const onTeamZero = players.filter((p) => p.team === 0).length;
  const onTeamOne = players.filter((p) => p.team === 1).length;
  return onTeamZero <= onTeamOne ? 0 : 1;
}

function clampTeam(team: number): number {
  return team === 1 ? 1 : 0;
}

function cleanName(name: string): string {
  const trimmed = name.trim().slice(0, 16);
  return trimmed.length > 0 ? trimmed : "Player";
}

function sortBySlot(players: Player[]): Player[] {
  return [...players].sort((a, b) => a.slot - b.slot);
}
