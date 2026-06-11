using Godot;
using TankGame.Domain.Net;

namespace TankGame.Presentation;

/// <summary>The Team vs Team lobby (ADR-0019 step 2): Host mints a shareable 6-char code and connects
/// straight away — first socket in takes relay slot 0, so connecting at create time is what makes the
/// host the authority — then shows the code to read out while the friend joins. Join validates a typed
/// code against the lobby directory and connects as a guest. Both paths land in the networked play
/// scene; the lobby directory and transport come from <see cref="NetworkSession"/>'s swappable
/// factories, so the click paths test against fakes. Labels are translation keys (auto-translated);
/// the code itself renders raw.</summary>
public partial class LobbyScene : Control
{
    public const string NetArenaScenePath = "res://src/Presentation/Arena/NetArena.tscn";
    public const string TitleScenePath = "res://src/Presentation/Title.tscn";

    private ILobbyClient _lobby = null!;
    private Button _host = null!;
    private Button _join = null!;
    private Button _start = null!;
    private LineEdit _codeEntry = null!;
    private Label _code = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        _lobby = NetworkSession.LobbyFactory();

        var menu = new VBoxContainer { Name = "Menu" };
        menu.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        menu.GrowHorizontal = GrowDirection.Both;
        menu.GrowVertical = GrowDirection.Both;
        menu.AddThemeConstantOverride("separation", 12);

        menu.AddChild(new Label
        {
            Name = "Heading",
            Text = "lobby.heading",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        _host = Button("Host", "lobby.host");
        _host.Pressed += OnHostPressed;
        menu.AddChild(_host);

        _code = new Label
        {
            Name = "Code",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _code.AddThemeFontSizeOverride("font_size", 40); // the one thing a friend reads off this screen
        menu.AddChild(_code);

        _start = Button("Start", "lobby.start");
        _start.Visible = false;
        _start.Pressed += () => Go(NetArenaScenePath);
        menu.AddChild(_start);

        // No MaxLength: a pasted code with stray spaces must survive the assignment; the trim in
        // OnJoinPressed tidies it instead.
        _codeEntry = new LineEdit
        {
            Name = "CodeEntry",
            PlaceholderText = "lobby.code_hint",
            Alignment = HorizontalAlignment.Center,
        };
        menu.AddChild(_codeEntry);

        _join = Button("Join", "lobby.join");
        _join.Pressed += OnJoinPressed;
        menu.AddChild(_join);

        _status = new Label
        {
            Name = "Status",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        menu.AddChild(_status);

        var back = Button("Back", "lobby.back");
        back.Pressed += () => Go(TitleScenePath);
        menu.AddChild(back);

        AddChild(menu);
    }

    private async void OnHostPressed()
    {
        SetBusy(true);
        _status.Text = string.Empty;

        var code = await _lobby.CreateLobbyAsync();
        if (code is null)
        {
            _status.Text = "lobby.error";
            SetBusy(false);
            return;
        }

        NetworkSession.Join(code); // connect now: first in takes slot 0, the host authority
        _code.Text = code;
        _status.Text = "lobby.share_code";
        _start.Visible = true;
    }

    private async void OnJoinPressed()
    {
        var code = _codeEntry.Text.Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        if (!await _lobby.JoinLobbyAsync(code))
        {
            _status.Text = "lobby.not_found";
            SetBusy(false);
            return;
        }

        NetworkSession.Join(code);
        Go(NetArenaScenePath);
    }

    private void SetBusy(bool busy)
    {
        _host.Disabled = busy;
        _join.Disabled = busy;
        _codeEntry.Editable = !busy;
    }

    // Guarded so the GoDotTest click-path (which adds the lobby as a child, not the active scene) can
    // assert the wiring without the runner swapping its whole scene out from under it.
    private void Go(string scenePath)
    {
        if (GetTree().CurrentScene == this)
        {
            GetTree().ChangeSceneToFile(scenePath);
        }
    }

    private static Button Button(string name, string textKey) => new() { Name = name, Text = textKey };
}
