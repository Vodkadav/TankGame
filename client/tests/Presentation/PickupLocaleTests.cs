using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain;
using TankGame.Infrastructure;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class PickupLocaleTests : TestClass
{
    private string _originalLocale = default!;

    public PickupLocaleTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();
    }

    [Cleanup]
    public void Cleanup() => TranslationServer.SetLocale(_originalLocale);

    [Test]
    public void EveryPickupName_IsTranslated_InEveryLocale()
    {
        foreach (var locale in new[] { "en", "es", "dk" })
        {
            TranslationServer.SetLocale(locale);
            foreach (PowerupKind kind in System.Enum.GetValues(typeof(PowerupKind)))
            {
                var key = PickupFloater.LabelKeyFor(kind);
                var text = TranslationServer.Translate(key).ToString();
                if (string.IsNullOrEmpty(text) || text == key)
                {
                    throw new System.Exception(
                        $"Pickup key '{key}' has no '{locale}' translation (got '{text}').");
                }
            }
        }
    }
}
