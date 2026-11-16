using StemStudio.Agent.Ports;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>An environment with fixed variables (never the process environment).</summary>
public sealed class FakeEnvironment(params (string Name, string Value)[] variables) : IEnvironmentReader
{
    /// <summary>A well-formed key that must never appear in events, results or logs (built at run time, research R-07).</summary>
    public static string SentinelKey { get; } = ClaudeKeySentinels.Environment;

    private readonly Dictionary<string, string> _variables = variables.ToDictionary(variable => variable.Name, variable => variable.Value, StringComparer.Ordinal);

    public List<string> Reads { get; } = [];

    public static FakeEnvironment WithKey() => new(("ANTHROPIC_API_KEY", SentinelKey));

    /// <summary>A key provider over this environment and an empty store.</summary>
    public StemStudio.Agent.Backends.ClaudeKeyProvider Keys() => new(this, new FakeStoredClaudeKeyReader());

    public string? GetVariable(string name)
    {
        Reads.Add(name);
        return _variables.GetValueOrDefault(name);
    }
}
