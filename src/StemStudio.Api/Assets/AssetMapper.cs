using StemStudio.Api.Library;
using StemStudio.Api.Mixes;
using StemStudio.Api.Scores;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Mixes;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;

namespace StemStudio.Api.Assets;

/// <summary>Maps library items and export recipes between the Domain and the contract DTOs (feature 007).</summary>
public static class AssetMapper
{
    /// <summary>Maps an item.</summary>
    /// <param name="asset">The item.</param>
    /// <param name="trackAvailable">Whether its track still exists.</param>
    /// <returns>The DTO.</returns>
    public static AssetDto ToDto(Asset asset, bool trackAvailable)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return WithResult(Header(asset, trackAvailable), asset);
    }

    private static AssetDto Header(Asset asset, bool trackAvailable) => new()
    {
        Id = asset.Id,
        Kind = Enum.Parse<AssetKindDto>(asset.Kind.ToString()),
        Name = asset.Name,
        Status = Enum.Parse<AssetStatusDto>(asset.Status.ToString()),
        Progress = asset.Progress,
        Provenance = ToDto(asset.Provenance, trackAvailable),
        Placement = LibraryMapper.PlacementOf(asset),
        SizeBytes = asset.SizeBytes,
        CreatedAt = TrackMapper.Utc(asset.CreatedAt),
        UpdatedAt = TrackMapper.Utc(asset.UpdatedAt),
    };

    private static AssetDto WithResult(AssetDto dto, Asset asset) => dto with
    {
        Error = asset.Error,
        Files = asset.Files.Select(file => ToDto(asset.Id, file)).ToList(),
        Audio = asset.Audio is { } audio ? ToDto(audio) : null,
        Score = asset.Score is { } score ? ScoreMapper.ToDto(score) : null,
    };

    /// <summary>Maps a recipe to its DTO.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The DTO.</returns>
    public static ExportRecipeDto ToDto(ExportRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new ExportRecipeDto
        {
            TrackId = recipe.TrackId,
            Mode = recipe.Mode == ExportMode.Raw ? ExportModeDto.Raw : ExportModeDto.Mix,
            Channels = recipe.Channels,
            Mix = recipe.Mix is { } mix ? ToDto(mix) : null,
            Range = recipe.Range is { } range ? new RangeDto(range.StartSeconds, range.EndSeconds) : null,
            Format = new ExportFormatDto(ToDto(recipe.Format.Format), recipe.Format.BitDepth, recipe.Format.Mp3BitrateKbps),
        };
    }

    /// <summary>Maps an export mix DTO.</summary>
    /// <param name="mix">The DTO.</param>
    /// <returns>The domain mix (not validated).</returns>
    public static ExportMix ToDomain(ExportMixDto mix)
    {
        ArgumentNullException.ThrowIfNull(mix);
        return new ExportMix(mix.MasterVolume, (mix.Channels ?? []).Select(MixMapper.ToDomain).ToList())
        {
            Expanded = (mix.Expanded ?? []).ToList(),
            DetailChannels = (mix.DetailChannels ?? []).Select(MixMapper.ToDomain).ToList(),
        };
    }

    /// <summary>Maps a file format.</summary>
    /// <param name="format">The DTO format.</param>
    /// <returns>The domain format.</returns>
    public static AudioFormat ToDomain(AudioFileFormatDto format) => Enum.Parse<AudioFormat>(format.ToString());

    private static AudioFileFormatDto ToDto(AudioFormat format) => Enum.Parse<AudioFileFormatDto>(format.ToString());

    private static AssetFileDto ToDto(Guid assetId, AssetFile file) =>
        new(Enum.Parse<AssetFileRoleDto>(file.Role, ignoreCase: true), file.ContentType, file.SizeBytes, file.Sha256, ApiRoutes.AssetFile(assetId, file.Role));

    private static AudioFactsDto ToDto(AudioFacts audio) => new()
    {
        Format = ToDto(audio.Format),
        BitDepth = audio.BitDepth,
        Mp3BitrateKbps = audio.Mp3BitrateKbps,
        SampleRate = audio.SampleRate,
        Channels = audio.Channels,
        DurationSeconds = audio.DurationSeconds,
        PeakDbfs = audio.PeakDbfs,
        AppliedGainDb = audio.AppliedGainDb,
    };

    private static ProvenanceDto ToDto(Provenance provenance, bool trackAvailable) => new()
    {
        Origin = Enum.Parse<AssetOriginDto>(provenance.Origin.ToString()),
        TrackId = provenance.TrackId,
        TrackTitle = provenance.TrackTitle,
        TrackArtist = provenance.TrackArtist,
        TrackAvailable = trackAvailable,
        SourceAssetIds = provenance.SourceAssetIds,
        Contents = provenance.Contents,
        Recipe = provenance.Recipe is { } recipe ? ToDto(recipe) : null,
        ScoreRecipe = provenance.ScoreRecipe is { } scoreRecipe ? ScoreMapper.ToDto(scoreRecipe) : null,
        Generation = provenance.Generation is { } generation ? ToDto(generation) : null,
    };

    private static GenerationProvenanceDto ToDto(GenerationSource generation) => new()
    {
        GenerationId = generation.GenerationId,
        TakeIndex = generation.TakeIndex,
        Kind = Enum.Parse<GenerationKindDto>(generation.Kind.ToString()),
    };

    private static ExportMixDto ToDto(ExportMix mix) =>
        new(mix.MasterVolume, mix.Channels.Select(c => new ChannelSettingsDto(c.Stem, c.Volume, c.Pan, c.Mute, c.Solo)).ToList())
        {
            Expanded = mix.Expanded,
            DetailChannels = mix.DetailChannels.Select(c => new DetailChannelSettingsDto(c.Parent, c.Stem, c.Volume, c.Pan, c.Mute, c.Solo)).ToList(),
        };
}
