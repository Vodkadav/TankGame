using TankGame.Infrastructure;
using Xunit;

namespace TankGame.Tests.Infrastructure;

public class WebTextEntryTests
{
    // The native browser prompt is only for the touch web export — Godot's canvas LineEdit cannot
    // raise the iOS Safari soft keyboard, so typing there is impossible. Desktop web (hardware
    // keyboard) and the desktop/Android builds keep the in-game prompt panel.
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void NativePrompt_IsOnlyForTheTouchWebExport(bool isWeb, bool hasTouch, bool expected)
    {
        Assert.Equal(expected, WebTextEntry.NeedsNativePrompt(isWeb, hasTouch));
    }

    [Fact]
    public void PromptScript_EmbedsTheMessageAndTheCurrentValue()
    {
        var script = WebTextEntry.PromptScript("Choose your battle name", "Player");

        Assert.Equal("window.prompt('Choose your battle name', 'Player')", script);
    }

    [Fact]
    public void PromptScript_EscapesQuotesBackslashesAndNewlines_SoNoValueCanBreakOutOfTheScript()
    {
        var script = WebTextEntry.PromptScript("It's time", "a\\b'c\r\nd");

        Assert.Equal(@"window.prompt('It\'s time', 'a\\b\'c\nd')", script);
    }
}
