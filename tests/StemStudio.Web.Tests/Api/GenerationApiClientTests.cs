using System.Net;
using System.Text.Json;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Api;
using StemStudio.Web.Styles;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Style and generation operations of the typed client (backend contract 1.8.0, feature 009).</summary>
public sealed partial class StemStudioApiClientTests
{
    private static string Body(object value) => JsonSerializer.Serialize(value, StemStudioApiClient.Json);

    [Fact]
    public async Task GetGenerationStatus_ReadsTheState()
    {
        _handler.Respond(HttpStatusCode.OK, Body(GenerationData.Status()));

        var status = await _client.GetGenerationStatusAsync(Ct);

        status.State.ShouldBe(GenerationAvailabilityDto.Ready);
        _handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/generation/status");
    }

    [Fact]
    public async Task ListStyles_GetsTheLibrary()
    {
        _handler.Respond(HttpStatusCode.OK, Body(GenerationData.Styles()));

        var styles = await _client.ListStylesAsync(Ct);

        styles.Select(s => s.Name).ShouldBe(["Jazz trio", "Synthwave", "My bossa"]);
        _handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/styles");
    }

    [Fact]
    public async Task CreateRestyle_PostsTheRequestToTheTrack()
    {
        _handler.Respond(HttpStatusCode.Accepted, Body(GenerationData.Generation(GenerationStateDto.Queued)));
        var request = new RestyleRequest
        {
            StyleId = GenerationData.JazzId,
            Mode = RestyleModeDto.InPlace,
            RangeStartSeconds = 5,
            RangeEndSeconds = 15,
            IncludedStems = ["drums"],
            Strength = StrengthDto.Light,
            Takes = 2,
        };

        var generation = await _client.CreateRestyleAsync(TestData.TrackId, request, Ct);

        generation.Status.ShouldBe(GenerationStateDto.Queued);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, $"/api/tracks/{TestData.TrackId}/restyles"));
        sent.Body!.ShouldContain("\"mode\":\"inPlace\"");
        sent.Body!.ShouldContain("\"strength\":\"light\"");
        sent.Body!.ShouldContain("\"includedStems\":[\"drums\"]");
        sent.Body!.ShouldContain("\"takes\":2");
    }

    [Theory]
    [InlineData(null, false, "/api/generations")]
    [InlineData(GenerationKindDto.Restyle, false, "/api/generations?kind=restyle")]
    [InlineData(null, true, "/api/generations?sourceTrackId=0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a")]
    [InlineData(GenerationKindDto.VoiceConversion, true, "/api/generations?kind=voiceConversion&sourceTrackId=0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a")]
    public async Task ListGenerations_SendsTheFilters(GenerationKindDto? kind, bool byTrack, string expected)
    {
        _handler.Respond(HttpStatusCode.OK, Body(new[] { GenerationData.Generation() }));

        var list = await _client.ListGenerationsAsync(new GenerationQuery(kind, byTrack ? TestData.TrackId : null), Ct);

        list.ShouldHaveSingleItem().Id.ShouldBe(GenerationData.GenerationId);
        _handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe(expected);
    }

    [Fact]
    public async Task GenerationActions_UseTheirRoutes()
    {
        var json = Body(GenerationData.Generation(GenerationStateDto.Cancelled));
        _handler.Respond(HttpStatusCode.OK, json).Respond(HttpStatusCode.OK, json).Respond(HttpStatusCode.Accepted, json).Respond(HttpStatusCode.NoContent);
        var id = GenerationData.GenerationId;

        await _client.GetGenerationAsync(id, Ct);
        await _client.CancelGenerationAsync(id, Ct);
        await _client.RetryGenerationAsync(id, Ct);
        await _client.DeleteGenerationAsync(id, Ct);

        _handler.Requests.Select(r => (r.Method, r.PathAndQuery)).ShouldBe(
        [
            (HttpMethod.Get, $"/api/generations/{id}"),
            (HttpMethod.Post, $"/api/generations/{id}/cancel"),
            (HttpMethod.Post, $"/api/generations/{id}/retry"),
            (HttpMethod.Delete, $"/api/generations/{id}"),
        ]);
    }

    [Fact]
    public async Task ImportTake_ReturnsTheTrack()
    {
        _handler.Respond(HttpStatusCode.Accepted, TestData.CompletedTrackJson);

        var track = await _client.ImportTakeAsync(GenerationData.GenerationId, 2, Ct);

        track.Id.ShouldBe(TestData.TrackId);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, $"/api/generations/{GenerationData.GenerationId}/takes/2/import"));
    }

    [Fact]
    public async Task StyleWrites_UseTheirRoutes()
    {
        var json = Body(GenerationData.UserStyle());
        _handler.Respond(HttpStatusCode.OK, json).Respond(HttpStatusCode.Created, json).Respond(HttpStatusCode.OK, json)
            .Respond(HttpStatusCode.Created, json).Respond(HttpStatusCode.NoContent);
        var id = GenerationData.UserStyleId;
        var input = new StyleInput
        {
            Name = "My bossa",
            GenreFamily = GenreFamilyDto.Latin,
            TempoFeel = TempoFeelDto.Laidback,
            VocalHandling = VocalHandlingDto.Keep,
            DefaultStrength = StrengthDto.Light,
            Moods = ["dreamy"],
        };

        await _client.GetStyleAsync(id, Ct);
        await _client.CreateStyleAsync(input, Ct);
        await _client.UpdateStyleAsync(id, input, Ct);
        await _client.DuplicateStyleAsync(id, Ct);
        await _client.DeleteStyleAsync(id, Ct);

        _handler.Requests.Select(r => (r.Method, r.PathAndQuery)).ShouldBe(
        [
            (HttpMethod.Get, $"/api/styles/{id}"),
            (HttpMethod.Post, "/api/styles"),
            (HttpMethod.Put, $"/api/styles/{id}"),
            (HttpMethod.Post, $"/api/styles/{id}/duplicate"),
            (HttpMethod.Delete, $"/api/styles/{id}"),
        ]);
        _handler.Requests[1].Body!.ShouldContain("\"genreFamily\":\"latin\"");
        _handler.Requests[1].Body!.ShouldContain("\"moods\":[\"dreamy\"]");
    }

    [Fact]
    public async Task ImitationProblem_KeepsTheField()
    {
        _handler.RespondProblem(HttpStatusCode.BadRequest, """
            {"type":"imitation","title":"Styles cannot imitate an artist.","status":400,
             "errors":{"description":["Describe the music (genre, instruments, mood) instead of naming an artist or saying \"sounds like\"."]}}
            """);

        var ex = await Should.ThrowAsync<ApiException>(() => _client.CreateStyleAsync(GenerationData.UserStyle().ToInput(), Ct));

        ex.Error.FieldErrors.Keys.ShouldBe(["description"]);
        ex.Error.UserMessage.ShouldStartWith("Describe the music");
    }
}
