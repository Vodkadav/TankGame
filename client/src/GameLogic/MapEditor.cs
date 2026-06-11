using System.Collections.Generic;
using System.Linq;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>What a click in the editor does to a cell.</summary>
public enum EditorAction
{
    PaintMaterial,
    ToggleBush,
    ToggleSandbag,
    SetPlayerSpawn,
    ToggleEnemySpawn,

    /// <summary>The unified spawn tool (owner follow-up 2026-06-11): one numbered pool of up to
    /// eight markers; marker 1 doubles as the format's player slot.</summary>
    ToggleSpawn,
    TogglePowerup,
    PlaceTeleportPad,
    RaiseLayer,
    LowerLayer,
    ToggleRamp,
    RotateCell,
    Erase,
}

/// <summary>The mutable state behind the map editor — all the editing rules with no Godot, so the scene
/// is a thin view over it. Starts as a steel-bordered floor arena; the outer ring is fixed (a click on a
/// border cell is ignored), bushes/sandbags/spawns only sit on interior floor. <see cref="ToMap"/> hands
/// the current state to <see cref="MapCodec"/>/<see cref="MapValidator"/> as a <see cref="MapDefinition"/>.
/// </summary>
public sealed class MapEditor
{
    // Not readonly: Resize swaps the grids for re-sized copies (owner follow-up 2026-06-11).
    private CellMaterial[,] _materials;
    private bool[,] _bushes;
    private bool[,] _sandbags;
    private int[,] _layers;
    private bool[,] _ramps;
    private int[,] _orientations;
    private readonly List<(int X, int Y)> _enemySpawns = new();
    private readonly List<PowerupSpawn> _powerupSpawns = new();
    private readonly List<TeleportPadLink> _teleportPads = new();
    private (int X, int Y) _playerSpawn = (1, 1);
    private (int X, int Y)? _pendingTeleportPad;

    public MapEditor(string name, int width, int height)
    {
        Name = name;
        var blank = MapDefinition.CreateBlank(name, width, height);
        _materials = blank.Materials;
        _bushes = blank.Bushes;
        _sandbags = blank.Sandbags;
        _layers = new int[width, height];
        _ramps = new bool[width, height];
        _orientations = new int[width, height];
        Width = width;
        Height = height;
    }

    public string Name { get; set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Re-sizes the arena in place, keeping everything that still fits (owner follow-up
    /// 2026-06-11 — a size change must not throw the author's work away): interior cells, overlays,
    /// elevation, facings, the theme, and every spawn/powerup/pad that lands inside the new interior.
    /// The steel ring is re-drawn at the new bounds; a teleport link loses both ends if either falls
    /// out; the player spawn rehomes to (1, 1) if its cell no longer fits.</summary>
    public void Resize(int width, int height)
    {
        var blank = MapDefinition.CreateBlank(Name, width, height);
        var materials = blank.Materials;
        var bushes = new bool[width, height];
        var sandbags = new bool[width, height];
        var layers = new int[width, height];
        var ramps = new bool[width, height];
        var orientations = new int[width, height];

        // Copy the shared interior only — both rings (the old border and the new) stay out of it, so
        // growing a map melts the old steel ring into open floor instead of leaving a stray wall.
        for (var x = 1; x < System.Math.Min(Width, width) - 1; x++)
        {
            for (var y = 1; y < System.Math.Min(Height, height) - 1; y++)
            {
                materials[x, y] = _materials[x, y];
                bushes[x, y] = _bushes[x, y];
                sandbags[x, y] = _sandbags[x, y];
                layers[x, y] = _layers[x, y];
                ramps[x, y] = _ramps[x, y];
                orientations[x, y] = _orientations[x, y];
            }
        }

        _materials = materials;
        _bushes = bushes;
        _sandbags = sandbags;
        _layers = layers;
        _ramps = ramps;
        _orientations = orientations;
        Width = width;
        Height = height;

        bool Fits(int x, int y) => x >= 1 && y >= 1 && x < width - 1 && y < height - 1;
        _enemySpawns.RemoveAll(s => !Fits(s.X, s.Y));
        _powerupSpawns.RemoveAll(p => !Fits(p.X, p.Y));
        _teleportPads.RemoveAll(p => !Fits(p.AX, p.AY) || !Fits(p.BX, p.BY));
        if (_pendingTeleportPad is { } pending && !Fits(pending.X, pending.Y))
        {
            _pendingTeleportPad = null;
        }

        if (!Fits(_playerSpawn.X, _playerSpawn.Y))
        {
            _playerSpawn = (1, 1);
        }
    }

    public EditorAction Action { get; set; } = EditorAction.PaintMaterial;

    public CellMaterial PaintMaterial { get; set; } = CellMaterial.Brick;

    public PowerupKind PaintPowerup { get; set; } = PowerupKind.Repair;

    public (int X, int Y) PlayerSpawn => _playerSpawn;

    public IReadOnlyList<(int X, int Y)> EnemySpawns => _enemySpawns;

    /// <summary>The unified spawn pool in marker order: marker 1 (the format's player slot) first,
    /// then the rest. The editor draws these as the numbered ringed discs.</summary>
    public IReadOnlyList<(int X, int Y)> Spawns
    {
        get
        {
            var spawns = new List<(int X, int Y)>(1 + _enemySpawns.Count) { _playerSpawn };
            spawns.AddRange(_enemySpawns);
            return spawns;
        }
    }

    // Toggle a marker on the unified pool: clicking marker 1 removes it by promoting marker 2 into
    // the player slot (the last marker can never be removed — a map always has a spawn); clicking
    // any other marker removes it; clicking empty floor adds one up to the 4v4 cap.
    private void ToggleSpawn(int x, int y)
    {
        if (_playerSpawn == (x, y))
        {
            if (_enemySpawns.Count > 0)
            {
                _playerSpawn = _enemySpawns[0];
                _enemySpawns.RemoveAt(0);
            }

            return;
        }

        if (_enemySpawns.Remove((x, y)))
        {
            return;
        }

        if (1 + _enemySpawns.Count < MapValidator.MaxTankSpawns)
        {
            _enemySpawns.Add((x, y));
        }
    }

    public IReadOnlyList<PowerupSpawn> PowerupSpawns => _powerupSpawns;

    public IReadOnlyList<TeleportPadLink> TeleportPads => _teleportPads;

    /// <summary>The first pad of a link the author has placed but not yet partnered (null when none is
    /// half-placed). The scene shows a marker on it so the author can see they owe a second click.</summary>
    public (int X, int Y)? PendingTeleportPad => _pendingTeleportPad;

    public CellMaterial MaterialAt(int x, int y) => _materials[x, y];

    public bool BushAt(int x, int y) => _bushes[x, y];

    public bool SandbagAt(int x, int y) => _sandbags[x, y];

    public int LayerAt(int x, int y) => _layers[x, y];

    public bool RampAt(int x, int y) => _ramps[x, y];

    /// <summary>Applies the current <see cref="Action"/> to cell (x, y). A click on the steel border, or
    /// an overlay/spawn placement on a non-floor cell, is ignored.</summary>
    public void ApplyAt(int x, int y)
    {
        if (!IsInterior(x, y))
        {
            return;
        }

        switch (Action)
        {
            case EditorAction.PaintMaterial:
                _materials[x, y] = PaintMaterial;
                _orientations[x, y] = 0; // a freshly-painted item starts unrotated
                if (PaintMaterial != CellMaterial.Floor)
                {
                    _bushes[x, y] = false;
                    _sandbags[x, y] = false;
                }

                break;

            case EditorAction.ToggleBush when IsFloor(x, y):
                _bushes[x, y] = !_bushes[x, y];
                break;

            case EditorAction.ToggleSandbag when IsFloor(x, y):
                _sandbags[x, y] = !_sandbags[x, y];
                break;

            case EditorAction.SetPlayerSpawn when IsFloor(x, y):
                _playerSpawn = (x, y);
                break;

            case EditorAction.ToggleEnemySpawn when IsFloor(x, y):
                // Removal always works; adding stops at the 4v4 cap (player + 7 enemies = 8 tanks).
                if (!_enemySpawns.Remove((x, y)) && 1 + _enemySpawns.Count < MapValidator.MaxTankSpawns)
                {
                    _enemySpawns.Add((x, y));
                }

                break;

            case EditorAction.ToggleSpawn when IsFloor(x, y):
                ToggleSpawn(x, y);
                break;

            case EditorAction.TogglePowerup when IsFloor(x, y):
                TogglePowerup(x, y);
                break;

            case EditorAction.PlaceTeleportPad when IsFloor(x, y):
                PlaceTeleportPad(x, y);
                break;

            case EditorAction.RaiseLayer:
                _layers[x, y] = System.Math.Min(MapValidator.MaxLayer, _layers[x, y] + 1);
                break;

            case EditorAction.LowerLayer:
                _layers[x, y] = System.Math.Max(0, _layers[x, y] - 1);
                break;

            case EditorAction.ToggleRamp when IsFloor(x, y):
                _ramps[x, y] = !_ramps[x, y];
                break;

            case EditorAction.RotateCell when !IsFloor(x, y):
                // A quarter turn clockwise per click; cosmetic, so any placed item rotates freely.
                _orientations[x, y] = (_orientations[x, y] + 1) % 4;
                break;

            case EditorAction.Erase:
                _materials[x, y] = CellMaterial.Floor;
                _bushes[x, y] = false;
                _sandbags[x, y] = false;
                _layers[x, y] = 0;
                _ramps[x, y] = false;
                _orientations[x, y] = 0;
                _enemySpawns.Remove((x, y));
                _powerupSpawns.RemoveAll(p => p.X == x && p.Y == y);
                RemoveTeleportPadAt(x, y);
                break;
        }
    }

    /// <summary>The whole-arena ground tileset the author picked (owner feedback 2026-06-11).</summary>
    public GroundTheme GroundTheme { get; set; } = GroundTheme.Sand;

    public MapDefinition ToMap() => new(
        Name,
        (CellMaterial[,])_materials.Clone(),
        (bool[,])_bushes.Clone(),
        (bool[,])_sandbags.Clone(),
        _playerSpawn,
        _enemySpawns.ToList(),
        _powerupSpawns.ToList(),
        _teleportPads.ToList(),
        HasElevation() ? (int[,])_layers.Clone() : null,
        HasElevation() ? (bool[,])_ramps.Clone() : null,
        GroundTheme,
        HasRotation() ? (int[,])_orientations.Clone() : null);

    // An unrotated map keeps the lean document: no orientations key at all.
    private bool HasRotation()
    {
        foreach (var facing in _orientations)
        {
            if (facing != 0)
            {
                return true;
            }
        }

        return false;
    }

    // An untouched (flat) map keeps the lean pre-elevation document: no layers/ramps keys at all.
    private bool HasElevation()
    {
        foreach (var layer in _layers)
        {
            if (layer != 0)
            {
                return true;
            }
        }

        foreach (var ramp in _ramps)
        {
            if (ramp)
            {
                return true;
            }
        }

        return false;
    }

    public MapValidationResult Validate() => MapValidator.Validate(ToMap());

    private void TogglePowerup(int x, int y)
    {
        var existing = _powerupSpawns.FindIndex(p => p.X == x && p.Y == y);
        if (existing >= 0)
        {
            var sameKind = _powerupSpawns[existing].Kind == PaintPowerup;
            _powerupSpawns.RemoveAt(existing);
            if (sameKind)
            {
                return; // clicking the same kind again clears the cell
            }
        }

        _powerupSpawns.Add(new PowerupSpawn(PaintPowerup, x, y));
    }

    // Two-click placement: the first click parks a pending pad, the second partners it into a link. Clicking
    // a cell already part of a link removes that whole link; clicking the pending cell again cancels it.
    private void PlaceTeleportPad(int x, int y)
    {
        if (RemoveTeleportPadAt(x, y))
        {
            return;
        }

        if (_pendingTeleportPad is { } pending)
        {
            if (pending == (x, y))
            {
                _pendingTeleportPad = null; // clicking the pending pad again cancels it
                return;
            }

            _teleportPads.Add(new TeleportPadLink(pending.X, pending.Y, x, y));
            _pendingTeleportPad = null;
            return;
        }

        _pendingTeleportPad = (x, y);
    }

    private bool RemoveTeleportPadAt(int x, int y)
    {
        if (_pendingTeleportPad == (x, y))
        {
            _pendingTeleportPad = null;
            return true;
        }

        return _teleportPads.RemoveAll(p => (p.AX == x && p.AY == y) || (p.BX == x && p.BY == y)) > 0;
    }

    private bool IsInterior(int x, int y) => x > 0 && y > 0 && x < Width - 1 && y < Height - 1;

    private bool IsFloor(int x, int y) => _materials[x, y] == CellMaterial.Floor;
}
