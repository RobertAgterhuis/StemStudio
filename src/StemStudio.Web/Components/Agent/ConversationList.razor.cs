using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Components.Agent;

/// <summary>The conversation list with in-place rename and delete (US5).</summary>
public sealed partial class ConversationList
{
    private Guid? _editing;
    private string _title = string.Empty;

    /// <summary>The conversations, newest first.</summary>
    [Parameter]
    public IReadOnlyList<ConversationSummaryDto> Conversations { get; set; } = [];

    /// <summary>The open conversation, or null.</summary>
    [Parameter]
    public Guid? OpenId { get; set; }

    /// <summary>True while a conversation's run is active.</summary>
    [Parameter]
    public Func<ConversationSummaryDto, bool> IsActive { get; set; } = _ => false;

    /// <summary>An entry was chosen.</summary>
    [Parameter]
    public EventCallback<Guid> OnSelect { get; set; }

    /// <summary>A new title was confirmed.</summary>
    [Parameter]
    public EventCallback<(Guid Id, string Title)> OnRename { get; set; }

    /// <summary>Delete was pressed (the parent confirms).</summary>
    [Parameter]
    public EventCallback<Guid> OnDelete { get; set; }

    private void StartRename(ConversationSummaryDto conversation) => (_editing, _title) = (conversation.ConversationId, conversation.Title);

    private void SetTitle(ChangeEventArgs e) => _title = e.Value?.ToString() ?? string.Empty;

    private Task KeyAsync(KeyboardEventArgs e, Guid id) => e.Key switch
    {
        "Enter" => SaveAsync(id),
        "Escape" => Task.FromResult(_editing = null),
        _ => Task.CompletedTask,
    };

    private async Task SaveAsync(Guid id)
    {
        _editing = null;
        await OnRename.InvokeAsync((id, _title));
    }
}
