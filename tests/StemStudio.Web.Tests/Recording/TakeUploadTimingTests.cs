using System.Diagnostics;
using System.Net;
using Microsoft.JSInterop;
using NSubstitute;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Tests.Recording;

/// <summary>
/// 015 T043 / research S-02 (.NET half): a 5-minute stereo 24-bit take (79,380,044 bytes) goes from the engine's stream
/// reference through <see cref="TakeUploader"/> and the typed client into an HTTP request that is read to the end, well
/// within SC-004's 15 s. The circuit transfer and the real API are measured by the CTO on the reference machine (T089).
/// </summary>
public sealed class TakeUploadTimingTests
{
    private const long FiveMinutesStereo = 44 + (300L * 44_100 * 2 * 3);

    [Fact]
    [Trait("Category", "Timing")]
    public async Task FiveMinuteStereoTake_StreamsThroughTheUploaderInUnderFifteenSeconds()
    {
        using var sink = new CountingHandler();
        var api = new StemStudioApiClient(new HttpClient(sink) { BaseAddress = new Uri("http://api.test") });
        var engine = Substitute.For<IRecordingEngine>();
        var reference = Substitute.For<IJSStreamReference>();
        reference.OpenReadStreamAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream(new byte[FiveMinutesStereo]));
        engine.CaptureWavAsync("c1").Returns(reference);
        using var uploader = new TakeUploader(engine, api);
        var item = TakeItem.Local(RecordingData.Capture("c1"), RecordingData.Upload());

        var watch = Stopwatch.StartNew();
        await uploader.UploadAsync(RecordingData.TrackId, [item]);
        watch.Stop();

        item.Status.ShouldBe("saving");
        sink.BodyBytes.ShouldBeGreaterThan(FiveMinutesStereo);
        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
        TestContext.Current.SendDiagnosticMessage($"S-02 .NET half: {FiveMinutesStereo:N0} bytes in {watch.ElapsedMilliseconds} ms");
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public long BodyBytes { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await using var body = await request.Content!.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[81_920];
            int read;
            while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
            {
                BodyBytes += read;
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(RecordingData.Body(RecordingData.Take(status: TakeStatusDto.Saving)), System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
