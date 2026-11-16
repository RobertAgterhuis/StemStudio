using StemStudio.Agent.Ports;

namespace StemStudio.Api.Agent;

/// <summary>Reads the process environment (default <see cref="IEnvironmentReader"/> when the agent library registers none).</summary>
public sealed class ProcessEnvironmentReader : IEnvironmentReader
{
    /// <inheritdoc />
    public string? GetVariable(string name)
    {
        var value = System.Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
