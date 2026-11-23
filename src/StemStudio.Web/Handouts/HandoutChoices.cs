using StemStudio.Contracts.Handouts;

namespace StemStudio.Web.Handouts;

/// <summary>The choices of the "Customise..." dialog (FR-008): ingredients, notation items, transposition and paper.</summary>
public sealed class HandoutChoices
{
    private readonly HashSet<IngredientCodeDto> _included = [];
    private readonly HashSet<Guid> _notation = [];

    /// <summary>The options the choices were made from; null until loaded.</summary>
    public HandoutOptionsDto? Options { get; private set; }

    /// <summary>The paper.</summary>
    public PaperDto Paper { get; set; } = PaperDto.A4;

    /// <summary>The transposition (melodic only); null for the other families.</summary>
    public TranspositionDto? Transposition { get; set; }

    /// <summary>Takes new options: ingredients and notation items as the one-click defaults; the paper the default once.</summary>
    /// <param name="options">The options.</param>
    /// <param name="first">True for the first options of the dialog (the paper follows the default only then).</param>
    public void Reset(HandoutOptionsDto options, bool first)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        _included.Clear();
        _included.UnionWith(options.Ingredients.Where(i => i.DefaultIncluded && i.Status == IngredientStatusDto.Available).Select(i => i.Code));
        _notation.Clear();
        _notation.UnionWith(options.Notation.Where(n => n.DefaultSelected).Select(n => n.AssetId));
        Paper = first ? options.DefaultPaper : Paper;
        Transposition = options.Transpositions.Count == 0 ? null : Transposition ?? TranspositionDto.C;
    }

    /// <summary>Whether an ingredient is chosen.</summary>
    /// <param name="code">The ingredient.</param>
    /// <returns>True when chosen.</returns>
    public bool Includes(IngredientCodeDto code) => _included.Contains(code);

    /// <summary>Whether a notation item is chosen.</summary>
    /// <param name="assetId">The item.</param>
    /// <returns>True when chosen.</returns>
    public bool Selects(Guid assetId) => _notation.Contains(assetId);

    /// <summary>Chooses or drops an ingredient.</summary>
    /// <param name="code">The ingredient.</param>
    /// <param name="on">True to choose it.</param>
    public void SetIngredient(IngredientCodeDto code, bool on) => _ = on ? _included.Add(code) : _included.Remove(code);

    /// <summary>Chooses or drops a notation item.</summary>
    /// <param name="assetId">The item.</param>
    /// <param name="on">True to choose it.</param>
    public void SetNotation(Guid assetId, bool on) => _ = on ? _notation.Add(assetId) : _notation.Remove(assetId);

    /// <summary>The <c>createHandout</c> body with the explicit choices.</summary>
    /// <param name="source">Song or setlist.</param>
    /// <param name="family">The family.</param>
    /// <returns>The request.</returns>
    public CreateHandoutRequest Request(HandoutSourceDto source, HandoutFamilyDto family) => new()
    {
        Source = source,
        Family = family,
        Include = [.. Options!.Ingredients.Select(i => i.Code).Where(_included.Contains)],
        NotationAssetIds = [.. Options.Notation.Select(n => n.AssetId).Where(_notation.Contains)],
        Transposition = Transposition,
        Paper = Paper,
    };
}
