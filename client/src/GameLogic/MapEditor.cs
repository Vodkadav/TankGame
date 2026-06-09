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
    TogglePowerup,
    Erase,
}

/// <summary>The mutable state behind the map editor — all the editing rules with no Godot, so the scene
/// is a thin view over it. Starts as a steel-bordered floor arena; the outer ring is fixed (a click on a
/// border cell is ignored), bushes/sandbags/spawns only sit on interior floor. <see cref="ToMap"/> hands
/// the current state to <see cref="MapCodec"/>/<see cref="MapValidator"/> as a <see cref="MapDefinition"/>.
/// </summary>
public sealed class MapEditor
{
    private readonly CellMaterial[,] _materials;
    private readonly bool[,] _bushes;
    private readonly bool[,] _sandbags;
    private readonly List<(int X, int Y)> _enemySpawns = new();
    private readonly List<PowerupSpawn> _powerupSpawns = new();
    private (int X, int Y) _playerSpawn = (1, 1);

    public MapEditor(string name, int width, int height)
    {
        Name = name;
        var blank = MapDefinition.CreateBlank(name, width, height);
        _materials = blank.Materials;
        _bushes = blank.Bushes;
        _sandbags = blank.Sandbags;
        Width = width;
        Height = height;
    }

    public string Name { get; set; }

    public int Width { get; }

    public int Height { get; }

    public EditorAction Action { get; set; } = EditorAction.PaintMaterial;

    public CellMaterial PaintMaterial { get; set; } = CellMaterial.Brick;

    public PowerupKind PaintPowerup { get; set; } = PowerupKind.Repair;

    public (int X, int Y) PlayerSpawn => _playerSpawn;

    public IReadOnlyList<(int X, int Y)> EnemySpawns => _enemySpawns;

    public IReadOnlyList<PowerupSpawn> PowerupSpawns => _powerupSpawns;

    public CellMaterial MaterialAt(int x, int y) => _materials[x, y];

    public bool BushAt(int x, int y) => _bushes[x, y];

    public bool SandbagAt(int x, int y) => _sandbags[x, y];

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
                if (!_enemySpawns.Remove((x, y)))
                {
                    _enemySpawns.Add((x, y));
                }

                break;

            case EditorAction.TogglePowerup when IsFloor(x, y):
                TogglePowerup(x, y);
                break;

            case EditorAction.Erase:
                _materials[x, y] = CellMaterial.Floor;
                _bushes[x, y] = false;
                _sandbags[x, y] = false;
                _enemySpawns.Remove((x, y));
                _powerupSpawns.RemoveAll(p => p.X == x && p.Y == y);
                break;
        }
    }

    public MapDefinition ToMap() => new(
        Name,
        (CellMaterial[,])_materials.Clone(),
        (bool[,])_bushes.Clone(),
        (bool[,])_sandbags.Clone(),
        _playerSpawn,
        _enemySpawns.ToList(),
        _powerupSpawns.ToList());

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

    private bool IsInterior(int x, int y) => x > 0 && y > 0 && x < Width - 1 && y < Height - 1;

    private bool IsFloor(int x, int y) => _materials[x, y] == CellMaterial.Floor;
}
