using TeamsMentionNotificationCenter.Localization;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Tests;

public class LanguageDetectionTests
{
    [Theory]
    [InlineData("de", AppLanguage.De)]
    [InlineData("DE", AppLanguage.De)]
    [InlineData("it", AppLanguage.It)]
    [InlineData("en", AppLanguage.En)]
    [InlineData("fr", AppLanguage.En)] // nicht unterstützt -> Englisch
    [InlineData("es", AppLanguage.En)]
    [InlineData("ja", AppLanguage.En)]
    [InlineData("", AppLanguage.En)]
    public void Maps_System_Language_With_English_Fallback(string iso, AppLanguage expected) =>
        Assert.Equal(expected, AppSettings.LanguageForCulture(iso));
}
