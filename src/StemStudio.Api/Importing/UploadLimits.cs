using StemStudio.Contracts;

namespace StemStudio.Api.Importing;

/// <summary>Request size limits for the upload endpoint (G1).</summary>
public static class UploadLimits
{
    /// <summary>Allowance for multipart boundaries and part headers on top of the file itself (1 MiB).</summary>
    public const long MultipartOverheadBytes = 1024 * 1024;

    /// <summary>Kestrel <c>MaxRequestBodySize</c> and <c>FormOptions.MultipartBodyLengthLimit</c>; the exact file limit is
    /// enforced while streaming (<see cref="ContractLimits.MaxUploadBytes"/>).</summary>
    public const long MaxRequestBodyBytes = ContractLimits.MaxUploadBytes + MultipartOverheadBytes;
}
