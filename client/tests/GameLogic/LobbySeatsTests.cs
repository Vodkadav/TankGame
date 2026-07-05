using System.Linq;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class LobbySeatsTests
{
    [Fact]
    public void PlaceholderNames_AreDeterministicForALobbyCode()
    {
        // Every member of the same room must see the same cast — and the host's AI fill must
        // match what the room promised — so the names derive from the shared code alone.
        Assert.Equal(LobbySeats.PlaceholderNames("ABC123", 4), LobbySeats.PlaceholderNames("ABC123", 4));
    }

    [Fact]
    public void PlaceholderNames_AreDistinctWithinARoom()
    {
        var names = LobbySeats.PlaceholderNames("ABC123", 4);

        Assert.Equal(4, names.Distinct().Count());
    }

    [Fact]
    public void DifferentCodes_DealDifferentCasts()
    {
        Assert.NotEqual(LobbySeats.PlaceholderNames("ABC123", 4), LobbySeats.PlaceholderNames("XYZ789", 4));
    }
}
