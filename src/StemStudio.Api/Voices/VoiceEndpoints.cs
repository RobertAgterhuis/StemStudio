using StemStudio.Contracts;
using StemStudio.Contracts.Voices;
using StemStudio.Api.LibraryCare.Gate;

namespace StemStudio.Api.Voices;

/// <summary>Maps the voice routes of backend 1.9.0 (feature 010).</summary>
public static class VoiceEndpoints
{
    /// <summary>Route of one profile.</summary>
    public const string ProfileRoute = ApiRoutes.VoiceProfiles + "/{profileId:guid}";

    /// <summary>Maps the voice endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapVoiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.VoiceConsent, VoiceProfileService.Statement).WithName("getVoiceConsentStatement").WithTags("voices").Produces<ConsentStatementDto>();
        app.MapGet(ApiRoutes.VoiceProfiles, (VoiceProfileService voices, CancellationToken ct) => voices.ListAsync(ct))
            .WithName("listVoiceProfiles").WithTags("voices").Produces<List<VoiceProfileDto>>();
        app.MapPost(ApiRoutes.VoiceProfiles, CreateAsync).WithName("createVoiceProfile").WithTags("voices").DisableAntiforgery()
            .Produces<VoiceProfileDto>(StatusCodes.Status201Created).ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapGet(ProfileRoute, (Guid profileId, VoiceProfileService voices, CancellationToken ct) => voices.GetAsync(profileId, ct))
            .WithName("getVoiceProfile").WithTags("voices").Produces<VoiceProfileDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapDelete(ProfileRoute, (Guid profileId, bool? deleteTakes, VoiceProfileDeletion deletion, CancellationToken ct) => deletion.DeleteAsync(profileId, deleteTakes ?? false, ct))
            .WithName("deleteVoiceProfile").RequireGateOpen().WithTags("voices").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet(ProfileRoute + "/sample", (Guid profileId, VoiceProfileService voices, CancellationToken ct) => voices.GetSampleAsync(profileId, ct))
            .WithName("getVoiceSample").WithTags("voices").Produces(StatusCodes.Status200OK, contentType: "audio/flac")
            .Produces(StatusCodes.Status206PartialContent, contentType: "audio/flac").Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound).Produces(StatusCodes.Status416RangeNotSatisfiable);
        return app;
    }

    private static async Task<IResult> CreateAsync(HttpRequest request, VoiceProfileService voices, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return ApiProblems.Validation(VoiceProfileForm.Sample, "Send the recording as multipart/form-data.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Files[VoiceProfileForm.Sample] is not { } file)
        {
            return ApiProblems.Validation(VoiceProfileForm.Sample, "Add the recording.");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return await voices.CreateAsync(new VoiceProfileRequest(
            buffer.ToArray(),
            file.FileName,
            form[VoiceProfileForm.DisplayName].FirstOrDefault(),
            form[VoiceProfileForm.PersonName].FirstOrDefault(),
            bool.TryParse(form[VoiceProfileForm.ConsentConfirmed].FirstOrDefault(), out var confirmed) && confirmed,
            form[VoiceProfileForm.ConsentStatementVersion].FirstOrDefault(),
            form[VoiceProfileForm.Source].FirstOrDefault()), cancellationToken);
    }
}
