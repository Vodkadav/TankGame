using System.Collections.Generic;
using Godot;

namespace TankGame.Presentation;

/// <summary>A pooled one-shot SFX player. Owns a ring buffer of 16
/// <see cref="AudioStreamPlayer3D"/> nodes for positional sounds and two flat
/// <see cref="AudioStreamPlayer"/> nodes for non-positional sounds (one at full volume for
/// clicks/victory, one at −8 dB for hover sounds). OGG files are loaded directly via
/// <see cref="AudioStreamOggVorbis.LoadFromBuffer"/> — no Godot import step required,
/// so a <c>git pull</c> immediately delivers working audio without opening the editor.</summary>
public partial class SfxPool : Node
{
    private const int Pool3DSize = 16;
    private const string SfxDir = "res://audio/sfx/";
    // Menu sounds sit well below gameplay SFX so navigating the UI is a gentle tick, not a barrage
    // (owner feedback 2026-06-18: the menu ping was too loud). Hover is the quietest; clicks a touch
    // louder but still subdued.
    private const float HoverOffsetDb = -16f;
    private const float UiClickOffsetDb = -6f;

    private static readonly Dictionary<SfxKind, string> SfxFiles = new()
    {
        [SfxKind.Fire]      = "fire.ogg",
        [SfxKind.Explosion] = "explosion.ogg",
        [SfxKind.WallBreak] = "wall_break.ogg",
        [SfxKind.Pickup]    = "pickup.ogg",
        [SfxKind.Victory]   = "victory.ogg",
        [SfxKind.UiClick]   = "ui_click.ogg",
        [SfxKind.UiHover]   = "ui_hover.ogg",
        [SfxKind.PowerupSpeed]     = "powerup_speed.ogg",
        [SfxKind.PowerupRapidFire] = "powerup_rapid.ogg",
        [SfxKind.PowerupBouncing]  = "powerup_bounce.ogg",
        [SfxKind.PowerupSpread]    = "powerup_spread.ogg",
        [SfxKind.PowerupPiercing]  = "powerup_pierce.ogg",
        [SfxKind.PowerupRepair]    = "powerup_repair.ogg",
        [SfxKind.PowerupShield]    = "powerup_shield.ogg",
        [SfxKind.PowerupMissile]   = "powerup_missile.ogg",
        [SfxKind.PowerupAirstrike] = "powerup_airstrike.ogg",
        [SfxKind.KillEnemy]        = "kill_enemy.ogg",
        [SfxKind.StreakDouble]     = "streak_double.ogg",
        [SfxKind.StreakTriple]     = "streak_triple.ogg",
        [SfxKind.StreakMulti]      = "streak_multi.ogg",
    };

    private readonly AudioStreamPlayer3D[] _pool3D = new AudioStreamPlayer3D[Pool3DSize];
    private AudioStreamPlayer _pool2D  = null!;
    private AudioStreamPlayer _hoverPlayer = null!;
    private AudioStreamPlayer _voicePlayer = null!;
    private readonly Dictionary<SfxKind, AudioStream?> _streams = new();
    private int _next3D;
    private float _sfxVolumeDb;

    public override void _Ready()
    {
        for (var i = 0; i < Pool3DSize; i++)
        {
            var p = new AudioStreamPlayer3D { Name = $"Sfx3D_{i}" };
            AddChild(p);
            _pool3D[i] = p;
        }

        _pool2D = new AudioStreamPlayer { Name = "Sfx2D" };
        AddChild(_pool2D);

        _hoverPlayer = new AudioStreamPlayer { Name = "SfxHover", VolumeDb = HoverOffsetDb };
        AddChild(_hoverPlayer);

        // Announcer voice lines get their own player so a kill callout is never cut off by a
        // simultaneous UI click or another voice line interrupting mid-word.
        _voicePlayer = new AudioStreamPlayer { Name = "SfxVoice" };
        AddChild(_voicePlayer);

        foreach (var (kind, file) in SfxFiles)
            _streams[kind] = LoadOgg(SfxDir + file);
    }

    // Load an OGG file via FileAccess + LoadFromBuffer — bypasses the Godot import/resource
    // system entirely, so the file works immediately after a git pull without an editor import.
    private static AudioStream? LoadOgg(string resPath)
    {
        using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (file is null) return null;
        var bytes = file.GetBuffer((long)file.GetLength());
        return AudioStreamOggVorbis.LoadFromBuffer(bytes);
    }

    /// <summary>Set the SFX volume for every player in the pool (dB; 0 = full, negative = quieter).
    /// Call after loading the user's audio settings.</summary>
    public void SetVolumeDb(float db)
    {
        _sfxVolumeDb = db;
        foreach (var p in _pool3D) p.VolumeDb = db;
        _pool2D.VolumeDb = db;
        _hoverPlayer.VolumeDb = db + HoverOffsetDb;
        _voicePlayer.VolumeDb = db;
    }

    /// <summary>Play a positional sound at the given world position.</summary>
    public void PlayAt(SfxKind kind, Vector3 worldPosition)
    {
        if (!_streams.TryGetValue(kind, out var stream) || stream is null) return;
        var player = _pool3D[_next3D % Pool3DSize];
        _next3D++;
        player.Stream = stream;
        player.Position = worldPosition;
        player.Play();
    }

    /// <summary>Play a non-positional sound (victory sting, UI clicks). Clicks are attenuated so
    /// menu navigation stays gentle; the victory sting plays at full SFX volume.</summary>
    public void PlayUi(SfxKind kind)
    {
        if (!_streams.TryGetValue(kind, out var stream) || stream is null) return;
        _pool2D.VolumeDb = _sfxVolumeDb + (kind == SfxKind.UiClick ? UiClickOffsetDb : 0f);
        _pool2D.Stream = stream;
        _pool2D.Play();
    }

    /// <summary>Play an announcer voice line (kill / streak callout) on the dedicated voice channel,
    /// non-positional and at full SFX volume so it is always clearly audible.</summary>
    public void PlayVoice(SfxKind kind)
    {
        if (!_streams.TryGetValue(kind, out var stream) || stream is null) return;
        _voicePlayer.Stream = stream;
        _voicePlayer.Play();
    }

    /// <summary>Play a quiet hover sound (mouse-entered on a button). Uses a separate
    /// player so it does not interrupt a simultaneous click sound.</summary>
    public void PlayHover()
    {
        if (!_streams.TryGetValue(SfxKind.UiHover, out var stream) || stream is null) return;
        _hoverPlayer.Stream = stream;
        _hoverPlayer.Play();
    }

    // ── Settings helpers ────────────────────────────────────────────────────────────────────

    private const string SettingsPath  = "user://settings.cfg";
    private const string AudioSection  = "audio";

    public static float LoadSfxVolumeDb()
    {
        var cfg = new ConfigFile();
        return cfg.Load(SettingsPath) == Error.Ok
            ? (float)cfg.GetValue(AudioSection, "sfx_volume_db", 0f)
            : 0f;
    }

    public static void SaveSfxVolumeDb(float db)
    {
        var cfg = new ConfigFile();
        cfg.Load(SettingsPath);
        cfg.SetValue(AudioSection, "sfx_volume_db", db);
        cfg.Save(SettingsPath);
    }
}
