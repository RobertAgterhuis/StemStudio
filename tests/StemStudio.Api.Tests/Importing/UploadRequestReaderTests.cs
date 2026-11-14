using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using StemStudio.Api.Importing;

namespace StemStudio.Api.Tests.Importing;

public sealed class UploadRequestReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task KestrelBodyLimit_Returns413()
    {
        var request = Request("multipart/form-data; boundary=abc", new ThrowingStream(new BadHttpRequestException("Request body too large.", 413)));

        var result = await UploadRequestReader.ReadAsync(request, (_, _) => throw new InvalidOperationException(), Ct);

        result.ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(413);
    }

    [Fact]
    public async Task OtherBadRequest_Returns400OnFile()
    {
        var request = Request("multipart/form-data; boundary=abc", new ThrowingStream(new BadHttpRequestException("Unexpected end of request content.", 400)));

        var result = await UploadRequestReader.ReadAsync(request, (_, _) => throw new InvalidOperationException(), Ct);

        result.ShouldBeOfType<ValidationProblem>().ProblemDetails.Errors.ShouldContainKey("file");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("multipart/form-data")]
    [InlineData("text/plain")]
    public async Task MissingOrForeignContentType_Returns400OnFile(string? contentType)
    {
        var result = await UploadRequestReader.ReadAsync(Request(contentType, new MemoryStream()), (_, _) => throw new InvalidOperationException(), Ct);

        result.ShouldBeOfType<ValidationProblem>().ProblemDetails.Errors["file"].Single().ShouldContain("multipart/form-data");
    }

    [Fact]
    public async Task FileNameStar_IsUsed()
    {
        const string body = "--abc\r\nContent-Disposition: form-data; name=\"file\"; filename*=UTF-8''Lied%20%C3%A9.mp3\r\n\r\nxyz\r\n--abc--\r\n";
        var request = Request("multipart/form-data; boundary=abc", new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body)));
        string? seen = null;

        await UploadRequestReader.ReadAsync(request, (_, name) => { seen = name; return Task.FromResult(Results.Ok()); }, Ct);

        seen.ShouldBe("Lied é.mp3");
    }

    private static HttpRequest Request(string? contentType, Stream body)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.Body = body;
        return context.Request;
    }

    private sealed class ThrowingStream(Exception error) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw error;

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw error;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw error;
    }
}
