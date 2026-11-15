using System.Reflection;
using Shouldly;
using Xunit;

namespace Uic.Claims.Ai.UnitTests;

/// <summary>
/// Keeps the layering honest over time. These are cheap and they catch the class of
/// mistake that is expensive later: someone opening an HttpClient straight from the
/// domain, or a controller reaching past the core into Oracle.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly CoreAssembly = typeof(Core.Claims.ClaimContext).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Core_does_not_reference_the_infrastructure_assembly()
    {
        var referenced = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name);

        referenced.ShouldNotContain(InfrastructureAssembly.GetName().Name);
    }

    [Fact]
    public void Core_has_no_dependency_on_a_vendor_sdk()
    {
        var forbidden = new[] { "OpenAI", "Azure.AI", "Anthropic", "Oracle.ManagedDataAccess", "Dapper" };
        var referenced = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();

        foreach (var name in forbidden)
        {
            referenced.ShouldNotContain(
                r => r.StartsWith(name, StringComparison.OrdinalIgnoreCase),
                $"Core must stay provider-agnostic, found reference to {name}");
        }
    }

    [Fact]
    public void Only_the_infrastructure_layer_knows_about_the_provider()
    {
        var coreTypes = CoreAssembly.GetTypes()
            .Where(t => t.IsPublic)
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(m => m.ToString() ?? "")
            .ToArray();

        coreTypes.ShouldNotContain(t => t.Contains("HttpClient", StringComparison.Ordinal));
    }
}
