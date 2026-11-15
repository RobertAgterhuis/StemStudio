namespace StemStudio.Contracts.Voices;

/// <summary>Multipart field names of <c>POST /api/voice-profiles</c> (backend contract 1.9.0).</summary>
public static class VoiceProfileForm
{
    /// <summary>The recorded or uploaded sample (file part).</summary>
    public const string Sample = "sample";

    /// <summary>Profile display name.</summary>
    public const string DisplayName = "displayName";

    /// <summary>Typed name of the person whose voice it is.</summary>
    public const string PersonName = "personName";

    /// <summary>Consent checkbox; must be <c>true</c>.</summary>
    public const string ConsentConfirmed = "consentConfirmed";

    /// <summary>Version of the statement the user agreed to.</summary>
    public const string ConsentStatementVersion = "consentStatementVersion";

    /// <summary><c>recorded</c> or <c>uploaded</c>.</summary>
    public const string Source = "source";
}
