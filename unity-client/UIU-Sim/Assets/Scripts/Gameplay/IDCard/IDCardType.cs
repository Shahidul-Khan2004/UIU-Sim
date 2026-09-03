/// <summary>
/// The type of campus ID card the player is currently holding.
/// </summary>
public enum IDCardType
{
    /// <summary>No card selected or available.</summary>
    None,

    /// <summary>Standard university ID — never consumed, small failure chance on scan.</summary>
    Permanent,

    /// <summary>One-use guest pass — always succeeds, destroyed after a single scan.</summary>
    Temporary
}
