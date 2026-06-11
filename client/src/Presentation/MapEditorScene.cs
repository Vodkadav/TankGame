using System;
using Godot;
using TankGame.Domain;
using TankGame.GameLogic;
using TankGame.Infrastructure;

namespace TankGame.Presentation;

/// <summary>The WYSIWYG map editor (ADR-0020, Wave A PR3). Edits a pure <see cref="MapEditor"/> under a
/// top-down orthographic camera over the real <see cref="Terrain3DView"/>, so painted cells show as the
/// actual game meshes. The palette picks what a click does (paint a material, toggle a bush/sandbag, place
/// the player/enemy/powerup spawns, erase); spawns are drawn as billboard gizmos. Validate/Test-Play/Save
/// run against the same <see cref="MapValidator"/>/<see cref="MapRepository"/> the play build uses. All the
/// editing rules live in <see cref="MapEditor"/>; this scene is the view and the input.</summary>
public partial class MapEditorScene : Node3D
{
    public const string MapEditorScenePath = "res://src/Presentation/MapEditor.tscn";
    private const string TitleScenePath = "res://src/Presentation/Title.tscn";
    private const string MapsDir = "user://maps";

    private const float TileSize = 64f;
    private const float MinOrthoSize = 200f;
    private const float MaxOrthoSize = 4000f;
    private const float ZoomStep = 1.12f;

    private static readonly PowerupKind[] PowerupKinds =
    {
        PowerupKind.SpeedBoost, PowerupKind.RapidFire, PowerupKind.BouncingAmmo, PowerupKind.SpreadAmmo,
        PowerupKind.Repair, PowerupKind.Shield, PowerupKind.PiercingAmmo, PowerupKind.Missile, PowerupKind.Telephone,
    };

    private MapEditor _editor = new("New Map", 28, 16);
    private MapRepository _maps = null!;
    private Camera3D _camera = null!;

    // A free orbiting camera (owner follow-up 2026-06-11): defaults to the game's ¾ isometric angle
    // (top-down made walls read as flat squares), right-drag orbits/tilts, middle-drag pans — so the
    // author can look around the map while building it.
    private float _camYawDeg = CamDefaultYawDeg;
    private float _camPitchDeg = CamDefaultPitchDeg;
    private Vector3 _camTarget;
    private bool _orbiting;
    private bool _panning;

    private const float CamDefaultYawDeg = 45f;
    private const float CamDefaultPitchDeg = 52f; // the match camera's angle, so editing looks like playing
    private const float CamDistance = 5000f;
    private const float OrbitDegreesPerPixel = 0.35f;
    private const float MinPitchDeg = 15f;  // never flat enough to lose the ground
    private const float MaxPitchDeg = 89f;  // and straight-down stays reachable for blocking out
    private Terrain3DView? _terrain;
    private Node3D? _gizmos;
    private MeshInstance3D? _ground;
    private PanelContainer _floorThemes = null!;
    private LineEdit _nameEdit = null!;
    private Label _status = null!;
    private bool _painting;

    /// <summary>Stamps the whole-arena ground tileset and selects floor painting — the WYSIWYG
    /// ground re-tints immediately. Public so tests can drive it.</summary>
    public void SelectGroundTheme(GroundTheme theme)
    {
        _editor.GroundTheme = theme;
        SelectMaterial(CellMaterial.Floor);
        _floorThemes.Visible = false;
        RefreshScene();
    }

    public string MapName
    {
        get => _editor.Name;
        set
        {
            _editor.Name = value;
            _nameEdit.Text = value;
        }
    }

    public override void _Ready()
    {
        _maps = new MapRepository(ProjectSettings.GlobalizePath(MapsDir));
        BuildEnvironment();
        BuildUi();
        NewMap(28, 16);
    }

    // ---- public surface (palette/buttons drive these; tests drive them too) ----

    public void NewMap(int width, int height)
    {
        _editor = new MapEditor(_nameEdit.Text, width, height);
        FrameCamera();
        RefreshScene();
    }

    /// <summary>Re-sizes the current map, keeping everything that fits (owner follow-up 2026-06-11)
    /// — the size buttons resize, they do not start over.</summary>
    public void ResizeMap(int width, int height)
    {
        _editor.Resize(width, height);
        FrameCamera();
        RefreshScene();
    }

    public void SelectMaterial(CellMaterial material)
    {
        _editor.Action = EditorAction.PaintMaterial;
        _editor.PaintMaterial = material;
    }

    public void SelectAction(EditorAction action) => _editor.Action = action;

    public void SelectPowerup(PowerupKind kind)
    {
        _editor.Action = EditorAction.TogglePowerup;
        _editor.PaintPowerup = kind;
    }

    public void Paint(int x, int y)
    {
        _editor.ApplyAt(x, y);
        RefreshScene();
    }

    public MapDefinition CurrentMap() => _editor.ToMap();

    /// <summary>Saves the map if it is valid; returns false (and shows the problem) when it is not.</summary>
    public bool Save()
    {
        var result = _editor.Validate();
        if (!result.IsValid)
        {
            ShowInvalid(result);
            return false;
        }

        _maps.Save(_editor.ToMap());
        _status.Text = TranslationServer.Translate("editor.saved");
        return true;
    }

    public void TestPlay()
    {
        var result = _editor.Validate();
        if (!result.IsValid)
        {
            ShowInvalid(result);
            return;
        }

        GameSetup.StartNewMatch(GameMode.OnePlayer);
        GameSetup.CustomMap = _editor.ToMap();
        Go(TitleScene.ArenaScenePath);
    }

    // ---- input ----

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton click)
        {
            if (click.ButtonIndex == MouseButton.WheelUp)
            {
                _camera.Size = Mathf.Clamp(_camera.Size / ZoomStep, MinOrthoSize, MaxOrthoSize);
            }
            else if (click.ButtonIndex == MouseButton.WheelDown)
            {
                _camera.Size = Mathf.Clamp(_camera.Size * ZoomStep, MinOrthoSize, MaxOrthoSize);
            }
            else if (click.ButtonIndex == MouseButton.Left)
            {
                _painting = click.Pressed;
                if (click.Pressed)
                {
                    PaintAtScreen(click.Position);
                }
            }
            else if (click.ButtonIndex == MouseButton.Right)
            {
                _orbiting = click.Pressed; // hold right and drag to orbit/tilt around the map
            }
            else if (click.ButtonIndex == MouseButton.Middle)
            {
                _panning = click.Pressed; // hold middle and drag to slide the view
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            if (_painting)
            {
                PaintAtScreen(motion.Position);
            }
            else if (_orbiting)
            {
                _camYawDeg -= motion.Relative.X * OrbitDegreesPerPixel;
                _camPitchDeg = Mathf.Clamp(
                    _camPitchDeg + (motion.Relative.Y * OrbitDegreesPerPixel), MinPitchDeg, MaxPitchDeg);
                ApplyCamera();
            }
            else if (_panning)
            {
                // Slide along the camera's screen axes projected onto the ground, scaled so a pixel of
                // mouse is about a pixel of world at the current zoom.
                var perPixel = _camera.Size / GetViewport().GetVisibleRect().Size.Y;
                var basis = _camera.GlobalTransform.Basis;
                var right = Flatten(basis.X);
                var forward = Flatten(-basis.Z);
                _camTarget += ((right * -motion.Relative.X) + (forward * motion.Relative.Y)) * perPixel;
                ApplyCamera();
            }
        }
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.Y = 0f;
        return v.LengthSquared() > 1e-6f ? v.Normalized() : Vector3.Zero;
    }

    private void PaintAtScreen(Vector2 screen)
    {
        if (CellAtScreen(screen) is var (x, y))
        {
            Paint(x, y);
        }
    }

    // Casts the camera ray onto the ground plane (y = 0) and returns the cell under it, or null if the
    // ray misses the field.
    private (int X, int Y)? CellAtScreen(Vector2 screen)
    {
        var from = _camera.ProjectRayOrigin(screen);
        var dir = _camera.ProjectRayNormal(screen);
        if (Mathf.Abs(dir.Y) < 1e-6f)
        {
            return null;
        }

        var t = -from.Y / dir.Y;
        if (t < 0f)
        {
            return null;
        }

        var hit = from + (dir * t);
        var cx = (int)Mathf.Floor(hit.X / TileSize);
        var cy = (int)Mathf.Floor(hit.Z / TileSize);
        if (cx < 0 || cy < 0 || cx >= _editor.Width || cy >= _editor.Height)
        {
            return null;
        }

        return (cx, cy);
    }

    // ---- scene refresh ----

    private void RefreshScene()
    {
        var map = _editor.ToMap();

        // Free immediately, not deferred: a drag-paint refreshes every mouse event, and deferred frees
        // would pile dead terrains into the tree until the frame ends (and confuse FindChild).
        _terrain?.Free();
        _terrain = new Terrain3DView { Name = "Terrain3DView" };
        AddChild(_terrain);
        // Layers/ramps ride along so raised cells show as real plateau blocks and ramp wedges live
        // (ADR-0020 Wave B) — the same WYSIWYG meshes the match renders.
        _terrain.Bind(
            WallGrid.FromMaterials(map.Materials, map.Layers, map.Ramps, map.Orientations),
            map.Bushes, map.Sandbags, TileSize);

        // The authored ground tileset under everything, sized to the map — WYSIWYG with play.
        _ground?.Free();
        _ground = new MeshInstance3D
        {
            Name = "Ground",
            Mesh = new PlaneMesh { Size = new Vector2(_editor.Width * TileSize, _editor.Height * TileSize) },
            Position = new Vector3(_editor.Width * TileSize / 2f, -0.5f, _editor.Height * TileSize / 2f),
            MaterialOverride = GroundThemes.Material(_editor.GroundTheme, _editor.Width, _editor.Height),
        };
        AddChild(_ground);

        _gizmos?.Free();
        _gizmos = new Node3D { Name = "Gizmos" };
        AddChild(_gizmos);
        // Numbered spawn markers (owner feedback 2026-06-11): the player is "1", each enemy takes
        // the next number, so a creator can track which of the up-to-8 tank spawns is which.
        AddMarker(_editor.PlayerSpawn.X, _editor.PlayerSpawn.Y, "1", new Color(0.4f, 1f, 0.4f));
        var spawnNumber = 2;
        foreach (var (x, y) in _editor.EnemySpawns)
        {
            AddMarker(x, y, spawnNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new Color(1f, 0.4f, 0.4f));
            spawnNumber++;
        }

        foreach (var spawn in _editor.PowerupSpawns)
        {
            AddMarker(spawn.X, spawn.Y, "★", new Color(1f, 0.9f, 0.3f));
        }

        var padColour = new Color(0.3f, 0.85f, 1f); // teleport cyan, mirrors TeleportPad3DView
        var linkIndex = 0;
        foreach (var pad in _editor.TeleportPads)
        {
            AddMarker(pad.AX, pad.AY, "T", padColour);
            AddMarker(pad.BX, pad.BY, "T", padColour);

            // The pairing reads as a live connection: a pulsing dotted line between the two ends.
            var link = new TeleportLinkLine { Name = $"PadLink{linkIndex}" };
            link.Configure(
                new Vector3((pad.AX + 0.5f) * TileSize, 0f, (pad.AY + 0.5f) * TileSize),
                new Vector3((pad.BX + 0.5f) * TileSize, 0f, (pad.BY + 0.5f) * TileSize));
            _gizmos!.AddChild(link);
            linkIndex++;
        }

        if (_editor.PendingTeleportPad is { } pending)
        {
            AddMarker(pending.X, pending.Y, "T?", padColour); // a half-placed pad awaiting its partner
        }
    }

    private void AddMarker(int x, int y, string glyph, Color colour)
    {
        _gizmos!.AddChild(new Label3D
        {
            Text = glyph,
            Modulate = colour,
            OutlineModulate = new Color(0f, 0f, 0f),
            OutlineSize = 8,
            FontSize = 96,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = new Vector3((x + 0.5f) * TileSize, 50f, (y + 0.5f) * TileSize),
        });
    }

    private void FrameCamera()
    {
        _camTarget = new Vector3(_editor.Width * TileSize / 2f, 0f, _editor.Height * TileSize / 2f);
        _camera.Size = Mathf.Max(_editor.Width, _editor.Height) * TileSize * 0.8f; // the ¾ view needs the diagonal
        ApplyCamera();
    }

    // Seats the camera on its orbit sphere around the target and aims it. The ortho size (zoom) is
    // independent of the orbit, so panning/tilting never changes how much map is on screen.
    private void ApplyCamera()
    {
        var pitch = Mathf.DegToRad(_camPitchDeg);
        var yaw = Mathf.DegToRad(_camYawDeg);
        var dir = new Vector3(
            Mathf.Cos(pitch) * Mathf.Sin(yaw),
            Mathf.Sin(pitch),
            Mathf.Cos(pitch) * Mathf.Cos(yaw));
        _camera.Position = _camTarget + (dir * CamDistance);
        _camera.LookAt(_camTarget, Vector3.Up);
    }

    private void ShowInvalid(MapValidationResult result) =>
        _status.Text = TranslationServer.Translate("editor.invalid") + $" ({result.Errors.Count})";

    // ---- build ----

    private void BuildEnvironment()
    {
        _camera = new Camera3D
        {
            Name = "EditorCamera",
            Projection = Camera3D.ProjectionType.Orthogonal,
            Far = 20000f,
            Near = 1f,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
        };
        AddChild(_camera);

        AddChild(new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-70f, -30f, 0f),
            LightEnergy = 1.3f,
        });

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.18f, 0.2f, 0.24f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.75f, 0.77f, 0.82f),
                AmbientLightEnergy = 1f,
                TonemapMode = Godot.Environment.ToneMapper.Aces,
            },
        });
    }

    private void BuildUi()
    {
        var layer = new CanvasLayer { Name = "EditorUi" };
        AddChild(layer);

        // The palette outgrew small windows, so it lives in a full-height scrolling strip on the left —
        // every tool stays reachable at any window size (owner feedback 2026-06-11).
        var scroll = new ScrollContainer
        {
            Name = "PaletteScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.AnchorTop = 0f;
        scroll.AnchorBottom = 1f;
        scroll.OffsetLeft = 12f;
        scroll.OffsetTop = 12f;
        scroll.OffsetRight = 212f;
        scroll.OffsetBottom = -56f; // stops above the bottom action bar
        layer.AddChild(scroll);

        var palette = new VBoxContainer
        {
            Name = "Palette",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        palette.AddThemeConstantOverride("separation", 4);
        palette.AddChild(new Label { Text = "editor.heading" });

        var sizes = new HBoxContainer { Name = "Sizes" };
        sizes.AddChild(SizeButton("SizeSmall", "editor.size_small", 20, 12));
        sizes.AddChild(SizeButton("SizeMedium", "editor.size_medium", 28, 16));
        sizes.AddChild(SizeButton("SizeLarge", "editor.size_large", 40, 24));
        palette.AddChild(sizes);

        // Floor doubles as the ground-theme picker (owner feedback 2026-06-11): pressing it opens
        // the tileset list. The list flies out BESIDE the palette (owner follow-up) — expanding
        // inline shoved every tool below it off the screen.
        palette.AddChild(ToolButton("Floor", "editor.floor", () =>
        {
            SelectMaterial(CellMaterial.Floor);
            _floorThemes.Visible = !_floorThemes.Visible;
        }));
        _floorThemes = new PanelContainer { Name = "FloorThemes", Visible = false };
        _floorThemes.Position = new Vector2(224f, 48f); // beside the palette strip, near its Floor button
        var themeList = new VBoxContainer();
        themeList.AddThemeConstantOverride("separation", 4);
        foreach (var (theme, key) in new[]
        {
            (GroundTheme.Sand, "editor.theme_sand"), (GroundTheme.Jungle, "editor.theme_jungle"),
            (GroundTheme.Mars, "editor.theme_mars"), (GroundTheme.ParkingLot, "editor.theme_parkinglot"),
        })
        {
            var t = theme;
            themeList.AddChild(ToolButton($"Theme{theme}", key, () => SelectGroundTheme(t)));
        }

        _floorThemes.AddChild(themeList);
        layer.AddChild(_floorThemes);

        foreach (var (material, key) in new[]
        {
            (CellMaterial.Brick, "editor.brick"),
            (CellMaterial.Crate, "editor.crate"), (CellMaterial.Steel, "editor.steel"),
            (CellMaterial.Water, "editor.water"), (CellMaterial.Bridge, "editor.bridge"),
            (CellMaterial.Mountain, "editor.mountain"), (CellMaterial.Building, "editor.building"),
        })
        {
            var m = material;
            palette.AddChild(ToolButton(material.ToString(), key, () => SelectMaterial(m)));
        }

        palette.AddChild(ToolButton("Bush", "editor.bush", () => SelectAction(EditorAction.ToggleBush)));
        palette.AddChild(ToolButton("Sandbag", "editor.sandbag", () => SelectAction(EditorAction.ToggleSandbag)));
        palette.AddChild(ToolButton("Player", "editor.player", () => SelectAction(EditorAction.SetPlayerSpawn)));
        palette.AddChild(ToolButton("Enemy", "editor.enemy", () => SelectAction(EditorAction.ToggleEnemySpawn)));
        palette.AddChild(ToolButton("TeleportPad", "editor.teleport", () => SelectAction(EditorAction.PlaceTeleportPad)));
        palette.AddChild(ToolButton("RaiseLayer", "editor.raise", () => SelectAction(EditorAction.RaiseLayer)));
        palette.AddChild(ToolButton("LowerLayer", "editor.lower", () => SelectAction(EditorAction.LowerLayer)));
        palette.AddChild(ToolButton("Ramp", "editor.ramp", () => SelectAction(EditorAction.ToggleRamp)));
        palette.AddChild(ToolButton("Rotate", "editor.rotate", () => SelectAction(EditorAction.RotateCell)));
        palette.AddChild(ToolButton("Erase", "editor.erase", () => SelectAction(EditorAction.Erase)));

        var powerups = new OptionButton { Name = "PowerupKind" };
        foreach (var kind in PowerupKinds)
        {
            powerups.AddItem(TranslationServer.Translate(PickupFloater.LabelKeyFor(kind)));
        }

        powerups.ItemSelected += index => SelectPowerup(PowerupKinds[index]);
        palette.AddChild(ToolButton("Powerup", "editor.powerup", () => SelectPowerup(PowerupKinds[Mathf.Max(0, powerups.Selected)])));
        palette.AddChild(powerups);
        scroll.AddChild(palette);

        var bottom = new HBoxContainer { Name = "Actions" };
        bottom.AddThemeConstantOverride("separation", 8);
        bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        // Grow UP from the bottom edge — the preset alone grows downward, which pushed the buttons
        // below the window (owner follow-up 2026-06-11).
        bottom.GrowVertical = Control.GrowDirection.Begin;
        bottom.Position += new Vector2(12f, -12f);
        _nameEdit = new LineEdit { Name = "MapName", Text = _editor.Name, CustomMinimumSize = new Vector2(180f, 0f) };
        _nameEdit.TextChanged += text => _editor.Name = text;
        bottom.AddChild(_nameEdit);
        bottom.AddChild(ActionButton("Validate", "editor.validate", () => ShowValidation()));
        bottom.AddChild(ActionButton("TestPlay", "editor.test_play", TestPlay));
        bottom.AddChild(ActionButton("Save", "editor.save", () => Save()));
        bottom.AddChild(ActionButton("Back", "editor.back", () => Go(TitleScenePath)));
        _status = new Label { Name = "Status" };
        bottom.AddChild(_status);
        layer.AddChild(bottom);
    }

    private void ShowValidation()
    {
        var result = _editor.Validate();
        _status.Text = result.IsValid
            ? TranslationServer.Translate("editor.valid")
            : TranslationServer.Translate("editor.invalid") + $" ({result.Errors.Count})";
    }

    private Button SizeButton(string name, string key, int w, int h)
    {
        var button = new Button { Name = name, Text = key };
        button.Pressed += () => ResizeMap(w, h);
        return button;
    }

    private static Button ToolButton(string name, string key, Action onPressed)
    {
        var button = new Button { Name = name, Text = key };
        button.Pressed += onPressed;
        return button;
    }

    private static Button ActionButton(string name, string key, Action onPressed)
    {
        var button = new Button { Name = name, Text = key };
        button.Pressed += onPressed;
        return button;
    }

    // Guarded so the GoDotTest click-path (scene added as a child, not the active scene) can drive the
    // wiring without the runner swapping its whole scene out from under it.
    private void Go(string scenePath)
    {
        if (GetTree().CurrentScene == this)
        {
            GetTree().ChangeSceneToFile(scenePath);
        }
    }
}
