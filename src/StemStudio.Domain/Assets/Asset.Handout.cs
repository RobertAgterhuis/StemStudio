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
    /// <param name="start">Provenance (origin <see cref="AssetOrigin.Handout"/> with a recipe) and plan.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The item.</returns>
    public static Asset CreateHandout(Guid id, string name, HandoutStart start, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(start.Plan);
        var provenance = start.Provenance;
        Require(provenance, provenance?.HandoutRecipe is not null && provenance.Origin == AssetOrigin.Handout, "A handout item needs its handout recipe.");
        var asset = New(id, AssetKind.Handout, name).Started(provenance!, nowUtc);
        asset.Handout = start.Plan with { Pages = 0, Warnings = [], Outline = [] };
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
    /// <param name="rendering">Pages (at least 1), outline and warnings.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void CompleteHandout(IReadOnlyList<AssetFile> files, HandoutRendering rendering, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(rendering);
        ArgumentNullException.ThrowIfNull(rendering.Outline);
        ArgumentNullException.ThrowIfNull(rendering.Warnings);
        ArgumentOutOfRangeException.ThrowIfLessThan(rendering.Pages, 1);
        var plan = RequireHandout();
        MarkReady(files, nowUtc);
        Handout = plan with { Pages = rendering.Pages, Outline = rendering.Outline.ToList(), Warnings = rendering.Warnings.ToList() };
    }

    private HandoutFacts RequireHandout() =>
        Kind == AssetKind.Handout && Handout is { } plan ? plan : throw new InvalidOperationException($"Item {Id} is not a handout.");
}

/// <summary>What a new handout item starts from.</summary>
/// <param name="Provenance">Provenance (origin handout, with the recipe).</param>
/// <param name="Plan">The plan (kind, family, songs, included and omitted ingredients).</param>
public sealed record HandoutStart(Provenance Provenance, HandoutFacts Plan);

/// <summary>The worker's result of a handout.</summary>
/// <param name="Pages">Page count.</param>
/// <param name="Outline">Bookmark titles.</param>
/// <param name="Warnings">Renderer warnings.</param>
public sealed record HandoutRendering(int Pages, IReadOnlyList<string> Outline, IReadOnlyList<HandoutWarning> Warnings);
