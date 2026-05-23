using System.Linq;
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace TankGame.Tests.Architecture;

public class LayerRulesTests
{
    private const string DomainNs = "TankGame.Domain";
    private const string GameLogicNs = "TankGame.GameLogic";
    private const string DataNs = "TankGame.Data";
    private const string InfrastructureNs = "TankGame.Infrastructure";
    private const string PresentationNs = "TankGame.Presentation";

    private static readonly Assembly ProductionAssembly =
        typeof(TankGame.Presentation.MainScene).Assembly;

    [Fact]
    public void Domain_DoesNotDependOnAnyOtherLayerOrGodot()
    {
        var result = Types.InAssembly(ProductionAssembly)
            .That()
            .ResideInNamespaceStartingWith(DomainNs)
            .ShouldNot()
            .HaveDependencyOnAny(GameLogicNs, DataNs, InfrastructureNs, PresentationNs, "Godot")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            FormatFailure("Domain", result.FailingTypeNames));
    }

    [Fact]
    public void GameLogic_DependsOnlyOnDomain()
    {
        var result = Types.InAssembly(ProductionAssembly)
            .That()
            .ResideInNamespaceStartingWith(GameLogicNs)
            .ShouldNot()
            .HaveDependencyOnAny(DataNs, InfrastructureNs, PresentationNs, "Godot")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            FormatFailure("GameLogic", result.FailingTypeNames));
    }

    [Fact]
    public void Data_DependsOnlyOnDomain()
    {
        var result = Types.InAssembly(ProductionAssembly)
            .That()
            .ResideInNamespaceStartingWith(DataNs)
            .ShouldNot()
            .HaveDependencyOnAny(GameLogicNs, InfrastructureNs, PresentationNs)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            FormatFailure("Data", result.FailingTypeNames));
    }

    [Fact]
    public void Infrastructure_DependsOnlyOnDomainGameLogicData()
    {
        var result = Types.InAssembly(ProductionAssembly)
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNs)
            .ShouldNot()
            .HaveDependencyOn(PresentationNs)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            FormatFailure("Infrastructure", result.FailingTypeNames));
    }

    [Fact]
    public void Presentation_DependsOnlyOnGameLogicAndDomain()
    {
        var result = Types.InAssembly(ProductionAssembly)
            .That()
            .ResideInNamespaceStartingWith(PresentationNs)
            .ShouldNot()
            .HaveDependencyOnAny(DataNs, InfrastructureNs)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            FormatFailure("Presentation", result.FailingTypeNames));
    }

    [Fact]
    public void EveryProductionType_LivesInOneOfTheFiveKnownNamespaces()
    {
        // GodotPlugins.* is SDK-injected entry-point glue emitted by
        // Godot.NET.Sdk, not application code we authored — excluded from
        // coverage. Every type we write must still live in one of the 5
        // known layer namespaces (R4 in development-plan.md §7).
        var untagged = ProductionAssembly.GetTypes()
            .Where(t => !t.IsNested)
            .Where(t => !IsCompilerGenerated(t))
            .Where(t => !IsGodotSdkInjected(t.Namespace))
            .Where(t => !LivesInKnownLayer(t.Namespace))
            .Select(t => $"{t.FullName} (namespace: {t.Namespace ?? "<null>"})")
            .OrderBy(s => s)
            .ToArray();

        Assert.True(
            untagged.Length == 0,
            "The following production types are not in one of the 5 known layer "
            + "namespaces (Domain / GameLogic / Data / Infrastructure / Presentation):\n  - "
            + string.Join("\n  - ", untagged));
    }

    private static bool LivesInKnownLayer(string? ns)
    {
        if (string.IsNullOrEmpty(ns))
        {
            return false;
        }

        return ns.StartsWith(DomainNs, System.StringComparison.Ordinal)
            || ns.StartsWith(GameLogicNs, System.StringComparison.Ordinal)
            || ns.StartsWith(DataNs, System.StringComparison.Ordinal)
            || ns.StartsWith(InfrastructureNs, System.StringComparison.Ordinal)
            || ns.StartsWith(PresentationNs, System.StringComparison.Ordinal);
    }

    private static bool IsCompilerGenerated(System.Type t)
    {
        return t.GetCustomAttributes(
            typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute),
            inherit: false).Length > 0;
    }

    private static bool IsGodotSdkInjected(string? ns)
    {
        return ns is not null
            && ns.StartsWith("GodotPlugins", System.StringComparison.Ordinal);
    }

    private static string FormatFailure(string layer, System.Collections.Generic.IEnumerable<string>? failing)
    {
        var list = failing?.ToArray() ?? System.Array.Empty<string>();
        if (list.Length == 0)
        {
            return $"{layer} layer rule failed (no specific failing types reported).";
        }

        return $"{layer} layer rule violated by:\n  - " + string.Join("\n  - ", list);
    }
}
