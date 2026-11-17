using System.Globalization;

namespace StemStudio.Web.Library;

/// <summary>
/// Tells the live search's own URL changes apart from genuine navigations (feature 022, FR-026). Every own navigation
/// carries a history entry state unique to this instance (<see cref="Next"/>). Blazor Server reports it back one round
/// trip later, and the router sets the page's parameters again, so the same echo may arrive twice, late, out of order
/// or not at all. An own state seen for the first time is such an echo; the location taken over last, seen again, is a
/// repeat; an own state that already came back once is Back or Forward to that history entry, a genuine navigation, as
/// is every location without an own state (page load, a link, another component's navigation).
/// </summary>
public sealed class OwnNavigations
{
    private const int Kept = 64;
    private readonly string _prefix = $"ss-search:{Guid.NewGuid():N}:";
    private readonly HashSet<long> _arrived = [];
    private readonly Queue<long> _arrivalOrder = new();
    private long _issued;
    private long _forgottenUpTo;
    private long? _current;

    /// <summary>The history entry state for the next own navigation.</summary>
    /// <returns>A state no other instance gives out.</returns>
    public string Next() => _prefix + (++_issued).ToString(CultureInfo.InvariantCulture);

    /// <summary>Takes over a location's history entry state and says whether the location is an own echo or a repeat.</summary>
    /// <param name="state">The history entry state of the location, or null.</param>
    /// <returns>True when the location must not change the search text.</returns>
    public bool IsEcho(string? state)
    {
        var id = IdOf(state);
        if (id is not { } own)
        {
            _current = null;
            return false;
        }

        if (own == _current)
        {
            return true;
        }

        var firstArrival = own > _forgottenUpTo && !_arrived.Contains(own);
        Arrive(own);
        return firstArrival;
    }

    private long? IdOf(string? state) =>
        state is not null && state.StartsWith(_prefix, StringComparison.Ordinal)
            && long.TryParse(state.AsSpan(_prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    private void Arrive(long id)
    {
        _current = id;
        if (!_arrived.Add(id))
        {
            return;
        }

        _arrivalOrder.Enqueue(id);
        if (_arrivalOrder.Count > Kept)
        {
            var forgotten = _arrivalOrder.Dequeue();
            _arrived.Remove(forgotten);
            _forgottenUpTo = Math.Max(_forgottenUpTo, forgotten);
        }
    }
}
