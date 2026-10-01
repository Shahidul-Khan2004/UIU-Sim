namespace UIU.Simulator.Networking
{
    /// <summary>
    /// Which Spring Boot backend a build talks to.
    /// LOCAL = developer machine. REMOTE = deployed Render (or similar) HTTPS API.
    /// </summary>
    public enum BackendEnvironment
    {
        LOCAL = 0,
        REMOTE = 1
    }
}
