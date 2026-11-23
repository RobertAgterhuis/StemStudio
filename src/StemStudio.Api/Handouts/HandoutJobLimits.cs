using System.Globalization;
using System.Text.Json;
using StemStudio.Api.Worker;
using StemStudio.Contracts;

namespace StemStudio.Api.Handouts;

/// <summary>The size of a handout job as the worker measures it (worker 1.11.0 <c>jobs.handout_limits</c>).</summary>
/// <param name="BodyBytes">The JSON body.</param>
/// <param name="TextChars">Characters (Unicode scalar values, as Python counts them) of every string of the document.</param>
/// <param name="Cells">Layout cells: sections, bars, chords in bars, legend chords, lyric lines and their chords, setlist entries.</param>
/// <param name="NotationBytes">The distinct notation PDFs together.</param>
public sealed record HandoutJobSize(long BodyBytes, long TextChars, long Cells, long NotationBytes);

/// <summary>
/// The worker's four handout job limits, mirrored so the API refuses a document over them with a readable 422 <c>document</c>
/// error before it submits anything (020 security fixes; the worker would answer 422 <c>ValidationFailed</c>).
/// </summary>
public static class HandoutJobLimits
{
    /// <summary>Measures a request the way the worker does.</summary>
    /// <param name="request">The request as it would be sent.</param>
    /// <param name="notationSizes">File size per notation PDF path (unknown paths count 0).</param>
    /// <returns>The size.</returns>
    public static HandoutJobSize Measure(WorkerHandoutJobRequest request, IReadOnlyDictionary<string, long> notationSizes)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(notationSizes);
        var body = WorkerClient.HandoutBody(request);
        using var json = JsonDocument.Parse(body);
        var document = request.Document;
        var notation = document.Songs.SelectMany(song => song.Notation).Select(item => item.PdfPath).Distinct(StringComparer.Ordinal)
            .Sum(path => notationSizes.GetValueOrDefault(path));
        return new HandoutJobSize(body.Length, Chars(json.RootElement.GetProperty("document")), Cells(document), notation);
    }

    /// <summary>The first limit the size is over, as a text for the user; null when it fits.</summary>
    /// <param name="size">The size.</param>
    /// <returns>The text or null.</returns>
    public static string? Refusal(HandoutJobSize size)
    {
        ArgumentNullException.ThrowIfNull(size);
        return size switch
        {
            { BodyBytes: > ContractLimits.MaxHandoutRequestBytes } =>
                $"The handout is too large to send to the processing service (over {Mib(ContractLimits.MaxHandoutRequestBytes)} MiB). Leave out lyrics or songs.",
            { TextChars: > ContractLimits.MaxHandoutTextChars } =>
                $"The handout has more than {Number(ContractLimits.MaxHandoutTextChars)} characters of text. Leave out lyrics or songs.",
            { Cells: > ContractLimits.MaxHandoutLayoutCells } =>
                $"The handout has more than {Number(ContractLimits.MaxHandoutLayoutCells)} sections, bars, chords and lyric lines. Leave out ingredients or songs.",
            { NotationBytes: > ContractLimits.MaxHandoutNotationBytes } =>
                $"The sheet music of the handout is larger than {Mib(ContractLimits.MaxHandoutNotationBytes)} MiB together. Choose fewer sheet music items.",
            _ => null,
        };
    }

    private static long Chars(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString()!.EnumerateRunes().Count(),
        JsonValueKind.Object => element.EnumerateObject().Sum(property => Chars(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Sum(Chars),
        _ => 0,
    };

    private static long Cells(WorkerHandoutDocument document) =>
        (document.Setlist?.Entries.Count ?? 0) + document.Songs.Sum(Cells);

    private static long Cells(WorkerHandoutSong song)
    {
        var bars = song.Sections.SelectMany(section => section.ChordBars ?? []).ToList();
        var lines = song.Lyrics?.Lines ?? [];
        return song.Sections.Count + bars.Count + bars.Sum(bar => bar.Count) + song.ChordSymbols.Count + lines.Count + lines.Sum(line => line.Chords.Count);
    }

    private static string Mib(long bytes) => (bytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
