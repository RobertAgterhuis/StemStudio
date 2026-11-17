using System.Text;
using Microsoft.AspNetCore.Http;
using StemStudio.Domain;

namespace StemStudio.Api.Tests;

/// <summary>The helpers shared by several API folders (SIG duplication fix of round 022/023).</summary>
public sealed class SharedHelpersTests
{
    [Fact]
    public void Require_AddsTheFieldErrorOnlyWhenTheRuleFails()
    {
        var errors = new List<DomainError>();

        QueryParsing.Require(errors, valid: true, "limit", "ignored");
        QueryParsing.Require(errors, valid: false, "offset", "Must be 0 or more.");

        errors.ShouldBe([new DomainError("offset", "Must be 0 or more.")]);
    }

    [Fact]
    public void Require_RefusesAMissingList() =>
        Should.Throw<ArgumentNullException>(() => QueryParsing.Require(null!, valid: false, "q", "x"));

    [Theory]
    [InlineData("?after=7", "", true, 7)]
    [InlineData("?after=7", "3", true, 7)]
    [InlineData("", "3", true, 3)]
    [InlineData("", "", true, 0)]
    [InlineData("?after=-1", "", false, 0)]
    [InlineData("?after=x", "", false, 0)]
    [InlineData("", "+4", false, 0)]
    public void StreamPosition_ReadsAfterBeforeLastEventId(string query, string lastEventId, bool valid, int after)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        if (lastEventId.Length > 0)
        {
            context.Request.Headers["Last-Event-ID"] = lastEventId;
        }

        StreamPosition.TryParse(context.Request, out var position).ShouldBe(valid);
        position.ShouldBe(after);
    }

    [Fact]
    public async Task SseText_WritesUtf8AndFlushes()
    {
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        await SseText.WriteAsync(context.Response, SseText.KeepAlive, TestContext.Current.CancellationToken);
        await SseText.WriteAsync(context.Response, "data: ü\n\n", TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(body.ToArray()).ShouldBe(": keep-alive\n\ndata: ü\n\n");
    }
}
