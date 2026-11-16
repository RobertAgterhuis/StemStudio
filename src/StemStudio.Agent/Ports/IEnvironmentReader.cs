namespace StemStudio.Agent.Ports;

/// <summary>
/// Reads process environment variables. The Claude backend reads <c>ANTHROPIC_API_KEY</c> only through this port so tests can
/// inject a sentinel key; the value is passed to the SDK client and nowhere else (constitution 1.3.0, SC-006).
/// </summary>
public interface IEnvironmentReader
{
    /// <summary>The variable's value, or null when unset or empty.</summary>
    /// <param name="name">Variable name.</param>
    /// <returns>The value, or null.</returns>
    string? GetVariable(string name);
}
