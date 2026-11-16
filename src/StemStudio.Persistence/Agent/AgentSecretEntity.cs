namespace StemStudio.Persistence.Agent;

/// <summary>
/// One encrypted secret of the agent (011 follow-up claude-key, data-model 2, table <c>AgentSecrets</c>). Today only
/// <see cref="ClaudeApiKeyName"/>. Holds only the Data Protection ciphertext, never the plain value; the ciphertext is never
/// decoded outside the API's <c>ProtectedClaudeKeyStore</c>.
/// </summary>
public sealed class AgentSecretEntity
{
    /// <summary>Name of the stored Claude API key row.</summary>
    public const string ClaudeApiKeyName = "claude-api-key";

    /// <summary>Maximum length of <see cref="Name"/>.</summary>
    public const int NameMaxLength = 40;

    /// <summary>Maximum length of <see cref="Ciphertext"/>.</summary>
    public const int CiphertextMaxLength = 2048;

    /// <summary>Exact length of <see cref="LastFour"/>.</summary>
    public const int LastFourLength = 4;

    /// <summary>Secret name (primary key).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Data Protection payload (base64url).</summary>
    public string Ciphertext { get; set; } = string.Empty;

    /// <summary>Last four characters of the trimmed value.</summary>
    public string LastFour { get; set; } = string.Empty;

    /// <summary>Version of the protector purpose the ciphertext was made with (1 = <c>StemStudio.Agent.ClaudeApiKey.v1</c>).</summary>
    public int PurposeVersion { get; set; }

    /// <summary>Time of the last save or re-protection (UTC).</summary>
    public DateTime StoredAt { get; set; }
}
