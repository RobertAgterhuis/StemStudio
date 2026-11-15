using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Artists;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="Artist"/> (table <c>Artists</c>, feature 005): MusicBrainz id key, genres, members and summary as JSON.</summary>
public sealed class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Artists");
        builder.HasKey(artist => artist.Mbid);
        builder.Property(artist => artist.Mbid).ValueGeneratedNever();
        builder.Property(artist => artist.Name).HasMaxLength(500);
        builder.Property(artist => artist.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(artist => artist.Country).HasMaxLength(2);
        builder.Property(artist => artist.Website).HasMaxLength(2048);
        builder.Property(artist => artist.WikidataId).HasMaxLength(32);
        ConfigureJson(builder);
    }

    private static void ConfigureJson(EntityTypeBuilder<Artist> builder)
    {
        builder.Ignore(artist => artist.Genres);
        builder.PrimitiveCollection<List<string>>("_genres").HasColumnName("Genres");
        builder.Ignore(artist => artist.Members);
        builder.ComplexCollection<List<ArtistMember>, ArtistMember>("_members", collection => collection.ToJson("Members"));
        builder.ComplexProperty(artist => artist.Summary, summary => summary.ToJson());
    }
}
