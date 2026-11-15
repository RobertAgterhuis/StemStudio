using StemStudio.Api.Styles;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Domain;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Styles;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Generations;

/// <summary>Creates songs from own lyrics (010 FR-001, FR-010, FR-012).</summary>
/// <param name="sheets">Lyrics sheets.</param>
/// <param name="styles">Styles.</param>
/// <param name="guard">The imitation guard with the library artists.</param>
/// <param name="availability">Generator availability.</param>
/// <param name="submitter">Submits the job.</param>
/// <param name="restyles">Draws the seeds.</param>
/// <param name="clock">Clock.</param>
public sealed class SongService(
    ILyricsSheetRepository sheets,
    IStyleRepository styles,
    LibraryStyleGuard guard,
    GenerationAvailability availability,
    GenerationSubmitter submitter,
    RestyleService restyles,
    TimeProvider clock)
{
    /// <summary>Title of a song from inline lyrics without a title.</summary>
    public const string UntitledSong = "Untitled song";

    /// <summary>Validates, snapshots the lyrics and style, builds the caption and submits the song.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 400, 404, 409 or 503.</returns>
    public async Task<IResult> CreateAsync(SongRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblems.Validation("language", "Choose a language.");
        }

        var (lyrics, lyricsProblem) = await LyricsAsync(request, cancellationToken);
        var style = request.StyleId is { } styleId ? await styles.FindAsync(styleId, cancellationToken) : null;
        if (lyricsProblem is not null || (request.StyleId is not null && style is null))
        {
            return lyricsProblem ?? StyleService.NotFound(request.StyleId!.Value);
        }

        return await ProblemAsync(request, cancellationToken) ?? await ReadyAsync(request, new SongInputs(lyrics!, style?.Snapshot()), cancellationToken);
    }

    private async Task<(LyricsSnapshot? Lyrics, IResult? Problem)> LyricsAsync(SongRequest request, CancellationToken cancellationToken)
    {
        if (request.LyricsId is { } id && request.LyricsText is null)
        {
            var sheet = await sheets.FindAsync(id, cancellationToken);
            return sheet is null
                ? (null, ApiProblems.NotFound($"Lyrics {id} do not exist."))
                : (sheet.Snapshot() with { Title = Title(request.Title) ?? sheet.Title }, null);
        }

        var text = LyricsNormaliser.Normalize(request.LyricsText);
        var error = LyricsError(request, text);
        return error is null ? (new LyricsSnapshot(null, Title(request.Title) ?? UntitledSong, request.Language, text), null) : (null, ApiProblems.Validation([error], string.Empty));
    }

    private static DomainError? LyricsError(SongRequest request, string text) => request switch
    {
        { LyricsId: not null } => new DomainError("lyricsId", "Give either lyricsId or lyricsText, not both."),
        _ when text.Length == 0 && !request.Instrumental => new DomainError("lyricsText", "Enter the lyrics, or choose instrumental."),
        _ when text.Length > LyricsRules.MaxTextLength => new DomainError("lyricsText", "The lyrics must be at most 4,096 characters."),
        _ => null,
    };

    private static string? Title(string? title) => string.IsNullOrWhiteSpace(title) ? null : GenerationText.Cut(title.Trim(), LyricsRules.MaxTitleLength);

    private async Task<IResult?> ProblemAsync(SongRequest request, CancellationToken cancellationToken)
    {
        var errors = SongRules.Validate(new SongOptions(request.Bpm, request.KeyScale, request.TimeSignature, request.DurationSeconds, request.Takes, request.Delivery, request.FreeCaption)).ToList();
        if (!LyricsRules.Languages.Contains(request.Language))
        {
            errors.Add(new DomainError("language", "Choose a supported language or unknown."));
        }

        if (request.StyleId is not null && request.FreeCaption is not null)
        {
            errors.Add(new DomainError("freeCaption", "Choose a style or write a caption, not both."));
        }

        return errors.Count > 0 ? ApiProblems.Validation(errors, string.Empty) : await GuardAsync(request, cancellationToken);
    }

    private async Task<IResult?> GuardAsync(SongRequest request, CancellationToken cancellationToken)
    {
        var hits = SongCaptionBuilder.Check(await guard.GuardAsync(cancellationToken), request.FreeCaption, request.Delivery);
        return hits.Count > 0 ? StyleProblems.Imitation(hits) : null;
    }

    private async Task<IResult> ReadyAsync(SongRequest request, SongInputs inputs, CancellationToken cancellationToken)
    {
        var readiness = await availability.GetAsync(cancellationToken);
        return readiness.IsReady ? await SubmitAsync(request, inputs, cancellationToken) : GenerationProblems.NotReady(readiness);
    }

    private async Task<IResult> SubmitAsync(SongRequest request, SongInputs inputs, CancellationToken cancellationToken)
    {
        var details = Details(request, inputs);
        var generation = Generation.CreateSong(Guid.NewGuid(), details, restyles.NewSeeds(request.Takes), clock.GetUtcNow().UtcDateTime);
        if (!details.Instrumental && LyricsDensity.IsTooDense(details.Lyrics.Text, details.DurationSeconds))
        {
            generation.AddWarnings([LyricsDensity.Warning(LyricsDensity.Words(details.Lyrics.Text), details.DurationSeconds)]);
        }

        return await submitter.AddAndSubmitAsync(generation, cancellationToken)
            ?? TypedResults.Accepted(ApiRoutes.Generation(generation.Id), GenerationMapper.ToDto(generation));
    }

    private static SongDetails Details(SongRequest request, SongInputs inputs)
    {
        var voice = StyleMapper.Map<LeadVoice>(request.LeadVoice);
        var delivery = string.IsNullOrWhiteSpace(request.Delivery) ? null : request.Delivery.Trim();
        return new SongDetails
        {
            Lyrics = inputs.Lyrics,
            Style = inputs.Style,
            FreeCaption = request.FreeCaption?.Trim(),
            LeadVoice = voice,
            Delivery = delivery,
            Instrumental = request.Instrumental,
            Bpm = request.Bpm,
            KeyScale = request.KeyScale,
            TimeSignature = request.TimeSignature,
            DurationSeconds = request.DurationSeconds,
            Caption = SongCaptionBuilder.Build(new SongCaptionInput(inputs.Style, request.FreeCaption, voice, delivery, request.Instrumental)),
        };
    }

    private sealed record SongInputs(LyricsSnapshot Lyrics, StyleSnapshot? Style);
}
