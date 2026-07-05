import { describe, it, expect } from "vitest";
import {
  emptyLobby,
  reduce,
  MAX_PLAYERS,
  COUNTDOWN_SECONDS,
  type LobbyState,
} from "./lobbyState";

// Apply a sequence of commands for terser scenario setup.
function run(state: LobbyState, ...commands: Parameters<typeof reduce>[1][]): LobbyState {
  return commands.reduce(reduce, state);
}

function joinN(n: number, mode: "ffa" | "team" = "ffa"): LobbyState {
  let state = emptyLobby(mode);
  for (let slot = 0; slot < n; slot++) {
    state = reduce(state, { type: "join", slot, name: `P${slot}` });
  }
  return state;
}

describe("lobby state reducer", () => {
  it("starts empty and waiting in FFA", () => {
    const state = emptyLobby();
    expect(state).toEqual({
      mode: "ffa",
      phase: "waiting",
      hostSlot: 0,
      countdown: 0,
      map: "",
      players: [],
    });
  });

  it("the first joiner becomes the host", () => {
    const state = run(emptyLobby(), { type: "join", slot: 3, name: "Alice" });
    expect(state.hostSlot).toBe(3);
    expect(state.players).toEqual([{ slot: 3, name: "Alice", team: 3, ready: false }]);
  });

  it("keeps players sorted by slot regardless of join order", () => {
    const state = run(
      emptyLobby(),
      { type: "join", slot: 2, name: "B" },
      { type: "join", slot: 0, name: "A" },
    );
    expect(state.players.map((p) => p.slot)).toEqual([0, 2]);
  });

  it("FFA gives every player their own team (= slot)", () => {
    const state = joinN(3, "ffa");
    expect(state.players.map((p) => p.team)).toEqual([0, 1, 2]);
  });

  it("Team mode balances joins across the two teams", () => {
    const state = joinN(4, "team");
    expect(state.players.map((p) => p.team)).toEqual([0, 1, 0, 1]);
  });

  it("ignores a duplicate slot join", () => {
    const state = run(
      emptyLobby(),
      { type: "join", slot: 0, name: "A" },
      { type: "join", slot: 0, name: "A-again" },
    );
    expect(state.players).toHaveLength(1);
    expect(state.players[0].name).toBe("A");
  });

  it("caps the lobby at MAX_PLAYERS", () => {
    let state = joinN(MAX_PLAYERS);
    state = reduce(state, { type: "join", slot: MAX_PLAYERS, name: "overflow" });
    expect(state.players).toHaveLength(MAX_PLAYERS);
  });

  it("setReady and setName edit only the addressed player", () => {
    const state = run(
      joinN(2),
      { type: "setReady", slot: 1, ready: true },
      { type: "setName", slot: 0, name: "  Renamed  " },
    );
    expect(state.players[0].name).toBe("Renamed");
    expect(state.players[1].ready).toBe(true);
    expect(state.players[0].ready).toBe(false);
  });

  it("setTeam works in Team mode and is ignored in FFA", () => {
    const team = run(joinN(2, "team"), { type: "setTeam", slot: 0, team: 1 });
    expect(team.players[0].team).toBe(1);

    const ffa = run(joinN(2, "ffa"), { type: "setTeam", slot: 0, team: 1 });
    expect(ffa.players[0].team).toBe(0); // FFA team stays = slot
  });

  it("only the host may change the mode, and it re-derives teams", () => {
    const hosted = joinN(3, "ffa"); // host is slot 0
    const ignored = reduce(hosted, { type: "setMode", slot: 2, mode: "team" });
    expect(ignored.mode).toBe("ffa");

    const switched = reduce(hosted, { type: "setMode", slot: 0, mode: "team" });
    expect(switched.mode).toBe("team");
    expect(switched.players.map((p) => p.team)).toEqual([0, 1, 0]);
  });

  it("any player can start once two have joined, opening the countdown", () => {
    const state = reduce(joinN(2), { type: "start", slot: 1 });
    expect(state.phase).toBe("countdown");
    expect(state.countdown).toBe(COUNTDOWN_SECONDS);
  });

  it("refuses a start from someone not seated in the lobby", () => {
    const state = reduce(joinN(1), { type: "start", slot: 3 });
    expect(state.phase).toBe("waiting");
  });

  it("counts down and hands off to started", () => {
    let state = reduce(joinN(2), { type: "start", slot: 0 });
    for (let i = 0; i < COUNTDOWN_SECONDS; i++) {
      state = reduce(state, { type: "tick" });
    }
    expect(state.phase).toBe("started");
    expect(state.countdown).toBe(0);
  });

  it("a tick does nothing while waiting", () => {
    const state = reduce(joinN(2), { type: "tick" });
    expect(state.phase).toBe("waiting");
  });

  it("reassigns the host to the lowest remaining slot when the host leaves", () => {
    const state = run(joinN(3), { type: "leave", slot: 0 });
    expect(state.hostSlot).toBe(1);
    expect(state.players.map((p) => p.slot)).toEqual([1, 2]);
  });

  it("empties back to a fresh waiting lobby when the last player leaves", () => {
    const state = run(joinN(1), { type: "leave", slot: 0 });
    expect(state).toEqual(emptyLobby());
  });

  it("a single player can start — AI tanks fill the empty seats (owner ask)", () => {
    const state = reduce(joinN(1), { type: "start", slot: 0 });
    expect(state.phase).toBe("countdown");
  });

  it("keeps counting down when a leave still leaves someone seated", () => {
    let state = reduce(joinN(2), { type: "start", slot: 0 });
    expect(state.phase).toBe("countdown");
    state = reduce(state, { type: "leave", slot: 1 });
    expect(state.phase).toBe("countdown"); // the remaining player still gets their match (AI fill)
  });

  it("resets to a fresh waiting lobby when the countdown loses its last player", () => {
    let state = reduce(joinN(1), { type: "start", slot: 0 });
    state = reduce(state, { type: "leave", slot: 0 });
    expect(state.phase).toBe("waiting");
    expect(state.countdown).toBe(0);
  });

  it("rejects joins once the match is counting down", () => {
    let state = reduce(joinN(2), { type: "start", slot: 0 });
    state = reduce(state, { type: "join", slot: 2, name: "late" });
    expect(state.players).toHaveLength(2);
  });
});

// The room's map (multiplayer plan Phase 1): "" is the random-map sentinel resolved at host launch.
describe("lobby map", () => {
  it("starts on the random-map sentinel", () => {
    expect(emptyLobby().map).toBe("");
  });

  it("lets the host pick a map while waiting", () => {
    const state = reduce(joinN(2), { type: "setMap", slot: 0, map: "CliffsAndValleys" });
    expect(state.map).toBe("CliffsAndValleys");
  });

  it("ignores a map pick from a non-host", () => {
    const state = reduce(joinN(2), { type: "setMap", slot: 1, map: "CliffsAndValleys" });
    expect(state.map).toBe("");
  });

  it("ignores a map pick once the countdown is running", () => {
    let state = reduce(joinN(2), { type: "start", slot: 0 });
    state = reduce(state, { type: "setMap", slot: 0, map: "CliffsAndValleys" });
    expect(state.map).toBe("");
  });

  it("keeps the picked map across joins and leaves", () => {
    const state = run(
      joinN(2),
      { type: "setMap", slot: 0, map: "DesertWar" },
      { type: "join", slot: 2, name: "C" },
      { type: "leave", slot: 1 },
    );
    expect(state.map).toBe("DesertWar");
  });
});
