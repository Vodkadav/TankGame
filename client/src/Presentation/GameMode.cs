using System;
using Godot;
using TankGame.GameLogic;

namespace TankGame.Presentation;

/// <summary>How a round is set up.</summary>
public enum GameMode
{
    /// <summary>One player versus the AI adversaries.</summary>
    OnePlayer,

    /// <summary>Two players on the same team versus the AI adversaries.</summary>
    TwoPlayerCoop,

    /// <summary>Two players against each other, no AI — last tank standing.</summary>
    TwoPlayerVersus,
}

/// <summary>A selectable battle arena (the map pool). The current procedural arena is "Desert War";
/// "Cliffs &amp; Valleys" is the upcoming multi-layer map (ADR-0018) and is not playable yet.</summary>
public enum ArenaId
{
    /// <summary>The dusty procedural square arena — the game's first map.</summary>
    DesertWar,

    /// <summary>The elevated multi-layer map (ADR-0018); not yet built.</summary>
    CliffsAndValleys,

    /// <summary>Dense woodland skirmish arena (built via <see cref="TankGame.GameLogic.ArenaBuilders"/>).</summary>
    Forest,

    /// <summary>Charred volcanic arena with lava hazards.</summary>
    Volcano,

    /// <summary>City block arena of building cover.</summary>
    City,

    /// <summary>Icy arctic wastes arena.</summary>
    Frozen,

    /// <summary>Rocky canyon-run arena.</summary>
    Canyon,

    /// <summary>Ring arena around a solid mountain core — fights orbit the donut.</summary>
    Donut,

    /// <summary>Plus-shaped arena: four arms meeting at an open central hub.</summary>
    Cross,

    /// <summary>Floor islands in a water sea, joined by bridge causeways.</summary>
    Archipelago,
}

/// <summary>Carries match-level state from the title screen into the play scene, which is loaded
/// fresh each round (so it cannot be passed by constructor). The <see cref="Series"/> persists
/// across the per-round scene reloads that drive a best-of-N match; <see cref="StartNewMatch"/>
/// resets it. Defaults let the play scene launch directly (tests, dev) without the title.</summary>
public static class GameSetup
{
    /// <summary>Round wins needed to take the match — best of three.</summary>
    public const int RoundsToWin = 2;

    public static GameMode Mode { get; set; } = GameMode.OnePlayer;

    /// <summary>The player's chosen battle name (the title screen prompts for it before any game and
    /// remembers it in <c>user://settings.cfg</c>). Blank only when no prompt has run — the play
    /// scenes fall back to "Player".</summary>
    public static string PlayerName { get; set; } = "";

    /// <summary>The chosen battle arena (the Select Map screen sets it). Only <see cref="ArenaId.DesertWar"/>
    /// is playable today; it is the map the 3D play scene builds.</summary>
    public static ArenaId Arena { get; set; } = ArenaId.DesertWar;

    /// <summary>A user-built map to play instead of the procedural arena, set when the player picks one
    /// from "My Maps". Null means play the built-in <see cref="Arena"/>. Cleared by
    /// <see cref="StartNewMatch"/>, so any built-in launch resets it; the custom-map launch sets it back
    /// afterwards. It then persists across the per-round scene reloads of a best-of-N match.</summary>
    public static MapDefinition? CustomMap { get; set; }

    /// <summary>The arena's visual palette (S8 theming): ground colour + wall tint. A title control
    /// can later set it; defaults to the sandy reference look.</summary>
    public static ArenaTheme Theme { get; set; } = ArenaTheme.Default;

    /// <summary>The active match modifier (S9): a whole-round rule applied to every tank at spawn.
    /// A title control can later set it; defaults to none (a plain match).</summary>
    public static MatchModifier Modifier { get; set; } = MatchModifier.None;

    /// <summary>The running best-of-N series; survives per-round scene reloads.</summary>
    public static SeriesTracker Series { get; private set; } = new(RoundsToWin);

    /// <summary>Seed for the procedural arena (S8). Fixed by default so a direct launch (tests, dev)
    /// is reproducible; a new match randomises it so each match gets a fresh battlefield, and it
    /// then persists across the per-round scene reloads so a best-of-N series plays one arena.</summary>
    public static int ArenaSeed { get; private set; } = 1;

    /// <summary>Size of the generated arena, in tiles. Adjustable per match (a title control can set
    /// it); the camera framing and the generator both follow it, so any sensible size works.</summary>
    public static int ArenaWidth { get; set; } = 28;
    public static int ArenaHeight { get; set; } = 16;

    /// <summary>Begins a fresh match in <paramref name="mode"/>: sets the mode, resets the series to
    /// 0 - 0, and rolls a new arena seed. Called from the title screen and from "Play again".</summary>
    public static void StartNewMatch(GameMode mode)
    {
        Mode = mode;
        Series = new SeriesTracker(RoundsToWin);
        ArenaSeed = Guid.NewGuid().GetHashCode();
        CustomMap = null; // a fresh match defaults to the built-in arena; My Maps sets it back after
    }

    // ── User settings (persisted to user://settings.cfg) ──────────────────────────────

    /// <summary>SFX volume in decibels (0 = full volume, −30 = nearly silent).</summary>
    public static float SfxVolumeDb { get; set; } = 0f;

    /// <summary>Brightness multiplier applied to the arena environment and sun (1 = default).</summary>
    public static float BrightnessMultiplier { get; set; } = 1f;

    /// <summary>Whether name labels are drawn above friendly (player-team) tanks.</summary>
    public static bool ShowFriendlyNames { get; set; } = true;

    /// <summary>Whether name labels are drawn above enemy tanks. On by default (owner ask) so you can
    /// see who you're fighting; the fog still hides out-of-vision enemies (and their tags) entirely.</summary>
    public static bool ShowEnemyNames { get; set; } = true;

    /// <summary>Bot skill level for AI tanks (solo, legacy 2D, and the host's net-bot fill).</summary>
    public static Difficulty BotDifficulty { get; set; } = Difficulty.Normal;

    /// <summary>Raised whenever the user saves new settings; subscribers update live.</summary>
    public static event System.Action? SettingsChanged;

    private const string SettingsPath = "user://settings.cfg";

    /// <summary>Load all user settings from <c>user://settings.cfg</c> into the static
    /// properties. Call once at startup (TitleScene._Ready).</summary>
    public static void LoadSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SettingsPath) != Error.Ok) return;
        PlayerName           = (string)cfg.GetValue("player",  "name",               "");
        SfxVolumeDb          = (float) cfg.GetValue("audio",   "sfx_volume_db",       0f);
        BrightnessMultiplier = (float) cfg.GetValue("display", "brightness",           1f);
        ShowFriendlyNames    = (bool)  cfg.GetValue("display", "show_friendly_names", true);
        ShowEnemyNames       = (bool)  cfg.GetValue("display", "show_enemy_names",    true);
        BotDifficulty        = (Difficulty)(int)cfg.GetValue("game", "bot_difficulty", (int)Difficulty.Normal);
    }

    /// <summary>Persist all user settings and raise <see cref="SettingsChanged"/>.</summary>
    public static void ApplySettings()
    {
        var cfg = new ConfigFile();
        cfg.Load(SettingsPath);
        cfg.SetValue("player",  "name",               PlayerName);
        cfg.SetValue("audio",   "sfx_volume_db",       SfxVolumeDb);
        cfg.SetValue("display", "brightness",           BrightnessMultiplier);
        cfg.SetValue("display", "show_friendly_names", ShowFriendlyNames);
        cfg.SetValue("display", "show_enemy_names",    ShowEnemyNames);
        cfg.SetValue("game",    "bot_difficulty",      (int)BotDifficulty);
        cfg.Save(SettingsPath);
        SettingsChanged?.Invoke();
    }
}
