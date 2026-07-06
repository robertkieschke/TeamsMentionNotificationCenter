using TeamsMentionNotificationCenter.Core;
using TeamsMentionNotificationCenter.Localization;

namespace TeamsMentionNotificationCenter.Tests;

public class ReleaseNotesLanguageTests
{
    private const string Trilingual = """
        ## Deutsch
        - Neues Feature A
        - Fehler B behoben

        ## English
        - New feature A
        - Fixed bug B

        ## Italiano
        - Nuova funzione A
        - Corretto bug B
        """;

    [Fact]
    public void Extracts_German_Section()
    {
        var s = UpdateManager.ExtractLanguageSection(Trilingual, AppLanguage.De);
        Assert.Contains("Neues Feature A", s);
        Assert.DoesNotContain("New feature A", s);
        Assert.DoesNotContain("Nuova funzione A", s);
        Assert.DoesNotContain("Deutsch", s); // Überschrift selbst nicht anzeigen
    }

    [Fact]
    public void Extracts_English_Section()
    {
        var s = UpdateManager.ExtractLanguageSection(Trilingual, AppLanguage.En);
        Assert.Contains("New feature A", s);
        Assert.DoesNotContain("Neues Feature A", s);
    }

    [Fact]
    public void Extracts_Italian_Section()
    {
        var s = UpdateManager.ExtractLanguageSection(Trilingual, AppLanguage.It);
        Assert.Contains("Nuova funzione A", s);
        Assert.DoesNotContain("Fixed bug B", s);
    }

    [Fact]
    public void Falls_Back_To_Full_Body_Without_Language_Headings()
    {
        const string germanOnly = "**Neu in 1.3.0**\n- Nur deutscher Text\n## Voraussetzungen\n- Windows";
        var s = UpdateManager.ExtractLanguageSection(germanOnly, AppLanguage.En);
        Assert.Equal(germanOnly, s); // altes Format -> kompletter Text
    }

    [Fact]
    public void Falls_Back_To_Full_Body_When_Wanted_Language_Missing()
    {
        const string deEn = "## Deutsch\n- Hallo\n## English\n- Hello";
        var s = UpdateManager.ExtractLanguageSection(deEn, AppLanguage.It);
        Assert.Equal(deEn, s); // Italienisch fehlt -> alles zeigen statt nichts
    }

    [Fact]
    public void Accepts_Heading_Level_And_Colon_Variations()
    {
        const string notes = "### Deutsch:\n- Tief verschachtelt\n# ENGLISH\n- Loud heading";
        Assert.Contains("Tief verschachtelt", UpdateManager.ExtractLanguageSection(notes, AppLanguage.De));
        Assert.Contains("Loud heading", UpdateManager.ExtractLanguageSection(notes, AppLanguage.En));
    }
}
