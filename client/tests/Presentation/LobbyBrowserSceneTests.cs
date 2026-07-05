using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain.Net;
using TankGame.Infrastructure;
using TankGame.Presentation;
using NetMode = TankGame.Domain.Net.GameMode;

namespace TankGame.Tests.Presentation;

// Drives the lobby browser (plan Phases 3–4) against a fake directory and a fake transport — no
// HTTP, no socket. The fakes complete synchronously, so the async click handlers run to completion
// within the press call and the asserts can run straight after.
public class LobbyBrowserSceneTests : TestClass
{
    private sealed class FakeLobby : ILobbyClient
    {
        public string? CodeToMint { get; set; } = "NEW001";
        public bool KnowsEveryCode { get; set; } = true;
        public string? LastJoinedCode { get; private set; }
        public IReadOnlyList<OpenLobbyInfo>? Open { get; set; } = new List<OpenLobbyInfo>
        {
            new("ABC123", NetMode.Ffa, 1, ""),
            new("DEF456", NetMode.Team, 3, "DesertWar"),
        };

        public Task<string?> CreateLobbyAsync() => Task.FromResult(CodeToMint);

        public Task<bool> JoinLobbyAsync(string code)
        {
            LastJoinedCode = code;
            return Task.FromResult(KnowsEveryCode);
        }

        public Task<IReadOnlyList<OpenLobbyInfo>?> ListOpenLobbiesAsync() => Task.FromResult(Open);
    }

    private sealed class FakeTransport : IMatchTransport
    {
        public event Action<byte> WelcomeReceived { add { } remove { } }
        public event Action<SnapshotFrame> SnapshotReceived { add { } remove { } }
        public void SendInput(InputFrame input) { }
        public void Poll() { }
    }

    private Func<ILobbyClient> _originalLobbyFactory = default!;
    private Func<string, IMatchTransport> _originalTransportFactory = default!;
    private FakeLobby _lobby = default!;
    private string? _connectedCode;
    private Control _scene = default!;

    public LobbyBrowserSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        TranslationLoader.EnsureLoaded();
        _originalLobbyFactory = NetworkSession.LobbyFactory;
        _originalTransportFactory = NetworkSession.TransportFactory;
        _lobby = new FakeLobby();
        _connectedCode = null;
        NetworkSession.LobbyFactory = () => _lobby;
        NetworkSession.TransportFactory = code =>
        {
            _connectedCode = code;
            return new FakeTransport();
        };
        NetworkSession.Reset();

        _scene = GD.Load<PackedScene>("res://src/Presentation/LobbyBrowser.tscn").Instantiate<Control>();
        TestScene.AddChild(_scene); // runs _Ready: builds the UI and refreshes the list
    }

    [Cleanup]
    public void Cleanup()
    {
        _scene.QueueFree();
        NetworkSession.LobbyFactory = _originalLobbyFactory;
        NetworkSession.TransportFactory = _originalTransportFactory;
        NetworkSession.Reset();
    }

    [Test]
    public void Browser_ListsEachOpenGame_WithModeAndJoin()
    {
        var row = Find("RowDEF456") as HBoxContainer
            ?? throw new Exception("The browser must list a row per open game (missing 'RowDEF456').");

        if (row.FindChild("Mode", recursive: true, owned: false) is not Label mode
            || mode.Text != "browser.mode_team")
        {
            throw new Exception("A row must badge its game mode (Team vs Team).");
        }

        if (row.FindChild("Join", recursive: true, owned: false) is not Button)
        {
            throw new Exception("Every row needs its Join button.");
        }

        if (Find("Empty") is not Label { Visible: false })
        {
            throw new Exception("The empty-list note must hide when games are listed.");
        }
    }

    [Test]
    public void Browser_OffersCreateRefreshAndBack()
    {
        foreach (var name in new[] { "CreateGame", "Refresh", "Back" })
        {
            if (Find(name) is not Button)
            {
                throw new Exception($"The browser must offer a '{name}' button.");
            }
        }
    }

    [Test]
    public void AnEmptyDirectory_ShowsTheCreateOneNote()
    {
        _lobby.Open = new List<OpenLobbyInfo>();

        Press("Refresh");

        if (Find("Empty") is not Label { Visible: true })
        {
            throw new Exception("An empty directory must show the 'create one' note.");
        }
    }

    [Test]
    public void AnUnreachableDirectory_ShowsTheError()
    {
        _lobby.Open = null;

        Press("Refresh");

        if (Find("Status") is not Label status || status.Text != "browser.error")
        {
            throw new Exception("An unreachable directory must surface 'browser.error'.");
        }
    }

    [Test]
    public void Join_ValidatesTheCode_AndConnectsTheTransport()
    {
        var row = Find("RowABC123") ?? throw new Exception("Missing 'RowABC123'.");
        var join = row.FindChild("Join", recursive: true, owned: false) as Button
            ?? throw new Exception("Missing the row's Join button.");

        join.EmitSignal(BaseButton.SignalName.Pressed);

        if (_lobby.LastJoinedCode != "ABC123" || _connectedCode != "ABC123" || NetworkSession.Active is null)
        {
            throw new Exception("Joining a row must validate its code and connect the transport.");
        }
    }

    [Test]
    public void Join_OnAGameThatJustClosed_ShowsGoneAndStaysPut()
    {
        _lobby.KnowsEveryCode = false;
        var row = Find("RowABC123") ?? throw new Exception("Missing 'RowABC123'.");
        var join = row.FindChild("Join", recursive: true, owned: false) as Button
            ?? throw new Exception("Missing the row's Join button.");

        join.EmitSignal(BaseButton.SignalName.Pressed);

        if (Find("Status") is not Label status || status.Text != "browser.gone")
        {
            throw new Exception("A join race (game filled/closed) must surface 'browser.gone'.");
        }

        if (NetworkSession.Active is not null)
        {
            throw new Exception("A failed join must not leave a transport active.");
        }
    }

    [Test]
    public void CreateGame_OpensThePanel_WithRandomMapAndFfaDefaults()
    {
        Press("CreateGame");

        if (Find("CreatePanel") is not Control { Visible: true })
        {
            throw new Exception("Create Game must open the create panel.");
        }

        if (Find("ModePick") is not OptionButton { Selected: 0 })
        {
            throw new Exception("The mode pick must default to FFA (index 0).");
        }

        if (Find("MapPick") is not OptionButton { Selected: 0 })
        {
            throw new Exception("The map pick must default to Random (index 0).");
        }
    }

    [Test]
    public void ConfirmingCreate_MintsConnects_AndStagesThePicksForTheRoom()
    {
        Press("CreateGame");
        var mode = Find("ModePick") as OptionButton ?? throw new Exception("Missing 'ModePick'.");
        mode.Selected = 1; // Team vs Team

        Press("CreateConfirm");

        if (_connectedCode != "NEW001" || NetworkSession.Active is null)
        {
            throw new Exception("Creating must connect the transport to the fresh room (creator = host).");
        }

        if (NetworkSession.PendingMode != NetMode.Team || NetworkSession.PendingMap != "")
        {
            throw new Exception("The creator's mode/map picks must be staged for the room scene to apply.");
        }
    }

    [Test]
    public void AFailedCreate_ShowsTheError_AndConnectsNothing()
    {
        _lobby.CodeToMint = null;

        Press("CreateGame");
        Press("CreateConfirm");

        if (Find("Status") is not Label status || status.Text != "browser.error")
        {
            throw new Exception("A failed create must surface 'browser.error'.");
        }

        if (NetworkSession.Active is not null)
        {
            throw new Exception("A failed create must not leave a transport active.");
        }
    }

    [Test]
    public void Browser_RendersItsLabelsInDanish()
    {
        var original = TranslationServer.GetLocale();
        try
        {
            TranslationServer.SetLocale("dk");
            var create = Find("CreateGame") as Button ?? throw new Exception("Missing 'CreateGame'.");
            var rendered = create.Tr(create.Text).ToString();
            if (rendered == create.Text || rendered.Length == 0)
            {
                throw new Exception($"'browser.create' must have a Danish translation; rendered '{rendered}'.");
            }
        }
        finally
        {
            TranslationServer.SetLocale(original);
        }
    }

    private Node? Find(string name) => _scene.FindChild(name, recursive: true, owned: false);

    private void Press(string buttonName)
    {
        var button = Find(buttonName) as Button ?? throw new Exception($"Missing '{buttonName}' button.");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
