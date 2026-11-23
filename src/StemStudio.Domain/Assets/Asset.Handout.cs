using StemStudio.Domain.Handouts;

namespace StemStudio.Domain.Assets;

/// <summary>Handout items (feature 020, data-model 5.4).</summary>
public sealed partial class Asset
{
    /// <summary>Role and file name of a handout's PDF.</summary>
    public const string HandoutFileName = "handout.pdf";

    /// <summary>The handout plan and, once ready, its result; null for other kinds.</summary>
    public HandoutFacts? Handout { get; private set; }

    /// <summary>A queued handout item.</summary>
    /// <param name="id">Item id.</param>
    /// <param name="name">Name.</param>
    /// <param name="provenance">Provenance with origin <see cref="AssetOrigin.Handout"/> and a handout recipe.</param>
    /// <param name="plan">The plan (kind, family, songs, included and omitted ingredients).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The item.</returns>
    public static Asset CreateHandout(Guid id, string name, Provenance provenance, HandoutFacts plan, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Require(provenance, provenance?.HandoutRecipe is not null && provenance.Origin == AssetOrigin.Handout, "A handout item needs its handout recipe.");
        var asset = New(id, AssetKind.Handout, name).Started(provenance!, nowUtc);
        asset.Handout = plan with { Pages = 0, Warnings = [], Outline = [] };
        return asset;
    }

    /// <summary>Replaces the plan of a handout item that is made again (retry).</summary>
    /// <param name="plan">The new plan.</param>
    public void Replan(HandoutFacts plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        RequireHandout();
        Handout = plan with { Pages = 0, Warnings = [], Outline = [] };
    }

    /// <summary>Makes a handout item ready with its PDF and the worker's result.</summary>
    /// <param name="files">The stored PDF.</param>
    /// <param name="pages">Page count (at least 1).</param>
    /// <param name="outline">Bookmark titles.</param>
    /// <param name="warnings">Renderer warnings.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void CompleteHandout(IReadOnlyList<AssetFile> files, int pages, IReadOnlyList<string> outline, IReadOnlyList<HandoutWarning> warnings, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentOutOfRangeException.ThrowIfLessThan(pages, 1);
        var plan = RequireHandout();
        MarkReady(files, nowUtc);
        Handout = plan with { Pages = pages, Outline = outline.ToList(), Warnings = warnings.ToList() };
    }

    private HandoutFacts RequireHandout() =>
        Kind == AssetKind.Handout && Handout is { } plan ? plan : throw new InvalidOperationException($"Item {Id} is not a handout.");
}
