namespace TankGame.GameLogic;

/// <summary>FNV-1a over a string's chars — cheap, and identical on every client, platform and
/// process. Used wherever all members of a lobby must derive the same value from the shared lobby
/// code (the placeholder cast, the map seed); <c>string.GetHashCode</c> is per-process-randomised
/// and would desync them.</summary>
public static class StableHash
{
    public static int Of(string value)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in value)
            {
                hash = (hash ^ c) * 16777619;
            }

            return hash;
        }
    }
}
