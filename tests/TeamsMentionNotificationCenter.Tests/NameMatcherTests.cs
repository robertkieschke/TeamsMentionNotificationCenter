using TeamsMentionNotificationCenter.Detection;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Tests;

public class NameMatcherTests
{
    private static AppSettings Settings(
        IEnumerable<string> triggers,
        bool fuzzy = true,
        int fuzzyDistance = 1,
        bool ignoreOwn = false,
        string ownName = "",
        int cooldownMs = 0) => new()
    {
        TriggerWords = triggers.ToList(),
        FuzzyEnabled = fuzzy,
        FuzzyMaxDistance = fuzzyDistance,
        IgnoreOwnSpeaker = ignoreOwn,
        OwnSpeakerName = ownName,
        TriggerCooldownMs = cooldownMs
    };

    private static NameMatcher Matcher(params string[] triggers) =>
        new(Settings(triggers));

    [Theory]
    [InlineData("Robert, kannst du kurz?")]
    [InlineData("hey ROBERT")]
    [InlineData("also robert meinte")]
    [InlineData("das war Kieschke")]
    public void Matches_TriggerWordInSentence(string text)
    {
        var m = Matcher("Robert", "Kieschke");
        Assert.True(m.ContainsTrigger(text, out _));
    }

    [Theory]
    [InlineData("hallo zusammen")]
    [InlineData("wir sehen uns morgen")]
    [InlineData("")]
    public void NoMatch_WhenTriggerAbsent(string text)
    {
        var m = Matcher("Robert", "Kieschke");
        Assert.False(m.ContainsTrigger(text, out _));
    }

    [Fact]
    public void Matches_IgnoringDiacritics()
    {
        var m = Matcher("Jörg", "Müller");
        Assert.True(m.ContainsTrigger("kann jorg das machen", out _));   // ohne Umlaut
        Assert.True(m.ContainsTrigger("frag mal müller", out _));        // mit Umlaut
    }

    [Fact]
    public void Matches_PrefixForLongerToken()
    {
        var m = Matcher("Robert");
        Assert.True(m.ContainsTrigger("das ist roberts idee", out _)); // "roberts" beginnt mit "robert"
    }

    [Fact]
    public void Fuzzy_MatchesSingleTypo_WhenEnabled()
    {
        var m = Matcher("Robert");
        Assert.True(m.ContainsTrigger("robrt bist du da", out var w)); // 1 Tippfehler
        Assert.Equal("robert", w);
    }

    [Fact]
    public void Fuzzy_NoMatch_WhenDisabled()
    {
        var m = new NameMatcher(Settings(new[] { "Robert" }, fuzzy: false));
        Assert.False(m.ContainsTrigger("robrt bist du da", out _));
    }

    [Fact]
    public void ShortTrigger_DoesNotSubstringMatch()
    {
        var m = Matcher("Tim"); // <4 Zeichen: nur exakter Token-Treffer
        Assert.False(m.ContainsTrigger("this is an intimate setting", out _));
        Assert.True(m.ContainsTrigger("tim kommt gleich", out _));
    }

    [Fact]
    public void IgnoreOwnSpeaker_SkipsOwnSpeech()
    {
        var m = new NameMatcher(Settings(new[] { "Robert" }, ignoreOwn: true, ownName: "Robert Kieschke"));
        Assert.False(m.TryMatch("Robert Kieschke", "ja hier robert", out _)); // man selbst
        Assert.True(m.TryMatch("Nicole Wichmann", "robert kannst du", out _)); // jemand anderes
    }

    [Fact]
    public void IgnoreOwnSpeaker_Off_MatchesEvenOwnSpeech()
    {
        var m = new NameMatcher(Settings(new[] { "Robert" }, ignoreOwn: false, ownName: "Robert Kieschke"));
        Assert.True(m.TryMatch("Robert Kieschke", "hier robert", out _));
    }

    [Fact]
    public void Cooldown_SuppressesRapidSecondMatch()
    {
        var m = new NameMatcher(Settings(new[] { "Robert" }, cooldownMs: 10000));
        Assert.True(m.TryMatch("Nicole", "robert eins", out _));
        Assert.False(m.TryMatch("Nicole", "robert zwei", out _)); // innerhalb Cooldown
    }

    [Fact]
    public void Cooldown_Zero_AllowsConsecutiveMatches()
    {
        var m = new NameMatcher(Settings(new[] { "Robert" }, cooldownMs: 0));
        Assert.True(m.TryMatch("Nicole", "robert eins", out _));
        Assert.True(m.TryMatch("Nicole", "robert zwei", out _));
    }

    [Fact]
    public void NoTriggers_NeverMatches()
    {
        var m = Matcher(); // leere Liste
        Assert.False(m.TryMatch("Nicole", "robert kieschke jörg müller", out _));
    }
}
