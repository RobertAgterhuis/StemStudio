using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Care;

/// <summary>Base of the library care page's tabs (backups, storage; feature 013): the client and the status the page passes in.</summary>
public abstract class CareTabBase : ComponentBase
{
    /// <summary>The library care status (null before the first answer).</summary>
    [Parameter]
    public LibraryCareStatusDto? Status { get; set; }

    /// <summary>The library care API client.</summary>
    [Inject]
    protected ILibraryCareClient Client { get; set; } = default!;
}
