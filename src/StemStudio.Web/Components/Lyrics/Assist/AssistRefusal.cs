using System.Text.Json;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// A refused assistant request, read from the problem details (<c>AssistProblem</c>, backend-api 1.23.0): its code, the readable
/// message and, for <c>not-own-lyrics</c>, the matched track and whether the own-work confirmation is offered (FR-031).
/// </summary>
/// <param name="Code">The problem code (<see cref="LyricAssistProblemTypes"/>), or null for other errors.</param>
/// <param name="Message">The readable message.</param>
public sealed record AssistRefusal(string? Code, string Message)
{
    /// <summary>The matched library track (<c>not-own-lyrics</c>).</summary>
    public Guid? MatchedTrackId { get; init; }

    /// <summary>Its title (<c>not-own-lyrics</c>).</summary>
    public string? MatchedTrackTitle { get; init; }

    /// <summary>True when the editor offers "This is my own text".</summary>
    public bool Overridable { get; init; }

    /// <summary>The fix of <c>model-unavailable</c>.</summary>
    public string? Fix { get; init; }

    /// <summary>True for <c>not-own-lyrics</c>.</summary>
    public bool IsNotOwn => Code == LyricAssistProblemTypes.NotOwnLyrics;

    /// <summary>Reads an API error.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The refusal.</returns>
    public static AssistRefusal From(ApiError error) => error.Code switch
    {
        LyricAssistProblemTypes.NotOwnLyrics => NotOwn(error),
        LyricAssistProblemTypes.Imitation =>
            new(error.Code, "The assistant does not write like a named artist. Describe the mood or the theme instead."),
        LyricAssistProblemTypes.ModelUnavailable =>
            new(error.Code, Text(error, LyricAssistProblemMembers.Reason) ?? error.UserMessage) { Fix = Text(error, LyricAssistProblemMembers.Fix) },
        LyricAssistProblemTypes.AssistActive => new(error.Code, "A check is running. Wait for it or cancel it."),
        _ => new(error.Code, error.UserMessage),
    };

    private static AssistRefusal NotOwn(ApiError error)
    {
        var title = Text(error, LyricAssistProblemMembers.MatchedTrackTitle);
        return new(error.Code, $"This text matches the lyrics of {title ?? "a track in your library"}. The assistant works on your own lyrics only.")
        {
            MatchedTrackTitle = title,
            MatchedTrackId = Guid.TryParse(Text(error, LyricAssistProblemMembers.MatchedTrackId), out var id) ? id : null,
            Overridable = error.Extensions.TryGetValue(LyricAssistProblemMembers.Overridable, out var value) && value.ValueKind == JsonValueKind.True,
        };
    }

    private static string? Text(ApiError error, string member) =>
        error.Extensions.TryGetValue(member, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
