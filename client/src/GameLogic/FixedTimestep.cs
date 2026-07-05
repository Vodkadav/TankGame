namespace TankGame.GameLogic;

/// <summary>Converts variable render-frame deltas into a whole number of fixed simulation steps.
/// The play scenes step the world through this instead of raw <c>_Process</c> delta, because a
/// laggy frame (WASM GC hitch, iPad dropping to 10 FPS) otherwise becomes one huge step in which
/// projectiles jump far enough to tunnel and movement turns erratic. With a fixed step the sim
/// stays deterministic per tick — under extreme lag the match slows down instead of misbehaving —
/// which is also what host/guest multiplayer determinism needs.</summary>
public sealed class FixedTimestep
{
    public const float DefaultStepSeconds = 1f / 60f;

    /// <summary>Most catch-up steps one frame may run. A hitch longer than this budget drops the
    /// un-simulated time instead of owing it, so a single long stall cannot trigger a spiral of
    /// death (each frame simulating more than it renders, falling further behind forever).</summary>
    public const int DefaultMaxSubsteps = 5;

    private readonly float _stepSeconds;
    private readonly int _maxSubsteps;
    private float _accumulated;

    public FixedTimestep(float stepSeconds = DefaultStepSeconds, int maxSubsteps = DefaultMaxSubsteps)
    {
        _stepSeconds = stepSeconds;
        _maxSubsteps = maxSubsteps;
    }

    /// <summary>Seconds each simulation step represents — the value to pass to the world's Step.</summary>
    public float StepSeconds => _stepSeconds;

    /// <summary>Banks a render frame's <paramref name="deltaSeconds"/> and returns how many fixed
    /// steps are now due (possibly zero — the fraction carries to the next frame).</summary>
    public int Advance(float deltaSeconds)
    {
        _accumulated += deltaSeconds;
        var due = (int)(_accumulated / _stepSeconds);
        if (due > _maxSubsteps)
        {
            _accumulated = 0f;
            return _maxSubsteps;
        }

        _accumulated -= due * _stepSeconds;
        return due;
    }
}
