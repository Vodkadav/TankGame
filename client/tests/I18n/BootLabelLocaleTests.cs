using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

namespace TankGame.Tests.I18n;

// Verifies the CSV→.translation pipeline resolves m0.boot_label in every shipped
// locale. If a locale's translation failed to load, Translate echoes the key back
// instead of the value, so asserting the value proves the resource is wired.
public class BootLabelLocaleTests : TestClass
{
    public BootLabelLocaleTests(Node testScene) : base(testScene) { }

    [Test]
    public void English_resolves() => AssertLocale("en");

    [Test]
    public void Spanish_resolves() => AssertLocale("es");

    [Test]
    public void Danish_resolves() => AssertLocale("da");

    private static void AssertLocale(string locale)
    {
        var previous = TranslationServer.GetLocale();
        TranslationServer.SetLocale(locale);
        try
        {
            TranslationServer.Translate("m0.boot_label").ToString().ShouldBe("TankGame M0");
        }
        finally
        {
            TranslationServer.SetLocale(previous);
        }
    }
}
