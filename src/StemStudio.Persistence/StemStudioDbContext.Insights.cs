using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Handouts;

namespace StemStudio.Persistence;

/// <summary>Insights and handouts (feature 020): the practice notes.</summary>
public sealed partial class StemStudioDbContext
{
    /// <summary>Practice notes per track and family (feature 020).</summary>
    public DbSet<PracticeNote> PracticeNotes => Set<PracticeNote>();
}
