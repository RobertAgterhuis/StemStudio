namespace StemStudio.Web.Services;

/// <summary>Red stub (T060).</summary>
/// <param name="Id">Id.</param>
/// <param name="Label">Label.</param>
/// <param name="Run">Run.</param>
public sealed record PageCommand(string Id, string Label, Func<Task> Run);

/// <summary>Red stub (T060).</summary>
public sealed class PageCommands
{
    /// <summary>Changed.</summary>
    public event Action? Changed;

    /// <summary>Items.</summary>
    public IReadOnlyList<PageCommand> Items => Changed is null ? [] : [];

    /// <summary>Register.</summary>
    /// <param name="id">Id.</param>
    /// <param name="label">Label.</param>
    /// <param name="run">Run.</param>
    /// <returns>Registration.</returns>
    public IDisposable Register(string id, string label, Func<Task> run) => throw new NotImplementedException(id + label + run);
}
