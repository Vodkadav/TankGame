using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A fired shot — a world <see cref="IEntity"/>. It holds its <see cref="ProjectileState"/>
/// and an <see cref="IProjectileBehaviour"/> that drives movement and impact each tick; the
/// Projectile itself is a thin entity wrapper exposing the read-only slices combat and the view
/// need. The default behaviour is a straight shot (today's logic); bouncing/piercing/etc. are just
/// a different behaviour over the same state. No Godot — the Presentation layer renders this
/// state.</summary>
public sealed class Projectile : IProjectile
{
    private readonly IArena _arena;
    private readonly ProjectileState _state;
    private readonly IProjectileBehaviour _behaviour;

    /// <param name="arena">Collision query source, passed to the behaviour each step.</param>
    /// <param name="spawn">World-space spawn position (the turret muzzle).</param>
    /// <param name="direction">Travel direction; normalised internally.</param>
    /// <param name="speed">Travel speed in units per second.</param>
    /// <param name="damage">Damage dealt to a destructible wall or tank on impact.</param>
    /// <param name="team">The firing tank's team; the combat pass spares the same team.</param>
    /// <param name="behaviour">How the shot moves and hits; defaults to a straight shot.</param>
    /// <param name="pierce">How many targets the shot passes through before stopping; 0 (default)
    /// is an ordinary shot spent on its first hit.</param>
    /// <param name="layer">The elevation layer the shot travels on — it inherits the firing tank's
    /// layer and keeps it for its whole flight, so it only hits tanks on that same layer (ADR-0018).
    /// Defaults to 0, the ground layer (a flat arena is single-layer).</param>
    public Projectile(IArena arena, Vector2 spawn, Vector2 direction, float speed, int damage = 1,
        int team = 0, IProjectileBehaviour? behaviour = null, int pierce = 0, Guid owner = default,
        ProjectileStyle style = ProjectileStyle.Normal, int layer = 0)
    {
        Id = Guid.NewGuid();
        Layer = layer;
        _arena = arena;
        _state = new ProjectileState
        {
            Position = spawn,
            Direction = Vector2.Normalize(direction),
            Speed = speed,
            Damage = damage,
            Team = team,
            Pierce = pierce,
            Owner = owner,
            Style = style,
            Layer = layer,
        };
        _behaviour = behaviour ?? StraightBehaviour.Instance;
    }

    public int Team => _state.Team;

    /// <summary>The id of the tank that fired this shot — combat never hits the shooter itself.</summary>
    public Guid Owner => _state.Owner;
    public Guid Id { get; }

    /// <summary>The elevation layer this shot travels on (ADR-0018); combat only lets it hit tanks on
    /// the same layer. 0 is the ground layer.</summary>
    public int Layer { get; }

    public Vector2 Position => _state.Position;
    public Vector2 Direction => _state.Direction;
    public ProjectileStyle Style => _state.Style;
    public bool IsAlive => _state.IsAlive;

    /// <summary>Damage this shot deals to a tank on impact.</summary>
    public int Damage => _state.Damage;

    /// <summary>Marks the shot spent so the world reaps it — used when the combat pass lands
    /// it on a tank.</summary>
    public void Expire() => _state.IsAlive = false;

    /// <summary>Whether this shot has already damaged the tank with <paramref name="tankId"/> — so
    /// a piercing shot lingering over the same tank does not damage it every tick.</summary>
    public bool HasHit(Guid tankId) => _state.HitTanks.Contains(tankId);

    /// <summary>Records a tank hit and spends one unit of the shared pierce budget: a shot with
    /// budget left passes through (stays alive); one with none stops here. The combat resolver
    /// calls this after applying the damage.</summary>
    public void RegisterTankHit(Guid tankId)
    {
        _state.HitTanks.Add(tankId);
        if (_state.Pierce > 0)
        {
            _state.Pierce--;
        }
        else
        {
            _state.IsAlive = false;
        }
    }

    public void Step(float deltaSeconds)
    {
        if (!_state.IsAlive)
        {
            return;
        }

        _behaviour.Step(_state, _arena, deltaSeconds);
    }
}
