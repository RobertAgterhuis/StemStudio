using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>An environment with fixed variables (never the process environment).</summary>
public sealed class FakeEnvironment(params (string Name, string Value)[] variables) : IEnvironmentReader
{
    /// <summary>A key that must never appear in events, results or logs.</summary>
    public const string SentinelKey = "sk" + "-ant-sentinel-7f3a-never-log-me"; // built from parts, like scripts/secret_scan.py

    private readonly Dictionary<string, string> _variables = variables.ToDictionary(variable => variable.Name, variable => variable.Value, StringComparer.Ordinal);

    public List<string> Reads { get; } = [];

    public static FakeEnvironment WithKey() => new(("ANTHROPIC_API_KEY", SentinelKey));

    public string? GetVariable(string name)
    {
        Reads.Add(name);
        return _variables.GetValueOrDefault(name);
    }
}
