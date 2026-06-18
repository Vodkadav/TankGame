using Godot;
using Chickensoft.GoDotTest;
using TankGame.Infrastructure;

namespace TankGame.Tests.Presentation;

// Forces each shipped locale and asserts the settings-panel strings resolve — the i18n half of the
// settings overlay (#226). Guards against the regression where the panel and the title's Settings
// button rendered raw keys ("title.settings", "settings.heading") because no row existed in strings.csv.
public class SettingsLocaleTests : TestClass
{
    private string _originalLocale = default!;

    public SettingsLocaleTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();
    }

    [Cleanup]
    public void Cleanup() => TranslationServer.SetLocale(_originalLocale);

    [Test]
    public void English_RendersTheSettingsStrings()
        => AssertAll("en", "Settings", "Settings", "SFX Volume", "Brightness",
            "Show Friendly Names", "Show Enemy Names", "Close", "Mute");

    [Test]
    public void Spanish_RendersTheSettingsStrings()
        => AssertAll("es", "Ajustes", "Ajustes", "Volumen de efectos", "Brillo",
            "Mostrar nombres aliados", "Mostrar nombres enemigos", "Cerrar", "Silencio");

    [Test]
    public void Danish_RendersTheSettingsStrings()
        => AssertAll("dk", "Indstillinger", "Indstillinger", "Lydeffekter", "Lysstyrke",
            "Vis venners navne", "Vis fjenders navne", "Luk", "Lyd fra");

    private static void AssertAll(string locale, string titleBtn, string heading, string sfxVol,
        string brightness, string friendly, string enemy, string close, string mute)
    {
        TranslationServer.SetLocale(locale);
        AssertKey(locale, "title.settings", titleBtn);
        AssertKey(locale, "settings.heading", heading);
        AssertKey(locale, "settings.sfx_volume", sfxVol);
        AssertKey(locale, "settings.brightness", brightness);
        AssertKey(locale, "settings.show_friendly_names", friendly);
        AssertKey(locale, "settings.show_enemy_names", enemy);
        AssertKey(locale, "settings.close", close);
        AssertKey(locale, "settings.mute", mute);
    }

    private static void AssertKey(string locale, string key, string expected)
    {
        var actual = TranslationServer.Translate(key).ToString();
        if (actual != expected)
        {
            throw new System.Exception($"Key '{key}' in locale '{locale}' rendered '{actual}', expected '{expected}'.");
        }
    }
}
