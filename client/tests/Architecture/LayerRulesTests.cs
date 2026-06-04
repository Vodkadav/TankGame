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
        typeof(TankGame.Presentation.Bootstrap).Assembly;

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
    public void Presentation_DoesNotDependOnData()
    {
        // Presentation (Godot scene scripts) is the composition root: it may wire
        // Infrastructure implementations (TranslationLoader, input sources, the
        // network transport) into the scene tree. It must still NOT reach the raw
        // Data layer directly — persistence is consumed via GameLogic. See ADR-0001.
        var result = Types.InAssembly(ProductionAssembly)
            .That()
            .ResideInNamespaceStartingWith(PresentationNs)
            .ShouldNot()
            .HaveDependencyOnAny(DataNs)
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
            .Where(t => !IsTestType(t.Namespace))
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
        // Most synthesized types carry [CompilerGenerated]; a few Roslyn-lowered helpers do not
        // (e.g. the `<>y__InlineArray6` buffer emitted for a params-ReadOnlySpan call, or
        // `<PrivateImplementationDetails>`). They all share an unspeakable name starting with '<',
        // which is the reliable marker.
        if (t.Name.StartsWith("<", System.StringComparison.Ordinal))
        {
            return true;
        }

        return t.GetCustomAttributes(
            typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute),
            inherit: false).Length > 0;
    }

    private static bool IsGodotSdkInjected(string? ns)
    {
        return ns is not null
            && ns.StartsWith("GodotPlugins", System.StringComparison.Ordinal);
    }

    // GoDotTest scene tests (TankGame.Tests.*) are compiled into the game assembly
    // for debug/editor builds so GoDotTest can reflect over them at runtime (see
    // Bootstrap.cs and ADR-0001). They are test code, not production types, and are
    // excluded from ExportRelease — so they are exempt from the layer taxonomy.
    private static bool IsTestType(string? ns)
    {
        return ns is not null
            && ns.StartsWith("TankGame.Tests", System.StringComparison.Ordinal);
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
