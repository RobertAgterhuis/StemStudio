using System.Text.Json.Serialization;

namespace StemStudio.Api.Worker;

/// <summary>Where a worker picture came from (worker contract 1.3.0 <c>ArtworkSource</c>).</summary>
public enum WorkerArtworkSource
{
    /// <summary>Spotify album image.</summary>
    Spotify,

    /// <summary>Cover embedded in an upload.</summary>
    Embedded,

    /// <summary>YouTube thumbnail.</summary>
    Youtube,

    /// <summary>Cover Art Archive release group front.</summary>
    [JsonStringEnumMemberName("coverartarchive")]
    CoverArtArchive,

    /// <summary>A normalised user picture.</summary>
    User,
}

/// <summary>A normalised picture of the worker (contract <c>ArtworkInfo</c>): two square JPEGs.</summary>
/// <param name="Source">Where the picture came from.</param>
/// <param name="Sha256">SHA-256 of the cover JPEG (the version).</param>
/// <param name="CoverBytes">Size of the 600 px cover.</param>
/// <param name="ThumbBytes">Size of the 160 px thumbnail.</param>
/// <param name="OriginUrl">Remote image URL (not shown to the user).</param>
public sealed record WorkerArtworkInfo(WorkerArtworkSource Source, string Sha256, int CoverBytes, int ThumbBytes, string? OriginUrl = null);

/// <summary>Result of <c>POST /v1/artwork</c> (contract <c>ArtworkUpload</c>).</summary>
/// <param name="ArtworkId">Identifier of the normalised files (kept one hour).</param>
/// <param name="Info">The normalised picture (source <c>user</c>).</param>
public sealed record WorkerArtworkUpload(string ArtworkId, WorkerArtworkInfo Info);

/// <summary>Which worker resource a picture belongs to.</summary>
public enum WorkerArtworkOwner
{
    /// <summary>A standard job: <c>/v1/jobs/{id}/artwork/{variant}</c>.</summary>
    Job,

    /// <summary>A metadata job: <c>/v1/metadata-jobs/{id}/artwork/{variant}</c>.</summary>
    MetadataJob,

    /// <summary>A normalised upload: <c>/v1/artwork/{id}/{variant}</c>.</summary>
    Upload,
}

/// <summary>One picture file of the worker.</summary>
/// <param name="Owner">The owning resource.</param>
/// <param name="Id">Its identifier (32 lower-case hex characters).</param>
/// <param name="Variant"><c>cover</c> or <c>thumb</c>.</param>
public sealed record WorkerArtworkRef(WorkerArtworkOwner Owner, string Id, string Variant);

/// <summary>A picture to normalise, streamed to <c>POST /v1/artwork</c>.</summary>
/// <param name="Content">The file content.</param>
/// <param name="FileName">File name sent in the multipart part.</param>
/// <param name="ContentType">Media type of the part, or null for <c>application/octet-stream</c>.</param>
public sealed record WorkerArtworkFile(Stream Content, string FileName, string? ContentType);
