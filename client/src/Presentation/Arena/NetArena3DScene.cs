using System.Collections.Generic;
using Godot;
using TankGame.Domain;
using TankGame.Domain.Net;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>The 3D networked play scene (ADR-0019 step 3): the welcome's slot decides the role.
/// The HOST (slot 0) is the authority — it runs the REAL <see cref="World"/> (the same GameLogic as
/// single-player) with its own tank on keyboard/mouse and the guest's tank on a
/// <see cref="RelayedInputSource"/>, and a <see cref="HostSession"/> broadcasts a snapshot per fixed
/// 20 Hz tick. A GUEST predicts its own tank (<see cref="PredictedTank"/>), mirrors every other slot
/// straight from the snapshots into <see cref="NetTank"/> view-models, and applies authoritative wall
/// deltas to the shared grid. Both roles render the shared <see cref="Battlefield01"/> map with the
/// same 3D views the local arena uses. "Client sends intent, the host resolves outcome."</summary>
public partial class NetArena3DScene : Node3D
{
    private const float TileSize = 64f;
    private const float TickSeconds = 1f / 20f; // the fixed authoritative tick (PredictedTank's cadence)

    // Match PredictedTank's movement model (200 u/s) so guest prediction replays the host's real
    // Tank faithfully; combat numbers mirror Arena3DScene.
    private const float TankSpeed = 200f;
    private const float FireInterval = 0.3f;
    private const float ProjectileSpeed = 600f;
    private const float CombatHitRadius = 28f;
    private const int TankMaxHp = 8;
    private const byte HostSlot = 0;

    // The guest spawns where the local game's Player 2 does (mirrors the 2D net scene).
    private static readonly (int X, int Y) GuestSpawn = (25, 7);
    private static readonly NVector2 GridOrigin = NVector2.Zero;

    // Camera: the same eyeballed ¾ ortho as Arena3DScene.
    private const float CamPitchDeg = 52f;
    private const float CamYawDeg = 45f;
    private const float CamDistance = 2500f;
    private const float CamOrthoSize = 820f;

    private IMatchTransport _transport = null!;
    private LevelMap _level = null!;
    private WallGrid _grid = null!;
    private GridArena _arena = null!;
    private Camera3D _camera = null!;
    private NetStatusOverlay _status = null!;

    private readonly Dictionary<byte, ITank> _tanks = new();
    private readonly Dictionary<byte, Tank3DView> _tankViews = new();
    private float _accumulator;
    private byte? _localSlot;

    // Host role: the authoritative world and its session.
    private World? _world;
    private HostSession? _session;
    private IInputSource? _hostInput;

    // Guest role: prediction for the local tank, an input seq counter for the frames sent up.
    private PredictedTank? _predicted;
    private IInputSource? _guestLocalInput;
    private uint _inputSeq;

    // The local player's input — twin-stick touch on a phone (the shipped APK), keyboard/mouse on
    // desktop. One overlay serves whichever role this client took; tests report no touchscreen.
    private TouchControls? _touch;

    private IInputSource BuildLocalInput()
    {
        if (!DisplayServer.IsTouchscreenAvailable())
        {
            return new KeyboardMouse3DInputSource(_camera);
        }

        _touch = new TouchControls { Name = "TouchControls" };
        var layer = new CanvasLayer { Name = "TouchControlsLayer" };
        layer.AddChild(_touch);
        AddChild(layer);
        return new TouchInput3DSource(_camera, () => _touch.MoveOutput, () => _touch.AimOutput);
    }

    // Guest role: the shots mirrored from the latest snapshot (ADR-0019 step 4) — the guest has no
    // world, so each snapshot rebuilds this set of throwaway projectile views.
    private readonly List<Projectile3DView> _snapshotProjectiles = new();

    /// <summary>The slot the relay assigned this client, or null before the welcome.</summary>
    public byte? LocalSlot => _localSlot;

    /// <summary>Each known slot's tank — the host's real <see cref="Tank"/>s, a guest's mirrored
    /// view-models. For the camera, the views, and the tests.</summary>
    public IReadOnlyDictionary<byte, ITank> Tanks => _tanks;

    /// <summary>The current state of a shared-grid cell — lets a test assert an applied wall delta.</summary>
    public WallCell CellAt(int x, int y) => _grid.GetCell(x, y);

    /// <summary>How many shots the guest is currently mirroring from the latest snapshot (ADR-0019
    /// step 4). Zero on the host, which renders its own shots from the world. For the tests.</summary>
    public int MirroredProjectileCount => _snapshotProjectiles.Count;

    public override void _Ready()
    {
        _transport = NetworkSession.Active
            ?? throw new System.InvalidOperationException("NetArena3DScene entered without an active session.");

        _level = LevelMap.Parse(Battlefield01.Text);
        _grid = _level.BuildGrid();
        _arena = new GridArena(_grid, TileSize, GridOrigin);

        BuildEnvironment();
        BuildGround();

        var terrain = new Terrain3DView { Name = "Terrain3DView" };
        AddChild(terrain);
        terrain.Bind(_grid, _level.Bushes, new bool[_level.Width, _level.Height], TileSize);

        _status = new NetStatusOverlay { Name = "NetStatusOverlay" };
        AddChild(_status); // shows "Connecting…" until the welcome arrives

        _transport.WelcomeReceived += OnWelcome;
        _transport.SnapshotReceived += OnSnapshot;
    }

    public override void _Process(double delta) => Tick((float)delta);

    /// <summary>One networked frame: pump the transport and run any due fixed ticks (20 Hz, the
    /// snapshot/input cadence both roles share). Public so tests can step it deterministically.</summary>
    public void Tick(float delta)
    {
        _transport.Poll();

        if (_localSlot is null)
        {
            return; // not yet welcomed
        }

        _accumulator += delta;
        while (_accumulator >= TickSeconds)
        {
            _accumulator -= TickSeconds;
            StepFixedTick();
        }

        FollowLocalTank();
    }

    private void StepFixedTick()
    {
        if (_session is not null)
        {
            // Host: the world reads every input source (keyboard + relayed) and the session broadcasts.
            _session.Step(TickSeconds);
            return;
        }

        if (_predicted is not null && _guestLocalInput is not null)
        {
            var input = _guestLocalInput.Read();
            var frame = new InputFrame(++_inputSeq, input.Move.X, input.Move.Y, input.Aim,
                input.Fire ? InputFrame.FireBit : (byte)0);
            _transport.SendInput(frame);
            _predicted.Predict(frame);
            SyncLocalFromPrediction();
        }
    }

    // The relay welcomed us: slot 0 becomes the authority, anyone else a predicting guest.
    private void OnWelcome(byte slot)
    {
        _localSlot = slot;
        if (slot == HostSlot)
        {
            BecomeHost();
        }
        else
        {
            BecomeGuest(slot);
        }

        _status.SetStatus(NetStatusOverlay.ConnectedKey);
    }

    private void BecomeHost()
    {
        _world = new World(new CombatResolver(CombatHitRadius));
        _world.EntitySpawned += OnEntitySpawned;
        _world.EntityDespawned += OnEntityDespawned;

        _hostInput = BuildLocalInput();
        var hostTank = new Tank(_hostInput, _world, _arena, CellCentre(_level.SpawnX, _level.SpawnY),
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: 0);
        var guestInput = new RelayedInputSource();
        var guestTank = new Tank(guestInput, _world, _arena, CellCentre(GuestSpawn.X, GuestSpawn.Y),
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: 1);

        _tanks[0] = hostTank;
        _tanks[1] = guestTank;
        _world.Spawn(hostTank);
        _world.Spawn(guestTank);

        _session = new HostSession(_transport, _world, _grid,
            new (byte Slot, ITank Tank)[] { (0, hostTank), (1, guestTank) }, guestInput);
    }

    private void BecomeGuest(byte slot)
    {
        _guestLocalInput = BuildLocalInput();
        _predicted = new PredictedTank(slot, _arena, CellCentre(GuestSpawn.X, GuestSpawn.Y));
        EnsureMirroredTank(slot);
        SyncLocalFromPrediction();
    }

    // Host-side world events: every spawned entity gets the same 3D view single-player uses, so the
    // host's match looks identical to a local one. The host renders shots straight from its world;
    // a guest mirrors them from the snapshot's projectile set instead (ADR-0019 step 4).
    private void OnEntitySpawned(IEntity entity)
    {
        switch (entity)
        {
            case ITank tank:
                foreach (var (slot, known) in _tanks)
                {
                    if (ReferenceEquals(known, tank))
                    {
                        AddTankView(slot, tank);
                    }
                }

                break;
            case IProjectile projectile:
                var view = new Projectile3DView { Name = "Projectile3DView" };
                view.Bind(projectile);
                AddChild(view);
                _projectileViews[projectile] = view;
                break;
        }
    }

    private readonly Dictionary<IEntity, Node3D> _projectileViews = new();

    private void OnEntityDespawned(IEntity entity)
    {
        if (_projectileViews.TryGetValue(entity, out var view))
        {
            _projectileViews.Remove(entity);
            view.QueueFree();
        }
    }

    // A guest's authoritative snapshot: reconcile the prediction, mirror every other slot, apply walls.
    private void OnSnapshot(SnapshotFrame snapshot)
    {
        if (_session is not null)
        {
            return; // the host is the authority — it never consumes snapshots
        }

        _predicted?.Reconcile(snapshot);

        foreach (var state in snapshot.Tanks)
        {
            if (state.Slot == _localSlot)
            {
                SyncLocalFromPrediction();
                continue;
            }

            var firstSighting = !_tanks.ContainsKey(state.Slot);
            var tank = EnsureMirroredTank(state.Slot);
            tank.Position = new NVector2(state.X, state.Y);
            tank.Rotation = state.Rotation;
            tank.TurretRotation = state.TurretRotation;
            tank.Hp = state.Hp;
            tank.Team = state.Team;
            tank.Shield = state.Shield; // a shielded remote tank shows its shield bar (ADR-0019 step 4)
            tank.Layer = state.Layer;   // and renders at the right elevation
            _tankViews[state.Slot].ApplyTeamTint(tank.Team); // the mirrored team arrives with the snapshot
            if (firstSighting)
            {
                _status.SetStatus(NetStatusOverlay.Player2JoinedKey);
            }
        }

        foreach (var delta in snapshot.WallDeltas)
        {
            _grid.SetCell(delta.CellX, delta.CellY, new WallCell((CellMaterial)delta.Material, delta.Hp));
        }

        MirrorProjectiles(snapshot.Projectiles);
    }

    // Rebuild the guest's shot views from the snapshot's live projectile set (ADR-0019 step 4). The
    // guest runs no world, so each snapshot replaces the lot — Free() (not deferred QueueFree) so a
    // synchronous re-read sees the fresh set, and a heading rebuilt from the wire angle points a
    // missile bolt along its travel exactly as the host renders it.
    private void MirrorProjectiles(IReadOnlyList<Domain.Net.ProjectileState> shots)
    {
        foreach (var view in _snapshotProjectiles)
        {
            view.Free();
        }

        _snapshotProjectiles.Clear();
        foreach (var shot in shots)
        {
            var model = new NetProjectile
            {
                Position = new NVector2(shot.X, shot.Y),
                Direction = new NVector2(Mathf.Sin(shot.Rotation), Mathf.Cos(shot.Rotation)),
                Style = (ProjectileStyle)shot.Style,
                Layer = shot.Layer,
            };
            var view = new Projectile3DView { Name = "NetProjectile3DView" };
            view.Bind(model);
            AddChild(view);
            _snapshotProjectiles.Add(view);
        }
    }

    private void SyncLocalFromPrediction()
    {
        if (_predicted is null || _localSlot is not byte slot)
        {
            return;
        }

        var tank = EnsureMirroredTank(slot);
        tank.Position = _predicted.Position;
        tank.Rotation = _predicted.Rotation;
        tank.TurretRotation = _predicted.TurretRotation;
        tank.Hp = _predicted.Hp;
        tank.Team = _predicted.Team;
        _tankViews[slot].ApplyTeamTint(tank.Team);
    }

    private NetTank EnsureMirroredTank(byte slot)
    {
        if (_tanks.TryGetValue(slot, out var existing))
        {
            return (NetTank)existing;
        }

        var tank = new NetTank(TankMaxHp);
        _tanks[slot] = tank;
        AddTankView(slot, tank);
        return tank;
    }

    private void AddTankView(byte slot, ITank tank)
    {
        var view = new Tank3DView { Name = $"Tank3DView{slot}" };
        view.Bind(tank);
        AddChild(view);
        _tankViews[slot] = view;
        view.ApplyTeamTint(tank.Team);
    }

    private void FollowLocalTank()
    {
        if (_localSlot is not byte slot || !_tanks.TryGetValue(slot, out var local))
        {
            return;
        }

        var target = GroundProjection.ToWorld(local.Position);
        _camera.Position = target + CameraOffset();
        _camera.LookAt(target, Vector3.Up);
    }

    private static Vector3 CameraOffset()
    {
        var pitch = Mathf.DegToRad(CamPitchDeg);
        var yaw = Mathf.DegToRad(CamYawDeg);
        var dir = new Vector3(Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Cos(yaw));
        return dir * CamDistance;
    }

    // Plain daylight (no fog of war in versus — both players deserve the same information), the same
    // ortho ¾ camera as the local arena, and a flat dusty ground plane.
    private void BuildEnvironment()
    {
        _camera = new Camera3D
        {
            Name = "GameCamera",
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = CamOrthoSize,
            Far = 12000f,
            Near = 1f,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
        };
        AddChild(_camera);
        var centre = GroundProjection.ToWorld(new NVector2(_level.Width * TileSize / 2f, _level.Height * TileSize / 2f));
        _camera.Position = centre + CameraOffset();
        _camera.LookAt(centre, Vector3.Up);

        AddChild(new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-55f, -40f, 0f),
            LightEnergy = 1f,
            ShadowEnabled = true,
        });

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.55f, 0.62f, 0.70f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.65f, 0.65f, 0.62f),
                AmbientLightEnergy = 1f,
                TonemapMode = Godot.Environment.ToneMapper.Aces,
            },
        });
    }

    private void BuildGround()
    {
        var w = _level.Width * TileSize;
        var h = _level.Height * TileSize;
        AddChild(new MeshInstance3D
        {
            Name = "Ground",
            Mesh = new PlaneMesh { Size = new Vector2(w, h) },
            Position = new Vector3(w / 2f, 0f, h / 2f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.66f, 0.58f, 0.40f), // dusty sand
                Roughness = 1f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            },
        });
    }

    private static NVector2 CellCentre(int x, int y) =>
        new(GridOrigin.X + ((x + 0.5f) * TileSize), GridOrigin.Y + ((y + 0.5f) * TileSize));
}
