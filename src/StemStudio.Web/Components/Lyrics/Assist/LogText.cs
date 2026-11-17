using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>Texts of the Expert assist log and revision list.</summary>
public static class LogText
{
    /// <summary>"2 anchor, 0 schema, 1 check, 0 filtered, 1 withheld".</summary>
    /// <param name="d">The counts.</param>
    /// <returns>The text.</returns>
    public static string Discarded(DiscardedCountsDto d) =>
        $"{d.InvalidAnchor} anchor, {d.InvalidSchema} schema, {d.FailedVerification} check, {d.Filtered} filtered, {d.Verbatim} withheld";

    /// <summary>"1200 in / 300 out", or a dash without usage.</summary>
    /// <param name="usage">The usage.</param>
    /// <returns>The text.</returns>
    public static string Tokens(TokenUsageDto? usage) => usage is null ? "-" : $"{usage.InputTokens} in / {usage.OutputTokens} out";

    /// <summary>The cause of a revision.</summary>
    /// <param name="cause">The cause.</param>
    /// <returns>The text.</returns>
    public static string Cause(RevisionCauseDto cause) => cause switch
    {
        RevisionCauseDto.Created => "created",
        RevisionCauseDto.UserSave => "your save",
        RevisionCauseDto.Accept => "accepted suggestions",
        _ => "undo",
    };
}
