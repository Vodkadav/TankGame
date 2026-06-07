using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Draws a carpet-bombing <see cref="IAirstrike"/> in 3D (ADR-0017): each zone shows a red
/// ground disc once it arms, a big cartoon countdown (3·2·1) in its last seconds, and a bright flash when
/// it detonates — all driven from the strike's live <see cref="IAirstrike.Zones"/>. A pure mirror; the
/// scene frees it when the whole run has detonated.</summary>
public partial class Airstrike3DView : Node3D
{
    private const float DiscY = 2f;

    private IAirstrike _strike = null!;
    private MeshInstance3D[] _discs = null!;
    private StandardMaterial3D[] _mats = null!;
    private Label3D[] _counts = null!;
    private float _time;

    public void Bind(IAirstrike strike) => _strike = strike;

    public override void _Ready()
    {
        var zones = _strike.Zones;
        _discs = new MeshInstance3D[zones.Count];
        _mats = new StandardMaterial3D[zones.Count];
        _counts = new Label3D[zones.Count];
        for (var i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            _mats[i] = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.2f, 0.15f, 0.4f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            _discs[i] = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = z.Radius, BottomRadius = z.Radius, Height = 1f },
                Position = GroundProjection.ToWorld(z.Position, DiscY),
                MaterialOverride = _mats[i],
                Visible = false,
            };
            AddChild(_discs[i]);

            _counts[i] = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                FontSize = 110,
                PixelSize = 0.5f,
                OutlineSize = 36,
                OutlineModulate = Colors.Black,
                Modulate = new Color(1f, 0.9f, 0.3f),
                Position = GroundProjection.ToWorld(z.Position, 36f),
                Visible = false,
            };
            AddChild(_counts[i]);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        var pulse = 0.30f + (0.30f * Mathf.Sin(_time * 9f));
        var zones = _strike.Zones;
        for (var i = 0; i < zones.Count && i < _discs.Length; i++)
        {
            var z = zones[i];
            switch (z.Phase)
            {
                case AirstrikeZonePhase.Pending:
                    _discs[i].Visible = false;
                    _counts[i].Visible = false;
                    break;
                case AirstrikeZonePhase.Armed:
                    _discs[i].Visible = true;
                    _mats[i].AlbedoColor = new Color(0.95f, 0.2f, 0.15f, pulse);
                    var secs = Mathf.CeilToInt(z.Countdown);
                    _counts[i].Visible = secs is >= 1 and <= 3;
                    _counts[i].Text = secs.ToString();
                    break;
                case AirstrikeZonePhase.Detonated:
                    _discs[i].Visible = true;
                    _mats[i].AlbedoColor = new Color(1f, 0.65f, 0.2f, 0.7f); // bright blast flash
                    _counts[i].Visible = false;
                    break;
            }
        }
    }
}
