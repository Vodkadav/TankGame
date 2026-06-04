using System.Collections.Generic;
using Godot;
using TankGame.Domain;
using TankGame.Domain.Net;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>The networked play scene: the client half of the authoritative match (M3). It loads the
/// same <see cref="Battlefield01"/> map both sides share, then each frame pumps the transport, sends
/// the local player's intent, predicts the local tank, and mirrors the server. A <c>WelcomeFrame</c>
/// tells it which slot it drives — that tank renders from a <see cref="PredictedTank"/> (no input
/// lag, reconciled to authority); every other slot renders straight from each snapshot. Authoritative
/// <c>WallDelta</c>s are applied to the shared grid so a brick broken on the server breaks for both.
///
/// "Client sends intent, server resolves outcome": this scene never decides a hit, a break, or a
/// death — it only sends inputs and draws what the snapshots say. The transport comes from
/// <see cref="NetworkSession.Active"/> (set by the title screen's Join). Verifiable in full only on
/// two devices; the headless test drives <see cref="OnWelcome"/>/<see cref="OnSnapshot"/>/<see cref="Tick"/>
/// against a fake transport.</summary>
public partial class NetArenaScene : Node2D
{
    private const float TileSize = 64f;
    private const byte HostSlot = 0;
    private static readonly NVector2 GridOrigin = NVector2.Zero;

    // The guest spawns at the same cell the local game's Player 2 does, mirroring the server sim.
    private static readonly (int X, int Y) GuestSpawn = (25, 7);

    private readonly Dictionary<byte, NetTank> _tanks = new();
    private readonly Dictionary<byte, TankView> _views = new();

    private IMatchTransport _transport = null!;
    private WallGrid _grid = null!;
    private GridArena _arena = null!;
    private IInputSource _input = null!;
    private Camera2D _camera = null!;
    private LevelMap _level = null!;

    private byte? _localSlot;
    private PredictedTank? _predicted;
    private uint _inputSeq;

    /// <summary>The slot the server assigned this client, or null before the welcome arrives.</summary>
    public byte? LocalSlot => _localSlot;

    /// <summary>The per-slot view-model tanks (the bound models the views mirror) — for tests.</summary>
    public IReadOnlyDictionary<byte, NetTank> Tanks => _tanks;

    /// <summary>The current state of a grid cell — lets a test assert an applied wall delta.</summary>
    public WallCell CellAt(int x, int y) => _grid.GetCell(x, y);

    public override void _Ready()
    {
        _transport = NetworkSession.Active
            ?? throw new System.InvalidOperationException("NetArenaScene entered without an active session.");

        _level = LevelMap.Parse(Battlefield01.Text);
        _grid = _level.BuildGrid();
        _arena = new GridArena(_grid, TileSize, GridOrigin);

        var wallView = new WallGridView { Name = "WallGridView", RenderTileSize = (int)TileSize };
        AddChild(wallView);
        wallView.Bind(_grid);

        _camera = new Camera2D { Name = "GameCamera", ProcessCallback = Camera2D.Camera2DProcessCallback.Physics };
        AddChild(_camera);

        _input = new KeyboardMouseInputSource(GetViewport(), fireOnClick: true);

        _transport.WelcomeReceived += OnWelcome;
        _transport.SnapshotReceived += OnSnapshot;
    }

    public override void _Process(double delta) => Tick((float)delta);

    /// <summary>One networked frame: pump the transport, then (once welcomed) send the local intent,
    /// predict the local tank, and keep its view-model and the camera on the prediction. Public so a
    /// test can step it without relying on engine frame timing.</summary>
    public void Tick(float delta)
    {
        _transport.Poll();

        if (_localSlot is not byte slot || _predicted is null)
        {
            return; // not yet welcomed — nothing to send or predict
        }

        var frame = ReadInputFrame();
        _transport.SendInput(frame);
        _predicted.Predict(frame);
        SyncLocalFromPrediction(slot);

        _camera.Position = new Vector2(_predicted.Position.X, _predicted.Position.Y);
    }

    // The server welcomed us: remember the slot and start predicting our tank from its spawn (the
    // first snapshot reconciles it to the authoritative position).
    private void OnWelcome(byte slot)
    {
        _localSlot = slot;
        var (cx, cy) = slot == HostSlot ? (_level.SpawnX, _level.SpawnY) : GuestSpawn;
        _predicted = new PredictedTank(slot, _arena, CellCentre(cx, cy));
        EnsureTank(slot);
        SyncLocalFromPrediction(slot);
    }

    // An authoritative snapshot: reconcile our prediction, mirror every other tank straight from the
    // snapshot, and apply the wall changes to the shared grid.
    private void OnSnapshot(SnapshotFrame snapshot)
    {
        _predicted?.Reconcile(snapshot);

        foreach (var state in snapshot.Tanks)
        {
            if (state.Slot == _localSlot)
            {
                SyncLocalFromPrediction(state.Slot);
            }
            else
            {
                var tank = EnsureTank(state.Slot);
                tank.Position = new NVector2(state.X, state.Y);
                tank.Rotation = state.Rotation;
                tank.TurretRotation = state.TurretRotation;
                tank.Hp = state.Hp;
                tank.Team = state.Team;
            }
        }

        foreach (var delta in snapshot.WallDeltas)
        {
            _grid.SetCell(delta.CellX, delta.CellY, new WallCell((CellMaterial)delta.Material, delta.Hp));
        }
    }

    private void SyncLocalFromPrediction(byte slot)
    {
        if (_predicted is null)
        {
            return;
        }

        var tank = EnsureTank(slot);
        tank.Position = _predicted.Position;
        tank.Rotation = _predicted.Rotation;
        tank.TurretRotation = _predicted.TurretRotation;
        tank.Hp = _predicted.Hp;
        tank.Team = _predicted.Team;
    }

    private NetTank EnsureTank(byte slot)
    {
        if (_tanks.TryGetValue(slot, out var existing))
        {
            return existing;
        }

        var tank = new NetTank();
        _tanks[slot] = tank;

        var view = GD.Load<PackedScene>("res://src/Presentation/Tank/TankView.tscn").Instantiate<TankView>();
        view.Name = $"TankView{slot}";
        view.Bind(tank);
        if (slot != _localSlot)
        {
            view.Modulate = new Color(1f, 0.5f, 0.5f); // tint the opponent so the two read apart
        }

        AddChild(view);
        _views[slot] = view;
        return tank;
    }

    private InputFrame ReadInputFrame()
    {
        var input = _input.Read();
        var buttons = input.Fire ? InputFrame.FireBit : (byte)0;
        return new InputFrame(++_inputSeq, input.Move.X, input.Move.Y, input.Aim, buttons);
    }

    private static NVector2 CellCentre(int x, int y) =>
        new(GridOrigin.X + ((x + 0.5f) * TileSize), GridOrigin.Y + ((y + 0.5f) * TileSize));
}
