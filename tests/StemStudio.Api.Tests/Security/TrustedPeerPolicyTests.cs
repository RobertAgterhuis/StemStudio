using System.Net;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Security;

namespace StemStudio.Api.Tests.Security;

/// <summary>Refactor 003 step 3: the shared trusted-peer rule of the cross-site guard and the key-entry filter.</summary>
public sealed class TrustedPeerPolicyTests
{
    private static readonly IPAddress WebContainer = IPAddress.Parse("172.18.0.5");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.1.2.3", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("172.18.0.5", true)]
    [InlineData("::ffff:172.18.0.5", true)]
    [InlineData("172.18.0.1", false)]
    [InlineData("192.168.1.20", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("::", false)]
    public async Task Peer_IsTrustedOnlyWhenLoopbackOrTheInternalCaller(string peer, bool expected)
    {
        var resolver = new CountingResolver([WebContainer]);

        (await Policy("web", resolver).IsTrustedAsync(IPAddress.Parse(peer), Ct)).ShouldBe(expected);
    }

    [Fact]
    public async Task NoPeer_IsNeverTrusted()
    {
        (await Policy("web", new CountingResolver([WebContainer])).IsTrustedAsync(null, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Loopback_NeedsNoLookup_AndWithoutAnInternalCallerNothingIsResolved()
    {
        var resolver = new CountingResolver([WebContainer]);

        (await Policy("web", resolver).IsTrustedAsync(IPAddress.Loopback, Ct)).ShouldBeTrue();
        (await Policy(null, resolver).IsTrustedAsync(WebContainer, Ct)).ShouldBeFalse();

        resolver.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task InternalCaller_IsResolvedOnEveryCall_SoARecreatedContainerIsRecognised()
    {
        var resolver = new CountingResolver([WebContainer]);
        var policy = Policy("web", resolver);
        (await policy.IsTrustedAsync(WebContainer, Ct)).ShouldBeTrue();

        resolver.Addresses = [IPAddress.Parse("172.18.0.9")];

        (await policy.IsTrustedAsync(WebContainer, Ct)).ShouldBeFalse();
        resolver.Calls.ShouldBe(2);
    }

    private static TrustedPeerPolicy Policy(string? caller, IHostAddressResolver resolver) =>
        new(new KeyEntryPolicy(caller, "http://localhost:8080"), resolver);

    private sealed class CountingResolver(IPAddress[] addresses) : IHostAddressResolver
    {
        public IPAddress[] Addresses { get; set; } = addresses;

        public int Calls { get; private set; }

        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Addresses);
        }
    }
}
