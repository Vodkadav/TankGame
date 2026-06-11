using System.Numerics;
using TankGame.Domain;
using TankGame.Domain.Net;

namespace TankGame.GameLogic;

/// <summary>A guest's relayed intent as an <see cref="IInputSource"/> (ADR-0019 step 3): the host
/// feeds each forwarded <see cref="InputFrame"/> in with <see cref="Receive"/>, and the guest's tank
/// in the authoritative world reads it like any other input source — the exact seam the local
/// keyboard uses, so the same <see cref="Tank"/> code drives both players. Latest intent wins; a
/// frame arriving out of order behind a newer one is dropped. <see cref="LastAppliedSeq"/> advances
/// only when a world step actually reads the intent — it is the snapshot's
/// <see cref="SnapshotFrame.AckSeq"/>, the anchor the guest reconciles and replays from.</summary>
public sealed class RelayedInputSource : IInputSource
{
    private TankInput _intent = new(Vector2.Zero, Aim: 0f, Fire: false);
    private uint _latestSeq;

    /// <summary>The newest <see cref="InputFrame.Seq"/> whose intent has been fed into the world.</summary>
    public uint LastAppliedSeq { get; private set; }

    /// <summary>Adopts <paramref name="frame"/> as the guest's current intent, unless a newer frame
    /// already arrived (relay delivery can reorder under loss/retry).</summary>
    public void Receive(InputFrame frame)
    {
        if (frame.Seq < _latestSeq)
        {
            return;
        }

        _latestSeq = frame.Seq;
        _intent = new TankInput(new Vector2(frame.MoveX, frame.MoveY), frame.Aim, frame.Fire);
    }

    public TankInput Read()
    {
        LastAppliedSeq = _latestSeq;
        return _intent;
    }
}
