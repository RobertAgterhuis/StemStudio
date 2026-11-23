using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="PracticeNote"/> (table <c>PracticeNotes</c>, feature 020 data-model 5.2): key (track, family), cascade delete.</summary>
public sealed class PracticeNoteConfiguration : IEntityTypeConfiguration<PracticeNote>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PracticeNote> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PracticeNotes");
        builder.HasKey(note => new { note.TrackId, note.Family });
        builder.HasOne<Track>().WithMany().HasForeignKey(note => note.TrackId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(note => note.Family).HasConversion<string>().HasMaxLength(16);
        builder.Property(note => note.Text).HasMaxLength(PracticeNoteRules.MaxLength);
    }
}
