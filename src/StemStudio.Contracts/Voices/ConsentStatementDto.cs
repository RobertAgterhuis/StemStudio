namespace StemStudio.Contracts.Voices;

/// <summary>Body of <c>GET /api/consent/voice</c> (1.9.0).</summary>
public sealed record ConsentStatementDto
{
    /// <summary>Statement version.</summary>
    public required string Version { get; init; }

    /// <summary>English text.</summary>
    public required string Text { get; init; }

    /// <summary>Dutch text.</summary>
    public required string TextNl { get; init; }
}
