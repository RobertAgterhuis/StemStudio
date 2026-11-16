using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Runtime;

/// <summary>Reads the process environment; a blank value counts as unset. Values are returned, never logged.</summary>
public sealed class ProcessEnvironmentReader : IEnvironmentReader
{
    /// <inheritdoc />
    public string? GetVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
