using System.Collections.Generic;
using System.Globalization;
using Godot;
using TankGame.Domain.Net;
using TankGame.GameLogic;
using NetMode = TankGame.Domain.Net.GameMode; // Presentation has its own local-play GameMode enum

namespace TankGame.Presentation;

/// <summary>The game room (multiplayer plan Phase 5): four seat rows driven by the server's
/// authoritative <see cref="LobbyView"/> pushes via <see cref="LobbyController"/>. A joined player
/// shows their real name; an empty seat shows a gray placeholder from the derpy cast — the same
/// name the AI tank filling that seat will carry once the match starts. The host (creator) sees a
/// Start button; everyone sees the mode, the map, and the countdown. When the server flips to
/// "started" the roster is snapshotted for the networked play scene and the room hands off.</summary>
public partial class LobbyRoomScene : Control
{
    public const string NetArenaScenePath = "res://src/Presentation/Arena/NetArena3D.tscn";
    public const string BrowserScenePath = "res://src/Presentation/LobbyBrowser.tscn";

    private static readonly Color PlaceholderGray = new(0.55f, 0.55f, 0.55f);
    private static readonly Color JoinedWhite = new(1f, 1f, 1f);

    private IMatchTransport _transport = null!;
    private LobbyController _controller = null!;
    private readonly Label[] _seatNames = new Label[LobbyProtocol.MaxPlayers];
    private IReadOnlyList<string> _placeholders = System.Array.Empty<string>();
    private Label _modeLabel = null!;
    private Label _mapLabel = null!;
    private Label _countdown = null!;
    private Button _start = null!;
    private bool _introduced; // name + the creator's mode/map picks sent once, on first seating

    public override void _Ready()
    {
        if (NetworkSession.Active is not { } transport)
        {
            Go(BrowserScenePath); // deep-linked here without a connection — nothing to show
            return;
        }

        _transport = transport;
        _placeholders = LobbySeats.PlaceholderNames(NetworkSession.ActiveCode, LobbyProtocol.MaxPlayers);
        _controller = new LobbyController(_transport);
        _controller.Changed += OnLobbyChanged;

        var menu = new VBoxContainer { Name = "Menu" };
        menu.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        menu.GrowHorizontal = GrowDirection.Both;
        menu.GrowVertical = GrowDirection.Both;
        menu.AddThemeConstantOverride("separation", 12);

        menu.AddChild(new Label
        {
            Name = "Heading",
            Text = "room.heading",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        for (var seat = 0; seat < _seatNames.Length; seat++)
        {
            _seatNames[seat] = new Label
            {
                Name = $"Seat{seat}",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            menu.AddChild(_seatNames[seat]);
        }

        _modeLabel = new Label { Name = "ModeLabel", HorizontalAlignment = HorizontalAlignment.Center };
        menu.AddChild(_modeLabel);
        _mapLabel = new Label { Name = "MapLabel", HorizontalAlignment = HorizontalAlignment.Center };
        menu.AddChild(_mapLabel);

        _countdown = new Label
        {
            Name = "Countdown",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        menu.AddChild(_countdown);

        _start = Button("Start", "room.start");
        _start.Visible = false; // revealed once the server seats this client as host
        _start.Pressed += () => _controller.Start();
        menu.AddChild(_start);

        var leave = Button("Leave", "room.leave");
        leave.Pressed += OnLeavePressed;
        menu.AddChild(leave);

        AddChild(menu);
        Render();
    }

    public override void _Process(double delta) => _transport?.Poll();

    private void OnLobbyChanged()
    {
        // First seating: tell the room our battle name, and — if we created it — apply the mode and
        // map picked in the create panel. Sent once; the server remains the authority thereafter.
        if (!_introduced && _controller.LocalSlot is not null && _controller.State is not null)
        {
            _introduced = true;
            _controller.SetName(GameSetup.PlayerName.Length > 0 ? GameSetup.PlayerName : "Player");
            if (_controller.IsHost)
            {
                if (NetworkSession.PendingMode is { } mode)
                {
                    _controller.SetMode(mode);
                }

                if (NetworkSession.PendingMap is { } map && map.Length > 0)
                {
                    _controller.SetMap(map);
                }

                NetworkSession.PendingMode = null;
                NetworkSession.PendingMap = null;
            }
        }

        if (_controller.HasStarted)
        {
            // Snapshot the final roster (slots, names, teams, mode, map) for the play scene.
            NetworkSession.StartedLobby = _controller.State;
            Go(NetArenaScenePath);
            return;
        }

        Render();
    }

    /// <summary>Renders the seats and status from the latest lobby view. Public so a test can force
    /// a render without frame timing.</summary>
    public void Render()
    {
        var view = _controller.State;
        for (var seat = 0; seat < _seatNames.Length; seat++)
        {
            var player = PlayerAt(view, seat);
            var label = _seatNames[seat];
            if (player is { } p)
            {
                label.Text = p.Slot == view!.HostSlot ? p.Name + " ★" : p.Name;
                label.AddThemeColorOverride("font_color", JoinedWhite);
            }
            else
            {
                label.Text = _placeholders.Count > seat ? _placeholders[seat] : string.Empty;
                label.AddThemeColorOverride("font_color", PlaceholderGray);
            }
        }

        _modeLabel.Text = view?.Mode == NetMode.Team ? "browser.mode_team" : "browser.mode_ffa";
        _mapLabel.Text = view is { Map.Length: > 0 } ? view.Map : "browser.map_random";
        _countdown.Text = view?.Phase == LobbyPhase.Countdown
            ? string.Format(CultureInfo.InvariantCulture, Tr("room.starting"), view.Countdown)
            : string.Empty;
        _start.Visible = _controller.IsHost && view?.Phase == LobbyPhase.Waiting;
    }

    private static LobbyPlayer? PlayerAt(LobbyView? view, int seat)
    {
        if (view is null)
        {
            return null;
        }

        foreach (var player in view.Players)
        {
            if (player.Slot == seat)
            {
                return player;
            }
        }

        return null;
    }

    private void OnLeavePressed()
    {
        NetworkSession.Reset(); // drops the transport; the room's close handler frees the seat
        Go(BrowserScenePath);
    }

    // Guarded so the GoDotTest click-path (which adds the room as a child, not the active scene)
    // can assert the wiring without the runner swapping its whole scene out from under it.
    private void Go(string scenePath)
    {
        if (GetTree().CurrentScene == this)
        {
            GetTree().ChangeSceneToFile(scenePath);
        }
    }

    private static Button Button(string name, string textKey) => new() { Name = name, Text = textKey };
}
