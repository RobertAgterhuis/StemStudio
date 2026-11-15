namespace StemStudio.Agent.Ports;

/// <summary>Answer of the open page to a UI tool call.</summary>
public sealed record UiToolResult
{
    /// <summary>True when the page executed the tool.</summary>
    public required bool Ok { get; init; }

    /// <summary>JSON result object, at most 4 KB.</summary>
    public string? Result { get; init; }

    /// <summary>Readable error, e.g. "The mixer of Jolene is not open".</summary>
    public string? Error { get; init; }
}
