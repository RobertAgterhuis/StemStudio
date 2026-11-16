using StemStudio.Api.Styles;
using StemStudio.Contracts.Generation;
using StemStudio.Domain.Generations;

namespace StemStudio.Api.Generations;

/// <summary>Maps song and voice-conversion details to the backend contract (1.9.0).</summary>
public static class SongMapper
{
    /// <summary>Song details.</summary>
    /// <param name="song">The details.</param>
    /// <returns>The DTO.</returns>
    public static SongDetailsDto ToDto(SongDetails song)
    {
        ArgumentNullException.ThrowIfNull(song);
        return new SongDetailsDto
        {
            Lyrics = new LyricsSnapshotDto { LyricsId = song.Lyrics.LyricsId, Title = song.Lyrics.Title, Language = song.Lyrics.Language, Text = song.Lyrics.Text },
            Style = song.Style is { } style ? StyleMapper.ToDto(style) : null,
            FreeCaption = song.FreeCaption,
            Voice = new SongVoiceDto { LeadVoice = StyleMapper.Map<LeadVoiceDto>(song.LeadVoice), Delivery = song.Delivery },
            Instrumental = song.Instrumental,
            Bpm = song.Bpm,
            KeyScale = song.KeyScale,
            TimeSignature = song.TimeSignature,
            DurationSeconds = song.DurationSeconds,
            Caption = song.Caption,
        };
    }

    /// <summary>Voice-conversion details.</summary>
    /// <param name="conversion">The details.</param>
    /// <returns>The DTO.</returns>
    public static VoiceConversionDetailsDto ToDto(VoiceConversionDetails conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        return new VoiceConversionDetailsDto
        {
            SourceGenerationId = conversion.SourceGenerationId,
            SourceTakeIndex = conversion.SourceTakeIndex,
            VoiceProfileId = conversion.VoiceProfileId,
            VoiceProfileName = conversion.VoiceProfileName,
            ImportedTrackId = conversion.ImportedTrackId,
            PitchShift = conversion.PitchShift,
            AutoShift = conversion.AutoShift,
        };
    }
}
