using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Draws a carpet-bombing <see cref="IAirstrike"/> in 3D (ADR-0017): each zone shows a pulsing
/// red ground disc once it arms (the expanding telegraph), and a bright flash plus an explosion when it
/// detonates — all driven from the strike's live <see cref="IAirstrike.Zones"/>. A pure mirror; the scene
/// frees it when the whole run has detonated.</summary>
public partial class Airstrike3DView : Node3D
{
    private const float DiscY = 2f;

    private IAirstrike _strike = null!;
    private MeshInstance3D[] _discs = null!;
    private StandardMaterial3D[] _mats = null!;
    private bool[] _blown = null!;
    private float _time;

    public void Bind(IAirstrike strike) => _strike = strike;

    public override void _Ready()
    {
        var zones = _strike.Zones;
        _discs = new MeshInstance3D[zones.Count];
        _mats = new StandardMaterial3D[zones.Count];
        _blown = new bool[zones.Count];
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
                    break;
                case AirstrikeZonePhase.Armed:
                    _discs[i].Visible = true;
                    _mats[i].AlbedoColor = new Color(0.95f, 0.2f, 0.15f, pulse);
                    break;
                case AirstrikeZonePhase.Detonated:
                    _discs[i].Visible = true;
                    _mats[i].AlbedoColor = new Color(1f, 0.65f, 0.2f, 0.7f); // bright blast flash
                    if (!_blown[i])
                    {
                        _blown[i] = true;
                        var boom = new Explosion3D();
                        boom.Init(z.Radius);
                        boom.Position = GroundProjection.ToWorld(z.Position, 4f);
                        GetParent()?.AddChild(boom);
                    }

                    break;
            }
        }
    }
}
