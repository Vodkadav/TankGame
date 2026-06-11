using System;
using System.Threading.Tasks;
using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain.Net;
using TankGame.Infrastructure;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Drives the Host/Join lobby flows (ADR-0019 step 2) against a fake lobby directory and a fake
// transport — no HTTP, no socket. The fakes complete synchronously, so the async button handlers
// run to completion within the press call and the asserts can run straight after.
public class LobbySceneTests : TestClass
{
    private sealed class FakeLobby : ILobbyClient
    {
        public string? CodeToMint { get; set; } = "ABC123";
        public bool KnowsEveryCode { get; set; } = true;
        public string? LastJoinedCode { get; private set; }

        public Task<string?> CreateLobbyAsync() => Task.FromResult(CodeToMint);

        public Task<bool> JoinLobbyAsync(string code)
        {
            LastJoinedCode = code;
            return Task.FromResult(KnowsEveryCode);
        }
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

    public LobbySceneTests(Node testScene) : base(testScene) { }

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

        _scene = GD.Load<PackedScene>("res://src/Presentation/Lobby.tscn").Instantiate<Control>();
        TestScene.AddChild(_scene); // runs _Ready, which builds the UI from the factories
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
    public void Lobby_OffersHostAndJoinControls()
    {
        foreach (var name in new[] { "Host", "Join", "Back" })
        {
            if (Find(name) is not Button)
            {
                throw new Exception($"Lobby screen must offer a '{name}' button.");
            }
        }

        if (Find("CodeEntry") is not LineEdit)
        {
            throw new Exception("Lobby screen must offer a 'CodeEntry' field for the friend code.");
        }
    }

    [Test]
    public void Host_MintsACode_ShowsIt_AndConnectsAsTheHost()
    {
        Press("Host");

        var code = Find("Code") as Label ?? throw new Exception("Missing the 'Code' label.");
        if (code.Text != "ABC123")
        {
            throw new Exception($"Hosting must show the minted code to share; showed '{code.Text}'.");
        }

        // The host connects the moment the lobby exists, so it takes relay slot 0 (the authority).
        if (_connectedCode != "ABC123" || NetworkSession.Active is null)
        {
            throw new Exception("Hosting must connect the transport to the new lobby immediately.");
        }

        if (Find("Start") is not Button { Visible: true })
        {
            throw new Exception("Hosting must reveal the 'Start' button once the code is up.");
        }
    }

    [Test]
    public void Host_ShowsTheError_WhenTheLobbyServiceIsUnreachable()
    {
        _lobby.CodeToMint = null;

        Press("Host");

        AssertStatus("lobby.error");
        if (NetworkSession.Active is not null)
        {
            throw new Exception("A failed host attempt must not leave a transport active.");
        }
    }

    [Test]
    public void Join_WithAKnownCode_ValidatesUppercased_AndConnects()
    {
        var entry = Find("CodeEntry") as LineEdit ?? throw new Exception("Missing 'CodeEntry'.");
        entry.Text = "  cdefgh ";

        Press("Join");

        if (_lobby.LastJoinedCode != "CDEFGH")
        {
            throw new Exception($"Join must validate the trimmed, uppercased code; sent '{_lobby.LastJoinedCode}'.");
        }

        if (_connectedCode != "CDEFGH" || NetworkSession.Active is null)
        {
            throw new Exception("A validated join must connect the transport to the lobby.");
        }
    }

    [Test]
    public void Join_WithAnUnknownCode_ShowsNotFound_AndStaysPut()
    {
        _lobby.KnowsEveryCode = false;
        var entry = Find("CodeEntry") as LineEdit ?? throw new Exception("Missing 'CodeEntry'.");
        entry.Text = "ZZZZZZ";

        Press("Join");

        AssertStatus("lobby.not_found");
        if (NetworkSession.Active is not null)
        {
            throw new Exception("An unknown code must not connect a transport.");
        }
    }

    [Test]
    public void Join_WithAnEmptyEntry_DoesNothing()
    {
        Press("Join");

        if (_lobby.LastJoinedCode is not null || NetworkSession.Active is not null)
        {
            throw new Exception("An empty code entry must not hit the lobby service.");
        }
    }

    [Test]
    public void Lobby_RendersItsLabelsInDanish()
    {
        var original = TranslationServer.GetLocale();
        try
        {
            TranslationServer.SetLocale("dk");
            var host = Find("Host") as Button ?? throw new Exception("Missing 'Host'.");
            var rendered = host.Tr(host.Text).ToString();
            if (rendered == host.Text || rendered.Length == 0)
            {
                throw new Exception($"'lobby.host' must have a Danish translation; rendered '{rendered}'.");
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

    private void AssertStatus(string expectedKey)
    {
        var status = Find("Status") as Label ?? throw new Exception("Missing the 'Status' label.");
        if (status.Text != expectedKey)
        {
            throw new Exception($"Expected status '{expectedKey}', got '{status.Text}'.");
        }
    }
}
