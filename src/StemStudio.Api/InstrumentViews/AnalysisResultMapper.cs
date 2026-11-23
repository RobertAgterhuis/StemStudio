using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.InstrumentViews;

/// <summary>
/// Turns a worker 1.9.0 analysis result into a domain document, checking the contract limits (data-model 7.6): at most
/// 4,000 segments, 50 key regions, 40,000 hits and 90,000 frames, pitch classes 0..11, frames 0..12,700, known enum values.
/// A result that breaks a rule is refused (null), so nothing outside the contract is ever stored.
/// </summary>
public static class AnalysisResultMapper
{
    private static readonly Dictionary<string, AnalysisWarningCode> WarningCodes = new(StringComparer.Ordinal)
    {
        ["vocabulary-reduced"] = AnalysisWarningCode.VocabularyReduced,
        ["grid-unreliable"] = AnalysisWarningCode.GridUnreliable,
        ["fallback-engine"] = AnalysisWarningCode.FallbackEngine,
        ["few-chords"] = AnalysisWarningCode.FewChords,
        ["key-uncertain"] = AnalysisWarningCode.KeyUncertain,
        ["few-voiced"] = AnalysisWarningCode.FewVoiced,
        ["detail-bleed"] = AnalysisWarningCode.DetailBleed,
    };

    private static readonly HashSet<string> ChordEngines = ["btc", "chroma-hmm", "fake"];
    private static readonly HashSet<string> DrumEngines = ["drum-onsets", "fake"];
    private static readonly HashSet<string> PitchEngines = ["pyin", "yin", "fake"];

    /// <summary>The document of a result of the expected kind.</summary>
    /// <param name="kind">The stored analysis kind.</param>
    /// <param name="result">The worker result.</param>
    /// <returns>The document, or null when the result is of another kind or breaks a rule.</returns>
    public static AnalysisDocument? ToDocument(AnalysisKind kind, WorkerAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        try
        {
            return Map(kind, result);
        }
        catch (InvalidResultException)
        {
            return null;
        }
    }

    /// <summary>The worker name of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><c>chords</c>, <c>drum-hits</c> or <c>vocal-pitch</c>.</returns>
    public static string WireKind(AnalysisKind kind) => kind switch
    {
        AnalysisKind.Chords => "chords",
        AnalysisKind.DrumHits => "drum-hits",
        _ => "vocal-pitch",
    };

    private static AnalysisDocument? Map(AnalysisKind kind, WorkerAnalysisResult result) => (kind, result.Kind) switch
    {
        (AnalysisKind.Chords, "chords") => Chords(result),
        (AnalysisKind.DrumHits, "drum-hits") => DrumHits(result),
        (AnalysisKind.VocalPitch, "vocal-pitch") => VocalPitch(result),
        _ => null,
    };

    private static ChordChart Chords(WorkerAnalysisResult result) => new()
    {
        Engine = Known(ChordEngines, result.Engine),
        Vocabulary = result.Vocabulary == "full" ? ChordVocabulary.Full : ChordVocabulary.Simple,
        Key = result.Key is { } key ? new SongKey(Pc(key.Tonic), Mode(key.Mode), Unit(key.Confidence), Alternative(key.Alternative)) : null,
        KeyRegions = Regions(result.KeyRegions),
        PreferFlats = result.PreferFlats,
        BeatSnapped = result.BeatSnapped,
        NoChords = result.NoChords,
        Segments = Bounded(result.Segments ?? [], ContractLimits.MaxChordSegments).Select(Segment).ToList(),
        Warnings = Warnings(result.Warnings),
    };

    private static string Known(HashSet<string> engines, string engine)
    {
        Require(engines.Contains(engine));
        return engine;
    }

    private static List<KeyRegion> Regions(IReadOnlyList<WorkerKeyRegion>? regions) => Bounded(regions ?? [], ContractLimits.MaxKeyRegions)
        .Select(region => new KeyRegion(NonNegative(region.StartMs), After(region.StartMs, region.EndMs), Pc(region.Tonic), Mode(region.Mode), Unit(region.Confidence))).ToList();

    private static int After(int start, int end)
    {
        Require(end > start);
        return end;
    }

    private static ChordSegment Segment(WorkerChordSegment segment)
    {
        Require(segment.StartMs >= 0 && segment.EndMs > segment.StartMs && HarteLabel.TryParse(segment.Label, out _));
        return new ChordSegment
        {
            StartMs = segment.StartMs,
            EndMs = segment.EndMs,
            Label = segment.Label,
            Root = OptionalPc(segment.Root),
            Quality = Quality(segment.Quality),
            Bass = OptionalPc(segment.Bass),
            Confidence = Unit(segment.Confidence),
        };
    }

    private static ChordQuality? Quality(string? quality) => quality is null ? null : ChordQualities.FromHarte(quality) ?? throw new InvalidResultException();

    private static int? OptionalPc(int? value) => value is { } pc ? Pc(pc) : null;

    private static DrumHitsDocument DrumHits(WorkerAnalysisResult result)
    {
        Require(DrumEngines.Contains(result.Engine));
        return new DrumHitsDocument
        {
            Engine = result.Engine,
            Pieces = (result.Pieces ?? []).Select(Piece).Distinct().ToList(),
            Hits = Bounded(result.Hits ?? [], ContractLimits.MaxDrumHits).Select(hit => new DrumHit(NonNegative(hit.TimeMs), Piece(hit.Piece), Unit(hit.Strength))).ToList(),
        };
    }

    private static VocalPitchDocument VocalPitch(WorkerAnalysisResult result)
    {
        Require(PitchEngines.Contains(result.Engine) && result.HopMs == 10 && result.VoicedRatio is not null);
        return new VocalPitchDocument
        {
            Engine = result.Engine,
            Frames = Frames(result.Frames),
            Source = Source(result.Source),
            VoicedRatio = Unit(result.VoicedRatio!.Value),
            Range = result.Range is { } range ? Range(range) : null,
            Warnings = Warnings(result.Warnings),
        };
    }

    private static PitchRange Range(WorkerPitchRange range)
    {
        Require(range.LowCents is >= 1 and <= ContractLimits.MaxPitchCents && range.HighCents is >= 1 and <= ContractLimits.MaxPitchCents && range.LowCents <= range.HighCents);
        return new PitchRange(range.LowCents, range.HighCents);
    }

    private static int[] Frames(IReadOnlyList<int>? frames)
    {
        var bounded = Bounded(frames ?? [], ContractLimits.MaxPitchFrames);
        Require(bounded.All(frame => frame is >= 0 and <= ContractLimits.MaxPitchCents));
        return bounded.ToArray();
    }

    private static VocalSource Source(string? source) => source switch
    {
        "vocals.lead" => VocalSource.VocalsLead,
        "vocals" => VocalSource.Vocals,
        _ => throw new InvalidResultException(),
    };

    private static List<AnalysisWarning> Warnings(IReadOnlyList<WorkerAnalysisWarning>? warnings) =>
        (warnings ?? []).Where(warning => WarningCodes.ContainsKey(warning.Code))
            .Select(warning => new AnalysisWarning(WarningCodes[warning.Code], Clip(warning.Text))).Take(16).ToList();

    private static string Clip(string? text) => text is null ? string.Empty : text.Length <= 300 ? text : text[..300];

    private static MusicalKey? Alternative(WorkerKey? alternative) => alternative is null ? null : new MusicalKey(Pc(alternative.Tonic), Mode(alternative.Mode));

    private static DrumPiece Piece(string piece) => piece switch
    {
        "kick" => DrumPiece.Kick,
        "snare" => DrumPiece.Snare,
        "toms" => DrumPiece.Toms,
        "hihat" => DrumPiece.Hihat,
        "cymbals" => DrumPiece.Cymbals,
        _ => throw new InvalidResultException(),
    };

    private static KeyMode Mode(string mode) => mode switch
    {
        "major" => KeyMode.Major,
        "minor" => KeyMode.Minor,
        _ => throw new InvalidResultException(),
    };

    private static IReadOnlyList<T> Bounded<T>(IReadOnlyList<T> items, int max)
    {
        Require(items.Count <= max);
        return items;
    }

    private static int Pc(int value)
    {
        Require(value is >= 0 and <= 11);
        return value;
    }

    private static int NonNegative(int value)
    {
        Require(value >= 0);
        return value;
    }

    private static double Unit(double value)
    {
        Require(value is >= 0 and <= 1);
        return value;
    }

    private static void Require(bool valid)
    {
        if (!valid)
        {
            throw new InvalidResultException();
        }
    }

    private sealed class InvalidResultException : Exception
    {
    }
}
