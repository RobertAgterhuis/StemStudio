using System.Buffers.Binary;
using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Recording;

namespace StemStudio.Api.Tests.Recording;

/// <summary>
/// Feature 015 T036: <c>createRecordingTake</c> - streamed multipart in either part order, every problem code of DELTA.md 1,
/// the padded <c>capture.wav</c>, the hidden asset with its provenance and recipe, the exact worker render request, and no
/// file left behind by a refused upload (security self-review: hostile headers, oversize, part names).
/// </summary>
public sealed class CreateTakeEndpointTests : RecordingTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upload_StoresThePaddedCapture_TheHiddenAsset_AndStartsTheRender(bool metadataFirst)
    {
        var track = await TrackAsync();
        var data = Samples(22_050, -300_000);

        using var response = await UploadAsync(track.Id, Form(Wav(data), Metadata() with { Name = " Groove ", PassIndex = 2 }, metadataFirst));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
        await ContractAssert.MatchesAsync(response, "createRecordingTake");
        var take = await response.BodyAsync<RecordingTakeDto>();
        response.Headers.Location!.ToString().ShouldBe(ApiRoutes.RecordingTakeOf(take.Id));
        (take.Number, take.Name, take.Status, take.Part, take.PassIndex, take.InputChannels, take.AudioUrl).ShouldBe((1, "Groove", TakeStatusDto.Saving, RecordingPartDto.Bass, (int?)2, 1, (string?)null));
        (take.StartSeconds, take.DurationSeconds, take.Latency.Milliseconds, take.Latency.Method).ShouldBe((1.0, 0.5, 24.99, LatencyMethodDto.Estimated));
        take.PeakDbfs.ShouldBe(20 * Math.Log10(300_000 / 8_388_608.0), 0.001);
        (take.Quiet, take.ClippedSamples).ShouldBe((false, 0));

        var capture = await File.ReadAllBytesAsync(CapturePath(take.AssetId), Ct);
        capture.Length.ShouldBe(44 + ((44_100 + 22_050) * 3));
        BinaryPrimitives.ReadUInt32LittleEndian(capture.AsSpan(40)).ShouldBe((uint)((44_100 + 22_050) * 3));
        capture.AsSpan(44, 44_100 * 3).ToArray().ShouldAllBe(value => value == 0);
        capture.AsSpan(44 + (44_100 * 3)).ToArray().ShouldBe(data);
        Directory.GetFiles(Path.GetDirectoryName(CapturePath(take.AssetId))!).Select(path => Path.GetFileName(path)).ShouldBe(new[] { "capture.wav" });

        var asset = await AssetAsync(take.AssetId);
        (asset.Kind, asset.Name, asset.Status, asset.WorkerJobId, asset.TrackId).ShouldBe((AssetKind.Audio, "Groove", AssetStatus.Queued, RenderJobId, (Guid?)track.Id));
        var provenance = asset.Provenance;
        (provenance.Origin, provenance.Recording, provenance.Recipe, provenance.Contents, provenance.TrackTitle, provenance.TrackArtist)
            .ShouldBe((AssetOrigin.Recording, new RecordingSource(take.Id), null, "Take 1 - Bass (recording)", "Never Gonna Give You Up", "Rick Astley"));
        asset.Placement.ShouldBeNull();

        var request = Worker.RenderCreates.ShouldHaveSingleItem();
        request.Inputs.ShouldBe([new WorkerRenderInput($"assets/{take.AssetId:D}/capture.wav", 1, 0)]);
        (request.MasterGain, request.Range, request.Output).ShouldBe((1.0, null, new WorkerRenderOutput(WorkerRenderFormat.Flac) { BitDepth = 24, Dither = false }));
        request.Metadata.ShouldBe(new WorkerRenderMetadata("Never Gonna Give You Up", "Rick Astley", "Take 1 - Bass (recording)"));
    }

    [Fact]
    public async Task Numbers_CountUp_DefaultNamesFollowThePart_AndTheListIsNewestFirst()
    {
        var track = await TrackAsync();
        var first = await CreateAsync(track.Id, Metadata() with { Part = RecordingPartDto.Piano });
        Factory.Clock.Advance(TimeSpan.FromSeconds(5));
        var second = await CreateAsync(track.Id, Metadata() with { Part = RecordingPartDto.None, MixId = Guid.NewGuid(), CalibrationId = Guid.NewGuid() });

        using var response = await Client.GetAsync(new Uri(ApiRoutes.TrackTakesOf(track.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "listRecordingTakes");
        var list = await response.BodyAsync<RecordingTakeListDto>();
        list.Items.Select(take => (take.Number, take.Name)).ShouldBe([(2, "Take 2"), (1, "Take 1 - Keys")]);
        (second.MixId, second.Latency.CalibrationId).ShouldBe(((Guid?)null, (Guid?)null));
        list.TotalSizeBytes.ShouldBe(0);
        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public async Task List_AndGet_OfUnknownIds_Are404()
    {
        (await Client.GetAsync(new Uri(ApiRoutes.TrackTakesOf(Guid.NewGuid()), UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var missing = await Client.GetAsync(new Uri(ApiRoutes.RecordingTakeOf(Guid.NewGuid()), UriKind.Relative), Ct);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(missing, "getRecordingTake");
    }

    [Fact]
    public async Task Get_ReturnsTheTake()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);

        using var response = await Client.GetAsync(new Uri(ApiRoutes.RecordingTakeOf(take.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getRecordingTake");
        (await response.BodyAsync<RecordingTakeDto>()).ShouldBe(take);
    }

    [Fact]
    public async Task UnknownTrack_Is404_AndUnfinishedTrack_Is409TrackNotReady()
    {
        using var unknown = await UploadAsync(Guid.NewGuid(), Form(Wav(Samples(22_050)), Metadata()));
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(unknown, "createRecordingTake");

        var queued = await Factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        using var notReady = await UploadAsync(queued.Id, Form(Wav(Samples(22_050)), Metadata()));
        notReady.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemTypeAsync(notReady)).ShouldBe(Problem("track-not-ready"));
        await ContractAssert.MatchesAsync(notReady, "createRecordingTake");
    }

    [Fact]
    public async Task The201stTake_Is409TakeLimitReached()
    {
        var track = await TrackAsync();
        await InScopeAsync(async context =>
        {
            for (var number = 1; number <= 200; number++)
            {
                var asset = Asset.CreateRecording(Guid.NewGuid(), "Take", new Provenance { Origin = AssetOrigin.Recording, TrackId = track.Id, TrackTitle = "Song", Contents = "x", Recording = new RecordingSource(Guid.NewGuid()) }, ApiFactory.Start.UtcDateTime);
                context.Assets.Add(asset);
                context.RecordingTakes.Add(RecordingTake.Create(Guid.NewGuid(), new NewTake
                {
                    TrackId = track.Id,
                    AssetId = asset.Id,
                    Number = number,
                    Part = RecordingPart.None,
                    SessionId = Guid.NewGuid(),
                    Partial = false,
                    StartSample = 0,
                    LengthSamples = 22_050,
                    InputMode = InputChannelMode.Mono1,
                    InputChannels = 1,
                    LatencySamples = 0,
                    LatencyMethod = LatencyMethod.Estimated,
                    Scan = new PeakScan(-6, 0, false),
                    Dropouts = 0,
                    RecordedAt = ApiFactory.Start.UtcDateTime,
                }, ApiFactory.Start.UtcDateTime));
            }

            await context.SaveChangesAsync(Ct);
            return true;
        });

        using var response = await UploadAsync(track.Id, Form(Wav(Samples(22_050)), Metadata()));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemTypeAsync(response)).ShouldBe(Problem("take-limit-reached"));
        Worker.RenderCreates.ShouldBeEmpty();
    }

    [Fact]
    public async Task LowDisk_Is409InsufficientDiskSpace_BeforeAnyDataIsStored()
    {
        var track = await TrackAsync();
        Disk.Available = (1L << 30) + 1000;

        using var response = await UploadAsync(track.Id, Form(Wav(Samples(22_050)), Metadata()));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemTypeAsync(response)).ShouldBe(Problem("insufficient-disk-space"));
        AssetFolders().ShouldBeEmpty();
    }

    public static TheoryData<string, byte[], string> RefusedWavs() => new()
    {
        { "16-bit", Wav(new byte[44_100], bits: 16), "unsupported-wav-format" },
        { "48 kHz", Wav(Samples(22_050), rate: 48_000), "unsupported-wav-format" },
        { "3 channels", Wav(Samples(22_050 * 3), channels: 3), "unsupported-wav-format" },
        { "not a wav", "ID3 this is an mp3"u8.ToArray(), "invalid-wav" },
        { "truncated data", Wav(Samples(22_050))[..^30], "invalid-wav" },
        { "header only", Wav(Samples(22_050))[..30], "invalid-wav" },
    };

    [Theory]
    [MemberData(nameof(RefusedWavs))]
    public async Task HostileOrWrongWavs_AreRefused_WithoutLeavingFiles(string name, byte[] wav, string code)
    {
        var track = await TrackAsync();

        using var response = await UploadAsync(track.Id, Form(wav, Metadata()));

        (await ProblemTypeAsync(response)).ShouldBe(Problem(code), name);
        response.StatusCode.ShouldBe(code == "invalid-wav" ? HttpStatusCode.BadRequest : HttpStatusCode.UnsupportedMediaType, name);
        await ContractAssert.MatchesAsync(response, "createRecordingTake");
        AssetFolders().ShouldBeEmpty(name);
        Worker.RenderCreates.ShouldBeEmpty(name);
    }

    public static TheoryData<string, RecordingTakeUploadDto, int, string> RefusedTakes() => new()
    {
        { "silent", Metadata(), 0, "silent-take" },
        { "outside", Metadata(startSample: 9_395_064 - 1000), 100_000, "take-outside-track" },
        { "speed", Metadata() with { Speed = 0.9 }, 100_000, "recording-speed" },
        { "transpose", Metadata() with { TransposeSemitones = 2 }, 100_000, "recording-speed" },
    };

    [Theory]
    [MemberData(nameof(RefusedTakes))]
    public async Task UnusableTakes_Are422_WithTheirCode(string name, RecordingTakeUploadDto metadata, int value, string code)
    {
        var track = await TrackAsync();

        using var response = await UploadAsync(track.Id, Form(Wav(Samples(22_050, value)), metadata));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, name);
        (await ProblemTypeAsync(response)).ShouldBe(Problem(code), name);
        await ContractAssert.MatchesAsync(response, "createRecordingTake");
        AssetFolders().ShouldBeEmpty(name);
    }

    [Fact]
    public async Task LengthMismatch_Is422()
    {
        var track = await TrackAsync();

        using var response = await UploadAsync(track.Id, Form(Wav(Samples(30_000)), Metadata()));

        (await ProblemTypeAsync(response)).ShouldBe(Problem("length-mismatch"));
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("passIndex")]
    [InlineData("latencySamples")]
    [InlineData("lengthSamples")]
    [InlineData("name")]
    [InlineData("deviceLabel")]
    [InlineData("inputMode")]
    [InlineData("dropouts")]
    public async Task InvalidMetadata_Is422Validation(string field)
    {
        var track = await TrackAsync();
        var metadata = field switch
        {
            "passIndex" => Metadata() with { PassIndex = 51 },
            "latencySamples" => Metadata() with { LatencySamples = 44_101 },
            "lengthSamples" => Metadata(lengthSamples: 22_049),
            "name" => Metadata() with { Name = "a\u0001" },
            "deviceLabel" => Metadata() with { DeviceLabel = new string('d', 201) },
            "inputMode" => Metadata() with { InputMode = InputChannelModeDto.Stereo },
            _ => Metadata() with { Dropouts = -1 },
        };

        using var response = await UploadAsync(track.Id, Form(Wav(Samples((int)Math.Max(1, metadata.LengthSamples))), metadata));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createRecordingTake");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain($"metadata.{field}");
        AssetFolders().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("missing audio")]
    [InlineData("missing metadata")]
    [InlineData("bad json")]
    [InlineData("not multipart")]
    [InlineData("huge metadata")]
    public async Task MalformedRequests_Are400(string name)
    {
        var track = await TrackAsync();
        HttpContent content = name switch
        {
            "missing audio" => Form(null, Metadata()),
            "missing metadata" => Form(Wav(Samples(22_050)), null),
            "bad json" => Form(Wav(Samples(22_050)), "{\"startSample\": \"soon\""),
            "huge metadata" => Form(Wav(Samples(22_050)), "\"" + new string('x', 70_000) + "\""),
            _ => new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };

        using var response = await UploadAsync(track.Id, content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, name);
        await ContractAssert.MatchesAsync(response, "createRecordingTake");
        AssetFolders().ShouldBeEmpty(name);
    }

    [Fact]
    public async Task DuplicateAudioParts_Are400()
    {
        var track = await TrackAsync();
        var form = Form(Wav(Samples(22_050)), Metadata());
        form.Add(new ByteArrayContent(Wav(Samples(22_050))), "audio", "second.wav");

        using var response = await UploadAsync(track.Id, form);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        AssetFolders().ShouldBeEmpty();
    }

    [Fact]
    public async Task UnreachableWorker_Is503_AndNothingIsStored()
    {
        var track = await TrackAsync();
        Worker.OnCreateRender = _ => throw new WorkerUnavailableException();

        using var response = await UploadAsync(track.Id, Form(Wav(Samples(22_050)), Metadata()));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(response, "createRecordingTake");
        AssetFolders().ShouldBeEmpty();
        (await TakesAsync()).ShouldBeEmpty();
    }
}

/// <summary>Feature 015 T036: the per-endpoint body limit (<c>StemStudio:Recording:MaxUploadBytes</c>).</summary>
public sealed class TakeUploadLimitTests : RecordingTestBase
{
    public TakeUploadLimitTests()
        : base(new Dictionary<string, string?> { ["StemStudio:Recording:MaxUploadBytes"] = "70000" })
    {
    }

    [Fact]
    public async Task DataLargerThanTheLimit_Is413()
    {
        var track = await TrackAsync();

        using var response = await UploadAsync(track.Id, Form(Wav(Samples(30_000)), Metadata(lengthSamples: 30_000)));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        await ContractAssert.MatchesAsync(response, "createRecordingTake");
        AssetFolders().ShouldBeEmpty();
    }

    [Fact]
    public async Task ABodyLargerThanTheLimitAndOverhead_Is413_BeforeReading()
    {
        var track = await TrackAsync();
        var body = new ByteArrayContent(new byte[70_000 + (1024 * 1024) + 1]);
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data") { Parameters = { new System.Net.Http.Headers.NameValueHeaderValue("boundary", "x") } };

        using var response = await UploadAsync(track.Id, body);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task DataWithinTheLimit_IsAccepted()
    {
        var track = await TrackAsync();

        (await CreateAsync(track.Id)).Status.ShouldBe(TakeStatusDto.Saving);
    }
}
