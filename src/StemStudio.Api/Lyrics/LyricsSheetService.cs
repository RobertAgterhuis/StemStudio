using System.Text;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain;
using StemStudio.Domain.Lyrics;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Lyrics;

/// <summary>Own lyrics sheets: CRUD, upload and search (010 FR-003); songs keep their snapshots.</summary>
/// <param name="sheets">Repository.</param>
/// <param name="clock">Clock.</param>
public sealed class LyricsSheetService(ILyricsSheetRepository sheets, TimeProvider clock)
{
    /// <summary>Largest upload (64 KiB).</summary>
    public const int MaxUploadBytes = 65_536;

    private static readonly string[] Extensions = [".txt", ".lrc", ".md"];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Sheets, newest change first.</summary>
    /// <param name="q">Search text (at most 100).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 400.</returns>
    public async Task<IResult> ListAsync(string? q, CancellationToken cancellationToken) => q?.Length > 100
        ? ApiProblems.Validation("q", "Must be at most 100 characters.")
        : TypedResults.Ok((await sheets.ListAsync(q, cancellationToken)).Select(ToDto).ToList());

    /// <summary>One sheet.</summary>
    /// <param name="id">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await sheets.FindAsync(id, cancellationToken) is { } sheet ? TypedResults.Ok(ToDto(sheet)) : NotFound(id);

    /// <summary>Saves new lyrics (normalised).</summary>
    /// <param name="input">The input.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201 or 400.</returns>
    public async Task<IResult> CreateAsync(LyricsInput? input, CancellationToken cancellationToken)
    {
        LyricsSheet sheet;
        try
        {
            sheet = LyricsSheet.Create(Guid.NewGuid(), Content(input), Now);
        }
        catch (DomainValidationException invalid)
        {
            return ApiProblems.Validation(invalid.Errors, string.Empty);
        }

        await sheets.AddAsync(sheet, cancellationToken);
        return TypedResults.Created(ApiRoutes.LyricsSheet(sheet.Id), ToDto(sheet));
    }

    /// <summary>Replaces a sheet.</summary>
    /// <param name="id">Sheet id.</param>
    /// <param name="input">The input.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400 or 404.</returns>
    public async Task<IResult> UpdateAsync(Guid id, LyricsInput? input, CancellationToken cancellationToken)
    {
        var sheet = await sheets.FindAsync(id, cancellationToken);
        if (sheet is null)
        {
            return NotFound(id);
        }

        var errors = LyricsRules.Validate(Content(input));
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors, string.Empty);
        }

        sheet.Update(Content(input), Now);
        await sheets.SaveAsync(cancellationToken);
        return TypedResults.Ok(ToDto(sheet));
    }

    /// <summary>Deletes a sheet.</summary>
    /// <param name="id">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var sheet = await sheets.FindAsync(id, cancellationToken);
        if (sheet is null)
        {
            return NotFound(id);
        }

        await sheets.RemoveAsync(sheet, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Saves lyrics from a .txt, .lrc or .md file (UTF-8, at most 64 KiB).</summary>
    /// <param name="form">The multipart form.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201, 400, 413 or 415.</returns>
    public async Task<IResult> UploadAsync(IFormCollection form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var file = form.Files[LyricsUploadForm.File];
        if (UploadProblem(file) is { } problem)
        {
            return problem;
        }

        var text = await DecodeAsync(file!, cancellationToken);
        if (text is null)
        {
            return ApiProblems.Validation("file", "The file is not UTF-8 text.");
        }

        var title = form[LyricsUploadForm.Title].FirstOrDefault() ?? Path.GetFileNameWithoutExtension(file!.FileName);
        return await CreateAsync(new LyricsInput { Title = title, Language = form[LyricsUploadForm.Language].FirstOrDefault() ?? "unknown", Text = text }, cancellationToken);
    }

    private static IResult? UploadProblem(IFormFile? file) => file switch
    {
        null => ApiProblems.Validation("file", "Choose a .txt, .lrc or .md file."),
        _ when !Extensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()) => TypedResults.Problem(
            "Only .txt, .lrc and .md lyrics files are supported.", statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Unsupported file type"),
        { Length: > MaxUploadBytes } => TypedResults.Problem(
            "Lyrics files can be at most 64 KiB (65,536 bytes).", statusCode: StatusCodes.Status413PayloadTooLarge, title: "File too large"),
        _ => null,
    };

    private static async Task<string?> DecodeAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        try
        {
            return StrictUtf8.GetString(buffer.ToArray()).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static LyricsContent Content(LyricsInput? input) => new(input?.Title ?? string.Empty, input?.Language ?? string.Empty, input?.Text ?? string.Empty);

    private static IResult NotFound(Guid id) => ApiProblems.NotFound($"Lyrics {id} do not exist.");

    private static LyricsSheetDto ToDto(LyricsSheet sheet) => new()
    {
        Id = sheet.Id,
        Title = sheet.Title,
        Language = sheet.Language,
        Text = sheet.Text,
        CreatedAt = TrackMapper.Utc(sheet.CreatedAt),
        UpdatedAt = TrackMapper.Utc(sheet.UpdatedAt),
    };
}
