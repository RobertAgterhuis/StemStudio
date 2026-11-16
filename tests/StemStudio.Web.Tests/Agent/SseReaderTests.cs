using System.Text;
using StemStudio.Web.Agent;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Agent;

/// <summary>The text/event-stream parser (DELTA.md section 3, WHATWG event-stream format).</summary>
public sealed class SseReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordedStream_YieldsEveryEventWithIdTypeAndData_AndSkipsComments()
    {
        var messages = await ReadAsync(SseHttpHandler.Fixture("simple-run.sse"));

        messages.Select(m => m.Id).ShouldBe(["1", "2", "3", "4", "5", "6", "7"]);
        messages.Select(m => m.Event).ShouldBe(["run.started", "plan.updated", "step.created", "step.updated", "text.delta", "text.delta", "run.completed"]);
        messages[4].Data.ShouldBe("""{"text":"I found "}""");
    }

    [Fact]
    public async Task MultiLineData_IsJoinedWithNewLines_AndCrLfIsAccepted()
    {
        var messages = await ReadAsync("id: 9\r\nevent: text.delta\r\ndata: first\r\ndata:second\r\n\r\n");

        var message = messages.ShouldHaveSingleItem();
        (message.Id, message.Event, message.Data).ShouldBe(("9", "text.delta", "first\nsecond"));
    }

    [Fact]
    public async Task EventWithoutType_IsAMessage_AndEventWithoutDataIsIgnored()
    {
        var messages = await ReadAsync("data: x\n\nevent: ping\n\nid: 4\nretry: 100\nunknown\n\n");

        var message = messages.ShouldHaveSingleItem();
        (message.Id, message.Event, message.Data).ShouldBe((null, "message", "x"));
    }

    [Fact]
    public async Task LastEventWithoutBlankLine_IsDiscarded()
    {
        var messages = await ReadAsync("id: 1\nevent: a\ndata: 1\n\nid: 2\nevent: b\ndata: 2");

        messages.ShouldHaveSingleItem().Id.ShouldBe("1");
    }

    [Fact]
    public async Task IdIsKeptForLaterEventsWithoutAnId()
    {
        var messages = await ReadAsync("id: 5\ndata: a\n\ndata: b\n\n");

        messages.Select(m => m.Id).ShouldBe(["5", "5"]);
    }

    private static async Task<List<SseMessage>> ReadAsync(string text)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var messages = new List<SseMessage>();
        await foreach (var message in SseReader.ReadAsync(stream, Ct))
        {
            messages.Add(message);
        }

        return messages;
    }
}
