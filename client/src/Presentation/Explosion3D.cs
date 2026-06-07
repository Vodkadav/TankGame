using Godot;

namespace TankGame.Presentation;

/// <summary>A short-lived cartoon explosion (ADR-0017): a bright fireball that bursts outward and fades,
/// with a few flung shards, so a player can see why a tank died or where an airstrike landed. Frees
/// itself when done. Spawned by <c>Tank3DView</c> on death and <c>Airstrike3DView</c> on detonation.</summary>
public partial class Explosion3D : Node3D
{
    private const float Life = 0.55f;

    private float _radius = 40f;
    private float _time;
    private MeshInstance3D _ball = null!;
    private StandardMaterial3D _ballMat = null!;
    private readonly System.Collections.Generic.List<(MeshInstance3D Node, Vector3 Velocity, StandardMaterial3D Mat)> _shards = new();

    /// <summary>Sets the blast size. Call before adding to the tree.</summary>
    public void Init(float radius) => _radius = radius;

    public override void _Ready()
    {
        _ballMat = Fire(new Color(1f, 0.7f, 0.2f));
        _ball = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f }, MaterialOverride = _ballMat };
        AddChild(_ball);

        for (var i = 0; i < 7; i++)
        {
            var angle = i * (Mathf.Tau / 7f);
            var mat = Fire(new Color(1f, 0.55f, 0.15f));
            var shard = new MeshInstance3D { Mesh = new SphereMesh { Radius = _radius * 0.12f, Height = _radius * 0.24f }, MaterialOverride = mat };
            AddChild(shard);
            var v = new Vector3(Mathf.Cos(angle), 0.6f, Mathf.Sin(angle)) * (_radius * 2.6f);
            _shards.Add((shard, v, mat));
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        var f = Mathf.Clamp(_time / Life, 0f, 1f);

        var r = _radius * Mathf.Sqrt(f);              // expand fast, then ease
        _ball.Scale = Vector3.One * Mathf.Max(0.01f, r);
        _ballMat.AlbedoColor = new Color(1f, 0.7f - (0.4f * f), 0.2f, 1f - f);

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

    private static StandardMaterial3D Fire(Color colour) => new()
    {
        AlbedoColor = colour,
        EmissionEnabled = true,
        Emission = colour,
        EmissionEnergyMultiplier = 1.5f,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };
}
