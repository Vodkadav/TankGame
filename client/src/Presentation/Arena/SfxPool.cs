using System.Collections.Generic;
using Godot;

namespace TankGame.Presentation;

/// <summary>A pooled one-shot SFX player for the arena and title screen. Owns a ring buffer of
/// 16 <see cref="AudioStreamPlayer3D"/> nodes for positional sounds (tank fire, explosions, wall
/// breaks, pickups) and one flat <see cref="AudioStreamPlayer"/> for non-positional sounds (victory
/// sting, UI clicks). Each <see cref="SfxKind"/> maps to a FLAC under <c>res://audio/sfx/</c>;
/// missing files are silently skipped so the pool degrades gracefully before assets are installed.
///
/// <para>Perf rule: no per-frame allocation — the pool is fixed-size and reused round-robin.
/// Add this node as a child of the scene that owns it; 3D players inherit their parent's
/// world-space origin (zero for both Arena3D and Title root nodes).</para></summary>
public partial class SfxPool : Node
{
    private const int Pool3DSize = 16;
    private const string SfxDir = "res://audio/sfx/";

    // One filename per kind — the asset pipeline writes these names (see docs/credits/assets.md).
    private static readonly Dictionary<SfxKind, string> SfxFiles = new()
    {
        [SfxKind.Fire]      = "fire.flac",
        [SfxKind.Explosion] = "explosion.flac",
        [SfxKind.WallBreak] = "wall_break.flac",
        [SfxKind.Pickup]    = "pickup.flac",
        [SfxKind.Victory]   = "victory.flac",
        [SfxKind.UiClick]   = "ui_click.flac",
    };

    private readonly AudioStreamPlayer3D[] _pool3D = new AudioStreamPlayer3D[Pool3DSize];
    private AudioStreamPlayer _pool2D = null!;
    private readonly Dictionary<SfxKind, AudioStream?> _streams = new();
    private int _next3D;

    public override void _Ready()
    {
        // Spin up the 3D pool (positional — world-space fire / explosion / wall-break / pickup).
        for (var i = 0; i < Pool3DSize; i++)
        {
            var player = new AudioStreamPlayer3D { Name = $"Sfx3D_{i}" };
            AddChild(player);
            _pool3D[i] = player;
        }

        // One 2D player for non-positional sounds (victory sting, UI clicks).
        _pool2D = new AudioStreamPlayer { Name = "Sfx2D" };
        AddChild(_pool2D);

        // Load each stream; null if the file is not yet installed.
        foreach (var (kind, file) in SfxFiles)
        {
            var path = SfxDir + file;
            _streams[kind] = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        }
    }

    /// <summary>Set the SFX volume for every player in the pool (dB; 0 = full, negative = quieter).
    /// Call after loading the user's audio settings.</summary>
    public void SetVolumeDb(float db)
    {
        foreach (var p in _pool3D) p.VolumeDb = db;
        _pool2D.VolumeDb = db;
    }

    /// <summary>Play a positional sound at the given world position. Cycles the 3D ring buffer
    /// so rapid-fire events overlap rather than interrupt each other.</summary>
    public void PlayAt(SfxKind kind, Vector3 worldPosition)
    {
        if (!_streams.TryGetValue(kind, out var stream) || stream is null) return;
        var player = _pool3D[_next3D % Pool3DSize];
        _next3D++;
        player.Stream = stream;
        player.Position = worldPosition;
        player.Play();
    }

    /// <summary>Play a non-positional sound (victory sting, menu clicks). Uses the single
    /// 2D player; interrupts itself if called faster than the clip's length, which is
    /// acceptable for short UI clicks.</summary>
    public void PlayUi(SfxKind kind)
    {
        if (!_streams.TryGetValue(kind, out var stream) || stream is null) return;
        _pool2D.Stream = stream;
        _pool2D.Play();
    }

    // ── Settings helpers ─────────────────────────────────────────────────────────────────────────

    private const string SettingsPath = "user://settings.cfg";
    private const string AudioSection  = "audio";

    /// <summary>Read the SFX volume (dB) from <c>user://settings.cfg</c>. Returns 0 (full volume)
    /// when the key is absent or the file does not exist yet.</summary>
    public static float LoadSfxVolumeDb()
    {
        var cfg = new ConfigFile();
        return cfg.Load(SettingsPath) == Error.Ok
            ? (float)cfg.GetValue(AudioSection, "sfx_volume_db", 0f)
            : 0f;
    }

    /// <summary>Persist the SFX volume to <c>user://settings.cfg</c>, preserving all other
    /// sections (e.g. the player name).</summary>
    public static void SaveSfxVolumeDb(float db)
    {
        var cfg = new ConfigFile();
        cfg.Load(SettingsPath); // keep player.name etc.; a missing file is fine
        cfg.SetValue(AudioSection, "sfx_volume_db", db);
        cfg.Save(SettingsPath);
    }
}
