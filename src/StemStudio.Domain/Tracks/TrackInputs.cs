namespace StemStudio.Domain.Tracks;

/// <summary>A worker status report for a non-terminal state.</summary>
/// <param name="Status">Queued, downloading, separating or analysing.</param>
/// <param name="Stage">Human-readable stage.</param>
/// <param name="Progress">Progress 0..100 (values outside are clamped).</param>
public sealed record TrackProgress(TrackStatus Status, string? Stage, int Progress);

/// <summary>Source metadata reported by the worker.</summary>
/// <param name="Title">Title; blank keeps the current title.</param>
/// <param name="Artist">Artist, when known.</param>
public sealed record SourceMetadata(string? Title, string? Artist);

/// <summary>The result of a completed job, after every stem was stored locally.</summary>
/// <param name="DurationSeconds">Duration, &gt; 0 and &lt;= 900.</param>
/// <param name="Model">Model name: <c>htdemucs_6s</c>, <c>htdemucs</c> or <c>fake</c>.</param>
/// <param name="Stems">Exactly the stem set of the model.</param>
/// <param name="BeatGrid">Beat analysis.</param>
public sealed record TrackCompletion(double DurationSeconds, string Model, IReadOnlyList<Stem> Stems, BeatGrid BeatGrid);

/// <summary>An upload kept by the API.</summary>
/// <param name="OriginalFileName">File name as sent by the user (only used for the title).</param>
/// <param name="StoredFileName">Generated name under <c>uploads/</c>, <c>&lt;sha256&gt;&lt;ext&gt;</c>.</param>
public sealed record UploadedFile(string OriginalFileName, string StoredFileName);
