namespace StemStudio.Web.Services;

/// <summary>An action of the current screen offered in the command palette (ui-test-ids <c>command-&lt;id&gt;</c>).</summary>
/// <param name="Id">Command id, e.g. <c>mixer-save</c> or <c>open-mixer</c>.</param>
/// <param name="Label">Visible label.</param>
/// <param name="Run">The same handler the screen's button uses.</param>
public sealed record PageCommand(string Id, string Label, Func<Task> Run);

/// <summary>
/// The actions the current screen offers in the command palette (FR-018). Pages register them while they are
/// shown and dispose the registration when they go away. Scoped per circuit.
/// </summary>
public sealed class PageCommands
{
    private readonly List<PageCommand> _items = [];

    /// <summary>Raised when an action is added or removed.</summary>
    public event Action? Changed;

    /// <summary>The registered actions in registration order.</summary>
    public IReadOnlyList<PageCommand> Items => _items;

    /// <summary>Adds an action.</summary>
    /// <param name="id">Command id.</param>
    /// <param name="label">Visible label.</param>
    /// <param name="run">The handler.</param>
    /// <returns>Dispose to remove the action again (twice is safe).</returns>
    public IDisposable Register(string id, string label, Func<Task> run)
    {
        var command = new PageCommand(id, label, run);
        _items.Add(command);
        Changed?.Invoke();
        return new Registration(this, command);
    }

    private void Remove(PageCommand command)
    {
        if (_items.Remove(command))
        {
            Changed?.Invoke();
        }
    }

    private sealed class Registration(PageCommands owner, PageCommand command) : IDisposable
    {
        public void Dispose() => owner.Remove(command);
    }
}
