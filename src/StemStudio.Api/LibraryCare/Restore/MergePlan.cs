using System.Globalization;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>
/// What a merge restore adds and skips (data-model 5.7): the staged entities to insert in dependency order, the files of
/// the added items (data-relative folders or files), the report entries and the ids whose dependants are skipped.
/// </summary>
public sealed class MergePlan
{
    private readonly List<object> _added = [];
    private readonly List<string> _paths = [];
    private readonly List<SkippedItemDto> _skipped = [];
    private readonly HashSet<Guid> _addedIds = [];
    private readonly HashSet<Guid> _dropped = [];

    /// <summary>Entities to insert, in dependency order.</summary>
    public IReadOnlyList<object> Added => _added;

    /// <summary>Data-relative folders and files of the added items.</summary>
    public IReadOnlyList<string> Paths => _paths;

    /// <summary>Skipped and renamed items for <c>RestoreReport.skipped</c>.</summary>
    public IReadOnlyList<SkippedItemDto> Skipped => _skipped;

    /// <summary>Ids of the added tracks, mixes, styles, lyrics sheets, voice profiles, assets, generations and conversations.</summary>
    public IReadOnlySet<Guid> AddedIds => _addedIds;

    /// <summary>Adds an entity with its files.</summary>
    /// <param name="entity">The staged entity.</param>
    /// <param name="id">Its id (null for rows without a reported id).</param>
    /// <param name="paths">Its data-relative folders or files.</param>
    public void Add(object entity, Guid? id, params string[] paths)
    {
        _added.Add(entity);
        _paths.AddRange(paths);
        if (id is { } value)
        {
            _addedIds.Add(value);
        }
    }

    /// <summary>Adds dependent rows (agent messages, runs, ...) that are not reported.</summary>
    /// <param name="entities">The rows.</param>
    public void AddRows(IEnumerable<object> entities) => _added.AddRange(entities);

    /// <summary>Skips an item; a same-source or dependency skip also skips its dependants.</summary>
    /// <param name="item">The item.</param>
    /// <param name="reason">The reason.</param>
    public void Skip(MergeItem item, SkippedItemReasonDto reason)
    {
        ArgumentNullException.ThrowIfNull(item);
        _skipped.Add(item.ToDto(reason, null));
        if (reason != SkippedItemReasonDto.SameId)
        {
            _dropped.Add(item.Id);
        }
    }

    /// <summary>Reports an added item that was renamed because its unique name is taken.</summary>
    /// <param name="item">The item with its archived name.</param>
    /// <param name="renamedTo">The new name.</param>
    public void Renamed(MergeItem item, string renamedTo)
    {
        ArgumentNullException.ThrowIfNull(item);
        _skipped.Add(item.ToDto(SkippedItemReasonDto.SameId, renamedTo));
    }

    /// <summary>True when the parent was skipped with its dependants.</summary>
    /// <param name="parent">The parent id (null: no parent).</param>
    /// <returns>True when dependants must be skipped.</returns>
    public bool IsDropped(Guid? parent) => parent is { } id && _dropped.Contains(id);
}

/// <summary>An archive item named in the merge report.</summary>
/// <param name="Category">Report category (<c>tracks</c>, <c>mixes</c>, ...).</param>
/// <param name="Id">Item id.</param>
/// <param name="Name">Display name.</param>
public sealed record MergeItem(string Category, Guid Id, string? Name)
{
    /// <summary>The report entry.</summary>
    /// <param name="reason">Why it was skipped.</param>
    /// <param name="renamedTo">The new name of a renamed item.</param>
    /// <returns>The entry.</returns>
    public SkippedItemDto ToDto(SkippedItemReasonDto reason, string? renamedTo) => new()
    {
        Category = Category,
        Id = Id.ToString("D", CultureInfo.InvariantCulture),
        Name = Name,
        Reason = reason,
        RenamedTo = renamedTo,
    };
}
