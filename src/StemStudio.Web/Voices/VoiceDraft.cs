using StemStudio.Contracts;
using StemStudio.Contracts.Voices;
using StemStudio.Web.Api;

namespace StemStudio.Web.Voices;

/// <summary>A sample of own singing waiting to become a voice profile (recorded in the app or uploaded).</summary>
/// <param name="Bytes">The audio.</param>
/// <param name="FileName">File name sent to the API.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="Source">Recorded or uploaded.</param>
/// <param name="PreviewUrl">Object URL of the browser preview, or null.</param>
public sealed record VoiceSample(byte[] Bytes, string FileName, string ContentType, VoiceSampleSourceDto Source, string? PreviewUrl);

/// <summary>
/// The "new voice profile" panel (010 FR-005, FR-006): a sample, the ticked consent statement of a known version, the
/// typed name of the person (1-80) and a profile name (1-60). Nothing is sent before all of them are there.
/// </summary>
public sealed class VoiceDraft
{
    /// <summary>The recorded or uploaded sample.</summary>
    public VoiceSample? Sample { get; set; }

    /// <summary>The statement shown (its version is sent with the consent).</summary>
    public ConsentStatementDto? Statement { get; set; }

    /// <summary>The consent checkbox.</summary>
    public bool Consented { get; set; }

    /// <summary>Typed name of the person whose voice it is.</summary>
    public string PersonName { get; set; } = string.Empty;

    /// <summary>Profile name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>True when the profile may be saved: sample, statement, ticked checkbox and both names within their limits.</summary>
    public bool IsComplete =>
        Sample is not null && Statement is not null && Consented && Fits(PersonName, ContractLimits.ConsentPersonNameMaxLength) && Fits(DisplayName, ContractLimits.VoiceNameMaxLength);

    /// <summary>The upload body (call only when <see cref="IsComplete"/>); the stream is the caller's to dispose.</summary>
    /// <returns>The body.</returns>
    public VoiceProfileUpload Upload() => new(
        new UploadFile(new MemoryStream(Sample!.Bytes), Sample.FileName, Sample.ContentType),
        DisplayName.Trim(),
        PersonName.Trim(),
        Consented,
        Statement!.Version,
        Sample.Source);

    private static bool Fits(string text, int max)
    {
        var length = text.Trim().Length;
        return length > 0 && length <= max;
    }
}
