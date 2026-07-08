using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Godot;
using TankGame.Domain.Net;
using TankGame.GameLogic;
using NetMode = TankGame.Domain.Net.GameMode; // Presentation has its own local-play GameMode enum

namespace TankGame.Presentation;

/// <summary>The multiplayer lobby browser (multiplayer plan Phases 3–4): a scrollable list of open
/// games — each row shows the mode (FFA / Team vs Team), the map, the seat count, and a Join button
/// — plus a Create Game panel (map pick, random by default; mode pick, FFA by default). Joining or
/// creating connects the transport and moves to the room scene. The lobby directory and transport
/// come from <see cref="NetworkSession"/>'s swappable factories, so every click path tests against
/// fakes. Map picking lives here too: a Maps button opens the pick-map browser, and on desktop an
/// Editor button opens the authoring editor (the editor needs the local dev asset library that the
/// WASM build doesn't bundle, so web omits it). Labels are translation keys (auto-translated);
/// codes and map names render raw.</summary>
public partial class LobbyBrowserScene : Control
{
    public const string RoomScenePath = "res://src/Presentation/LobbyRoom.tscn";
    public const string TitleScenePath = "res://src/Presentation/Title.tscn";

    /// <summary>Wire prefix for a user-created map id, so a created map can never collide with a
    /// built-in arena name.</summary>
    public const string CustomMapPrefix = "custom:";

    private static readonly ArenaId[] BuiltInArenas = { ArenaId.DesertWar, ArenaId.CliffsAndValleys };

    private ILobbyClient _lobby = null!;
    private VBoxContainer _list = null!;
    private Label _empty = null!;
    private Label _status = null!;
    private Button _create = null!;
    private Button _refresh = null!;
    private Control _createPanel = null!;
    private OptionButton _modePick = null!;
    private OptionButton _mapPick = null!;
    private readonly List<string> _mapIds = new(); // parallel to _mapPick items; "" = random

    public override void _Ready()
    {
        Theme = MenuStyle.Shared;
        MenuStyle.AddBackdrop(this);

        _lobby = NetworkSession.LobbyFactory();
        if (_lobby is Node lobbyNode)
        {
            AddChild(lobbyNode); // the Godot-HttpRequest client only pumps inside the tree
        }

        var menu = new VBoxContainer { Name = "Menu" };
        menu.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        menu.GrowHorizontal = GrowDirection.Both;
        menu.GrowVertical = GrowDirection.Both;
        menu.AddThemeConstantOverride("separation", 12);

        menu.AddChild(new Label
        {
            Name = "Heading",
            Text = "browser.heading",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // The list scrolls so any number of open games fits on a phone screen.
        var scroll = new ScrollContainer
        {
            Name = "Scroll",
            CustomMinimumSize = new Vector2(520f, 260f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _list = new VBoxContainer { Name = "LobbyList", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_list);
        menu.AddChild(scroll);

        _empty = new Label
        {
            Name = "Empty",
            Text = "browser.empty",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        menu.AddChild(_empty);

        _status = new Label
        {
            Name = "Status",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        menu.AddChild(_status);

        _refresh = Button("Refresh", "browser.refresh");
        _refresh.Pressed += () => _ = RefreshAsync();
        menu.AddChild(_refresh);

        _create = Button("CreateGame", "browser.create");
        _create.Pressed += () => _createPanel.Visible = true;
        menu.AddChild(_create);

        // Map picking moved off the title into the lobby (owner ask 2026-07-05). MapSelect's Back
        // returns to the title — one hop from here, kept simple over threading a return-to-lobby flag.
        var maps = Button("Maps", "browser.maps");
        maps.Pressed += () => Go(TitleScene.MapSelectScenePath);
        menu.AddChild(maps);

        if (!OS.HasFeature("web"))
        {
            var editor = Button("Editor", "title.editor");
            editor.Pressed += () => Go(MapEditorScene.MapEditorScenePath);
            menu.AddChild(editor);
        }

        var back = Button("Back", "browser.back");
        back.Pressed += () => Go(TitleScenePath);
        menu.AddChild(back);

        AddChild(menu);
        BuildCreatePanel();
        MenuStyle.AttachHoverRecursive(this); // the static buttons + create-panel controls
        _ = RefreshAsync(); // the list is fresh every time the browser opens
    }

    // The create panel: mode (FFA default / Team) + map (random default / built-ins / created maps).
    private void BuildCreatePanel()
    {
        _createPanel = new PanelContainer { Name = "CreatePanel", Visible = false };
        _createPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _createPanel.GrowHorizontal = GrowDirection.Both;
        _createPanel.GrowVertical = GrowDirection.Both;

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        box.AddChild(new Label
        {
            Text = "create.heading",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        box.AddChild(new Label { Text = "create.mode" });
        _modePick = new OptionButton { Name = "ModePick", CustomMinimumSize = TouchTarget };
        _modePick.AddItem(Tr("browser.mode_ffa"));  // index 0 = FFA, the default
        _modePick.AddItem(Tr("browser.mode_team")); // index 1 = Team vs Team
        box.AddChild(_modePick);

        box.AddChild(new Label { Text = "create.map" });
        _mapPick = new OptionButton { Name = "MapPick", CustomMinimumSize = TouchTarget };
        _mapPick.AddItem(Tr("browser.map_random")); // index 0 = random, the default
        _mapIds.Add("");
        foreach (var arena in BuiltInArenas)
        {
            _mapPick.AddItem(Tr(MapLabel(arena.ToString())));
            _mapIds.Add(arena.ToString());
        }

        // Created maps are NOT offered online yet: the wire carries only a map id, and the other
        // members don't have the creator's map file — syncing map content over the lobby channel
        // is a follow-up. Offering them here would promise a map the room can't deliver.
        box.AddChild(_mapPick);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);

        var cancel = Button("CreateCancel", "create.cancel");
        cancel.Pressed += () => _createPanel.Visible = false;
        buttons.AddChild(cancel);

        var confirm = Button("CreateConfirm", "create.confirm");
        confirm.Pressed += OnCreateConfirmed;
        buttons.AddChild(confirm);
        box.AddChild(buttons);

        _createPanel.AddChild(box);
        AddChild(_createPanel);
    }

    private async Task RefreshAsync()
    {
        _status.Text = string.Empty;
        var open = await _lobby.ListOpenLobbiesAsync();

        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        if (open is null)
        {
            _empty.Visible = false;
            _status.Text = "browser.error";
            return;
        }

        _empty.Visible = open.Count == 0;
        foreach (var lobby in open)
        {
            _list.AddChild(BuildRow(lobby));
        }
    }

    private HBoxContainer BuildRow(OpenLobbyInfo lobby)
    {
        var row = new HBoxContainer { Name = $"Row{lobby.Code}" };
        row.AddThemeConstantOverride("separation", 12);

        row.AddChild(new Label
        {
            Name = "Mode",
            Text = lobby.Mode == NetMode.Team ? "browser.mode_team" : "browser.mode_ffa",
        });
        row.AddChild(new Label
        {
            Name = "Map",
            Text = MapLabel(lobby.Map),
        });
        row.AddChild(new Label
        {
            Name = "Seats",
            Text = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", lobby.Players, LobbyProtocol.MaxPlayers),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });

        var join = Button("Join", "browser.join");
        join.Pressed += () => _ = JoinAsync(lobby.Code);
        MenuStyle.AttachHover(join); // rows are built on refresh, after the one-time recursive pass
        row.AddChild(join);
        return row;
    }

    private async Task JoinAsync(string code)
    {
        _status.Text = string.Empty;
        if (!await _lobby.JoinLobbyAsync(code))
        {
            await RefreshAsync(); // the stale row vanishes...
            _status.Text = "browser.gone"; // ...and the player learns why (after, so refresh can't wipe it)
            return;
        }

        NetworkSession.Join(code);
        Go(RoomScenePath);
    }

    private async void OnCreateConfirmed()
    {
        _status.Text = string.Empty;
        var code = await _lobby.CreateLobbyAsync();
        if (code is null)
        {
            _createPanel.Visible = false;
            _status.Text = "browser.error";
            return;
        }

        // The room scene applies these once the server seats the creator as host.
        NetworkSession.PendingMode = _modePick.Selected == 1 ? NetMode.Team : NetMode.Ffa;
        NetworkSession.PendingMap = _mapIds[_mapPick.Selected];

        NetworkSession.Join(code); // first socket in takes slot 0 — creating is what makes you host
        Go(RoomScenePath);
    }

    /// <summary>What to show for a map id — a translation key for the built-ins/random, the raw
    /// name (prefix stripped) for a custom map. Shared with the room scene so the two never
    /// disagree about a map's label.</summary>
    public static string MapLabel(string mapId) => mapId switch
    {
        "" => "browser.map_random",
        "DesertWar" => "map.desert_war",
        "CliffsAndValleys" => "map.cliffs_and_valleys",
        _ when mapId.StartsWith(CustomMapPrefix, System.StringComparison.Ordinal) =>
            mapId[CustomMapPrefix.Length..],
        _ => mapId,
    };

    // Guarded so the GoDotTest click-path (which adds the browser as a child, not the active scene)
    // can assert the wiring without the runner swapping its whole scene out from under it.
    private void Go(string scenePath)
    {
        if (GetTree().CurrentScene == this)
        {
            GetTree().ChangeSceneToFile(scenePath);
        }
    }

    // ≥44 px tall (a11y touch-target baseline) — the arcade is played on phones and iPads.
    private static readonly Vector2 TouchTarget = new(0f, 48f);

    private static Button Button(string name, string textKey) =>
        new() { Name = name, Text = textKey, CustomMinimumSize = TouchTarget };
}
