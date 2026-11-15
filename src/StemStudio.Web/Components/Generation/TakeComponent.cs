using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Generation;

namespace StemStudio.Web.Components.Generation;

/// <summary>Base of the components that show one take of a generation (player, details, vocal badges).</summary>
public abstract class TakeComponent : ComponentBase
{
    /// <summary>The generation of the take.</summary>
    [Parameter, EditorRequired]
    public GenerationDto Generation { get; set; } = default!;

    /// <summary>The take.</summary>
    [Parameter, EditorRequired]
    public GenerationTakeDto Take { get; set; } = default!;

    /// <summary>The id suffix of the take's test ids: <c>&lt;generationId&gt;-&lt;index&gt;</c>.</summary>
    protected string Key => $"{Generation.Id}-{Take.Index}";
}
