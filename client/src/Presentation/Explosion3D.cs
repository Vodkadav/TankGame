using Godot;

namespace TankGame.Presentation;

/// <summary>A short-lived, flashy cartoon explosion (ADR-0017): a blinding white core flash, a bright
/// fireball that bursts outward and fades, a ground shockwave ring, and a spray of flung shards — so a
/// player can clearly see why a tank died or where an airstrike landed. Frees itself when done. Spawned by
/// <c>Tank3DView</c> on death and <c>Airstrike3DView</c> on detonation.</summary>
public partial class Explosion3D : Node3D
{
    private const float Life = 0.7f;
    private const float FlashLife = 0.16f; // the white core flash is briefest and brightest

    private float _radius = 40f;
    private float _time;
    private MeshInstance3D _ball = null!;
    private StandardMaterial3D _ballMat = null!;
    private MeshInstance3D _flash = null!;
    private StandardMaterial3D _flashMat = null!;
    private MeshInstance3D _ring = null!;
    private StandardMaterial3D _ringMat = null!;
    private readonly System.Collections.Generic.List<(MeshInstance3D Node, Vector3 Velocity, StandardMaterial3D Mat)> _shards = new();

    /// <summary>Sets the blast size. Call before adding to the tree.</summary>
    public void Init(float radius) => _radius = radius;

    public override void _Ready()
    {
        // Fireball core.
        _ballMat = Fire(new Color(1f, 0.7f, 0.2f), energy: 3f);
        _ball = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f }, MaterialOverride = _ballMat };
        AddChild(_ball);

        // A blinding white flash that pops instantly and fades fastest.
        _flashMat = Fire(new Color(1f, 0.97f, 0.85f), energy: 6f);
        _flash = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f }, MaterialOverride = _flashMat };
        AddChild(_flash);

        // A flat shockwave ring sweeping out along the ground.
        _ringMat = Fire(new Color(1f, 0.85f, 0.4f), energy: 4f);
        _ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = _radius * 0.7f, OuterRadius = _radius * 0.9f },
            MaterialOverride = _ringMat,
        };
        AddChild(_ring);

        for (var i = 0; i < 12; i++)
        {
            var angle = i * (Mathf.Tau / 12f);
            var mat = Fire(new Color(1f, 0.55f, 0.15f), energy: 2.5f);
            var shard = new MeshInstance3D { Mesh = new SphereMesh { Radius = _radius * 0.12f, Height = _radius * 0.24f }, MaterialOverride = mat };
            AddChild(shard);
            var v = new Vector3(Mathf.Cos(angle), 0.7f, Mathf.Sin(angle)) * (_radius * 3.6f);
            _shards.Add((shard, v, mat));
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        var f = Mathf.Clamp(_time / Life, 0f, 1f);

        var r = _radius * Mathf.Sqrt(f);              // expand fast, then ease
        _ball.Scale = Vector3.One * Mathf.Max(0.01f, r * 1.1f);
        _ballMat.AlbedoColor = new Color(1f, 0.7f - (0.4f * f), 0.2f, 1f - f);

        // The white flash blooms large and vanishes within the first fraction of the life.
        var ff = Mathf.Clamp(_time / FlashLife, 0f, 1f);
        _flash.Scale = Vector3.One * Mathf.Max(0.01f, _radius * (0.6f + (0.9f * ff)));
        _flashMat.AlbedoColor = new Color(1f, 0.97f, 0.85f, 1f - ff);
        _flash.Visible = ff < 1f;

        // The shockwave ring sweeps outward and fades over the full life.
        _ring.Scale = new Vector3(0.4f + (1.8f * f), 1f, 0.4f + (1.8f * f));
        _ringMat.AlbedoColor = new Color(1f, 0.85f, 0.4f, (1f - f) * 0.8f);

        foreach (var (node, velocity, mat) in _shards)
        {
            node.Position += velocity * (float)delta;
            mat.AlbedoColor = new Color(1f, 0.55f, 0.15f, 1f - f);
        }

        if (_time >= Life)
        {
            QueueFree();
        }
    }

    private static StandardMaterial3D Fire(Color colour, float energy) => new()
    {
        AlbedoColor = colour,
        EmissionEnabled = true,
        Emission = colour,
        EmissionEnergyMultiplier = energy,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };
}
