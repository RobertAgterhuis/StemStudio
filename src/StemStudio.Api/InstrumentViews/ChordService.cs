using System.Text;
using StemStudio.Api.Lyrics;
using StemStudio.Api.SongSections;
using StemStudio.Api.Tracks;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.InstrumentViews;

/// <summary>A ready (or stale) chord chart of a track.</summary>
/// <param name="Track">The track (with its lyrics).</param>
/// <param name="Analysis">The analysis.</param>
/// <param name="Chart">The chart.</param>
public sealed record ReadyChords(Track Track, TrackAnalysis Analysis, ChordChart Chart);

/// <summary>Chord edits, the chord sheet and ChordPro (data-model 2.6, FR-015, FR-016).</summary>
/// <param name="tracks">Tracks.</param>
/// <param name="analyses">Analyses.</param>
/// <param name="gate">The analysis lock (edits never race a re-run).</param>
/// <param name="clock">Clock.</param>
public sealed class ChordService(ITrackRepository tracks, ITrackAnalysisRepository analyses, AnalysisGate gate, TimeProvider clock)
{
    /// <summary>Longest attachment base name.</summary>
    public const int MaxFileNameLength = 100;

    /// <summary>File name when the title has no usable character.</summary>
    public const string DefaultFileName = "chords";

    /// <summary>The ready or stale chart of a track: the result, or the 404/409 problem.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The chart or the problem.</returns>
    public async Task<(ReadyChords? Ready, IResult? Problem)> FindReadyAsync(Guid trackId, CancellationToken cancellationToken)
    {
        if (await tracks.FindAsync(trackId, cancellationToken) is not { } track)
        {
            return (null, TrackService.NotFound(trackId));
        }

        return await analyses.FindAsync(trackId, AnalysisKind.Chords, cancellationToken) is { Status: AnalysisStatus.Ready or AnalysisStatus.Stale, Document: ChordChart chart } analysis
            ? (new ReadyChords(track, analysis, chart), null)
            : (null, InstrumentViewProblems.ChordsNotReady());
    }

    /// <summary><c>PUT /api/tracks/{trackId}/chords/segments/{segmentIndex}</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="segmentIndex">0-based segment index.</param>
    /// <param name="request">The new label.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 with the analysis, 404, 409 or 422.</returns>
    public Task<IResult> EditAsync(Guid trackId, int segmentIndex, EditChordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return gate.RunAsync(
            trackId,
            AnalysisKind.Chords,
            async () =>
            {
                var (ready, problem) = await FindReadyAsync(trackId, cancellationToken);
                return ready is null ? problem! : await EditLockedAsync(ready, segmentIndex, request.Label, cancellationToken);
            },
            cancellationToken);
    }

    private async Task<IResult> EditLockedAsync(ReadyChords ready, int segmentIndex, string label, CancellationToken cancellationToken)
    {
        try
        {
            ready.Analysis.EditChord(segmentIndex, label, clock.GetUtcNow().UtcDateTime);
        }
        catch (DomainValidationException invalid)
        {
            return PracticeFlowProblems.Invalid(invalid.Errors);
        }

        await analyses.SaveAsync(cancellationToken);
        return TypedResults.Ok(InstrumentViewMapper.ToChordsDto(AnalysisView.Of(ready.Track, AnalysisKind.Chords, ready.Analysis)));
    }

    /// <summary><c>GET /api/tracks/{trackId}/chords/sheet</c> (no-store: it holds lyric text).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="display">Transpose and capo.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404, 409 or 422.</returns>
    public async Task<IResult> SheetAsync(Guid trackId, ChordDisplay display, CancellationToken cancellationToken)
    {
        var (ready, problem) = await FindReadyForDisplayAsync(trackId, display, cancellationToken);
        return LyricsProblems.NotStored(ready is null ? problem! : TypedResults.Ok(ToDto(Build(ready, display))));
    }

    /// <summary><c>GET /api/tracks/{trackId}/chords/chordpro</c>: attachment <c>&lt;title&gt;.cho</c>, UTF-8 text.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="display">Transpose and capo.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404, 409 or 422.</returns>
    public async Task<IResult> ChordProAsync(Guid trackId, ChordDisplay display, CancellationToken cancellationToken)
    {
        var (ready, problem) = await FindReadyForDisplayAsync(trackId, display, cancellationToken);
        if (ready is null)
        {
            return problem!;
        }

        var header = new ChordProHeader(ready.Track.Title, ready.Track.Artist, ready.Chart.Key?.Key);
        var text = ChordProWriter.Write(header, Build(ready, display));
        return LyricsProblems.NotStored(TypedResults.File(Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", FileName(ready.Track.Title)));
    }

    private static char Safe(char character) => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_' or '(' or ')' or '\'' ? character : '_';

    /// <summary>A safe attachment name: letters, digits, spaces, <c>-</c>, <c>_</c>, <c>(</c>, <c>)</c> and <c>'</c> of the title, at most 100, plus <c>.cho</c>.</summary>
    /// <param name="title">The track title.</param>
    /// <returns>The file name.</returns>
    public static string FileName(string? title)
    {
        var name = new string((title ?? string.Empty).Select(Safe).ToArray()).Trim(' ', '_');
        name = name.Length > MaxFileNameLength ? name[..MaxFileNameLength].TrimEnd() : name;
        return (name.Length == 0 ? DefaultFileName : name) + ".cho";
    }

    private async Task<(ReadyChords? Ready, IResult? Problem)> FindReadyForDisplayAsync(Guid trackId, ChordDisplay display, CancellationToken cancellationToken) =>
        InstrumentViewProblems.DisplayOutOfRange(display.Transpose, display.Capo) is { } invalid
            ? (null, invalid)
            : await FindReadyAsync(trackId, cancellationToken);

    private static ChordSheet Build(ReadyChords ready, ChordDisplay display)
    {
        var lyrics = ready.Track.Lyrics is { Status: LyricsStatus.Found, Document: { } document } found ? (document, found.OffsetMs) : (null, 0);
        return ChordSheetBuilder.Build(new SheetSource(ready.Chart, lyrics.document, lyrics.Item2, ready.Track.BeatGrid), display);
    }

    private static ChordSheetDto ToDto(ChordSheet sheet) => new()
    {
        Transpose = sheet.Display.Transpose,
        Capo = sheet.Display.Capo,
        Rows = sheet.Rows.Select(row => new ChordSheetRowDto
        {
            Text = row.Text,
            Chords = row.Chords.Select(chord => new ChordSheetChordDto { CharIndex = chord.CharIndex, Symbol = chord.Symbol, Lead = chord.Lead }).ToList(),
        }).ToList(),
    };
}
