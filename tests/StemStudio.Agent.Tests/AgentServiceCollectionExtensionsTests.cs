using Microsoft.Extensions.DependencyInjection;

namespace StemStudio.Agent.Tests;

/// <summary>Skeleton test (architect, 011 Phase 0 T004): the registration entry point exists and validates its argument.</summary>
public sealed class AgentServiceCollectionExtensionsTests
{
    [Fact]
    public void AddStemStudioAgent_ReturnsTheSameCollection()
    {
        var services = new ServiceCollection();

        services.AddStemStudioAgent().ShouldBeSameAs(services);
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddStemStudioAgent());
    }
}
