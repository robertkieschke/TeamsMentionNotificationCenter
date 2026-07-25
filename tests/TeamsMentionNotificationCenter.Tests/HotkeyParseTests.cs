using TeamsMentionNotificationCenter.Input;

namespace TeamsMentionNotificationCenter.Tests;

public class HotkeyParseTests
{
    [Theory]
    [InlineData("Ctrl+Alt+T")]
    [InlineData("Shift+F9")]
    [InlineData("Strg+Alt+Q")]     // deutscher Alias
    [InlineData("Ctrl+A")]         // Buchstabe MIT Modifier ist erlaubt
    public void Parses_Valid_Combos(string combo) =>
        Assert.True(HotkeyManager.TryParse(combo, out _, out var vk) && vk != 0);

    [Theory]
    [InlineData("MediaPlayPause", 0xB3)]
    [InlineData("PlayPause", 0xB3)]        // Alias
    [InlineData("play/pause", 0xB3)]       // Alias
    [InlineData("NextTrack", 0xB0)]
    [InlineData("PrevTrack", 0xB1)]
    [InlineData("MediaStop", 0xB2)]
    [InlineData("Mute", 0xAD)]
    [InlineData("F13", 0x7C)]
    public void Media_And_Function_Keys_Work_Without_Modifiers(string combo, int expectedVk)
    {
        Assert.True(HotkeyManager.TryParse(combo, out var mods, out var vk));
        Assert.Equal(0u, mods);
        Assert.Equal((uint)expectedVk, vk);
    }

    [Theory]
    [InlineData("A")]        // einzelner Buchstabe würde systemweit jedes A schlucken
    [InlineData("1")]
    [InlineData("Space")]
    [InlineData("Enter")]
    public void Plain_Typing_Keys_Require_Modifiers(string combo) =>
        Assert.False(HotkeyManager.TryParse(combo, out _, out _));

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Quatsch")]
    public void Invalid_Input_Is_Rejected(string combo) =>
        Assert.False(HotkeyManager.TryParse(combo, out _, out _));
}
