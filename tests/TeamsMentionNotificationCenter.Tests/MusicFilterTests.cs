using TeamsMentionNotificationCenter.Audio;

namespace TeamsMentionNotificationCenter.Tests;

public class MusicFilterTests
{
    private const string SpotifyId = "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";
    private const string ChromeId = "Chrome";

    [Fact]
    public void Empty_Filter_Matches_Everything()
    {
        Assert.True(AudioController.SourceMatchesFilter(SpotifyId, new List<string>()));
        Assert.True(AudioController.SourceMatchesFilter(ChromeId, new List<string>()));
    }

    [Fact]
    public void Filter_Matches_Substring_Case_Insensitive()
    {
        var filter = new List<string> { "spotify" };
        Assert.True(AudioController.SourceMatchesFilter(SpotifyId, filter));
        Assert.False(AudioController.SourceMatchesFilter(ChromeId, filter));
    }

    [Fact]
    public void Multi_Filter_Matches_Any_Entry()
    {
        var filter = new List<string> { "spotify", "chrome" };
        Assert.True(AudioController.SourceMatchesFilter(SpotifyId, filter));
        Assert.True(AudioController.SourceMatchesFilter(ChromeId, filter));
        Assert.False(AudioController.SourceMatchesFilter("MSEdge", filter));
    }

    [Fact]
    public void Blank_Entries_Are_Ignored()
    {
        var filter = new List<string> { " ", "" };
        // Nur Leereinträge => wie leerer Filter? Nein: es gibt Einträge, aber keiner passt.
        Assert.False(AudioController.SourceMatchesFilter(ChromeId, filter));
    }
}
