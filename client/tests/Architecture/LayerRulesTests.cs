using System;
using System.Linq;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace TankGame.Tests.Architecture;

// Enforces the five-layer dependency direction recorded in ADR-0001.
// Domain ← GameLogic ← Infrastructure, Domain ← Data ← Infrastructure,
// Presentation depends only on GameLogic + Domain.
public class LayerRulesTests
{
    private static readonly System.Reflection.Assembly Client =
        typeof(global::TankGame.Presentation.MainScene).Assembly;

    private const string Domain = "TankGame.Domain";
    private const string GameLogic = "TankGame.GameLogic";
    private const string Data = "TankGame.Data";
    private const string Infrastructure = "TankGame.Infrastructure";
    private const string Presentation = "TankGame.Presentation";

    [Fact]
    public void Domain_depends_on_no_other_layer() =>
        Types.InAssembly(Client).That().ResideInNamespace(Domain)
            .ShouldNot().HaveDependencyOnAny(GameLogic, Data, Infrastructure, Presentation)
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void GameLogic_depends_only_on_Domain() =>
        Types.InAssembly(Client).That().ResideInNamespace(GameLogic)
            .ShouldNot().HaveDependencyOnAny(Data, Infrastructure, Presentation)
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void Data_depends_only_on_Domain() =>
        Types.InAssembly(Client).That().ResideInNamespace(Data)
            .ShouldNot().HaveDependencyOnAny(GameLogic, Infrastructure, Presentation)
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void Infrastructure_does_not_depend_on_Presentation() =>
        Types.InAssembly(Client).That().ResideInNamespace(Infrastructure)
            .ShouldNot().HaveDependencyOn(Presentation)
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void Presentation_does_not_depend_on_Data_or_Infrastructure() =>
        Types.InAssembly(Client).That().ResideInNamespace(Presentation)
            .ShouldNot().HaveDependencyOnAny(Data, Infrastructure)
            .GetResult().IsSuccessful.ShouldBeTrue();

    // R4 coverage guard: every TankGame type must be classified into a known layer,
    // so a stray namespace fails the build the moment it is introduced.
    [Fact]
    public void Every_TankGame_type_lives_in_a_known_layer()
    {
        string[] allowed = { Domain, GameLogic, Data, Infrastructure, Presentation, "TankGame.Tests" };

        var stray = Types.InAssembly(Client).GetTypes()
            .Where(t => t.Namespace is not null
                        && t.Namespace.StartsWith("TankGame", StringComparison.Ordinal))
            .Where(t => !allowed.Any(a => t.Namespace!.StartsWith(a, StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .ToArray();

        stray.ShouldBeEmpty();
    }
}
