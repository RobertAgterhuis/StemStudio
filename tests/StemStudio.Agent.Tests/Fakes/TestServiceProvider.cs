using System.Collections;
using Microsoft.Extensions.DependencyInjection;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>
/// A minimal singleton container over <see cref="ServiceDescriptor"/>s (the test project references only the DI abstractions):
/// the last registration of a type wins, <c>IEnumerable&lt;T&gt;</c> returns all, every descriptor is created once.
/// </summary>
public sealed class TestServiceProvider(IServiceCollection services) : IServiceProvider, IDisposable
{
    private readonly List<ServiceDescriptor> _descriptors = [.. services];
    private readonly Dictionary<ServiceDescriptor, object> _instances = [];
    private readonly Lock _gate = new();

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(IServiceProvider))
        {
            return this;
        }

        if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return All(serviceType.GetGenericArguments()[0]);
        }

        var descriptor = _descriptors.LastOrDefault(item => item.ServiceType == serviceType);
        return descriptor is null ? null : Instance(descriptor);
    }

    /// <summary>Resolves every registered service once (like ValidateOnBuild).</summary>
    public void ResolveAll()
    {
        foreach (var type in _descriptors.Select(item => item.ServiceType).Distinct())
        {
            GetService(type).ShouldNotBeNull(type.Name);
        }
    }

    public void Dispose()
    {
        foreach (var disposable in _instances.Values.OfType<IDisposable>().Where(item => !ReferenceEquals(item, this)))
        {
            disposable.Dispose();
        }
    }

    private IList All(Type itemType)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
        foreach (var descriptor in _descriptors.Where(item => item.ServiceType == itemType))
        {
            list.Add(Instance(descriptor));
        }

        return list;
    }

    private object Instance(ServiceDescriptor descriptor)
    {
        lock (_gate)
        {
            if (!_instances.TryGetValue(descriptor, out var instance))
            {
                instance = descriptor.ImplementationInstance
                    ?? descriptor.ImplementationFactory?.Invoke(this)
                    ?? ActivatorUtilities.CreateInstance(this, descriptor.ImplementationType!);
                _instances[descriptor] = instance;
            }

            return instance;
        }
    }
}
