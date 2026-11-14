using StemStudio.Contracts;
using StemStudio.Web.Api;

namespace StemStudio.Web.Mixer;

/// <summary>Mix name rules and save/rename messages (FR-016, US4-6).</summary>
public static class MixNames
{
    /// <summary>Reason for an empty or too long name.</summary>
    public const string LengthMessage = "Enter a name of 1 to 100 characters.";

    /// <summary>Reason when a loaded mix belongs to a different track.</summary>
    public const string WrongTrackMessage = "This mix belongs to another track and cannot be loaded here.";

    /// <summary>Checks the trimmed length (1-100).</summary>
    /// <param name="name">The entered name.</param>
    /// <returns>A reason, or null when valid.</returns>
    public static string? Validate(string? name)
    {
        var length = name?.Trim().Length ?? 0;
        return length is >= ContractLimits.MixNameMinLength and <= ContractLimits.MixNameMaxLength ? null : LengthMessage;
    }

    /// <summary>The message for a name used by another mix of the same track.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The message.</returns>
    public static string TakenMessage(string name) => $"The name \"{name}\" is taken for this track. Choose another name.";

    /// <summary>Maps a failed create/update to a user message (409 = name taken).</summary>
    /// <param name="error">The API error.</param>
    /// <param name="name">The name that was sent.</param>
    /// <returns>The message.</returns>
    public static string SaveError(ApiError error, string name) => error.IsConflict ? TakenMessage(name) : error.UserMessage;
}
