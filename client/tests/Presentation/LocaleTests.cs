using Godot;
using Chickensoft.GoDotTest;
using TankGame.Infrastructure;

namespace TankGame.Tests.Presentation;

public class LocaleTests : TestClass
{
    private string _originalLocale = default!;

    public LocaleTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();
    }

    [Cleanup]
    public void Cleanup()
    {
        TranslationServer.SetLocale(_originalLocale);
    }

    [Test]
    public void BootLabel_English_ReturnsTankGameM0()
    {
        AssertLocale("en", "TankGame M0");
    }

    [Test]
    public void BootLabel_Spanish_ReturnsTankGameM0()
    {
        AssertLocale("es", "TankGame M0");
    }

    [Test]
    public void BootLabel_Danish_ReturnsTankGameM0()
    {
        AssertLocale("dk", "TankGame M0");
    }

    private static void AssertLocale(string locale, string expected)
    {
        TranslationServer.SetLocale(locale);
        var actual = TranslationServer.Translate("m0.boot_label");

        if (actual != expected)
        {
            throw new System.Exception(
                $"Expected locale '{locale}' key 'm0.boot_label' to return " +
                $"'{expected}', but got '{actual}'.");
        }
    }
}
