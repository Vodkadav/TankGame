namespace TankGame.GameLogic;

/// <summary>The whole-arena ground look an authored map chooses (owner feedback 2026-06-11): one
/// tileset covering the entire field. Stored on <see cref="MapDefinition"/>; the Presentation layer
/// maps each value to its colours. More themes are additive — append only, the codec stores names.</summary>
public enum GroundTheme
{
    /// <summary>The dusty sandy patchwork the game launched with — the default.</summary>
    Sand,

    /// <summary>Everything greenish.</summary>
    Jungle,

    /// <summary>Everything very dark red.</summary>
    Mars,

    /// <summary>Gray asphalt with darker small speckles, like pebbles.</summary>
    ParkingLot,
}
