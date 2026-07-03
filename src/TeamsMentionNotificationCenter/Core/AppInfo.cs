namespace TeamsMentionNotificationCenter.Core;

/// <summary>
/// Zentrale Anwendungs-Metadaten. Der Anzeigename wird überall in der UI verwendet
/// (Tray, Fenstertitel, Info-Tab). Hier anpassen, um die App umzubenennen.
/// </summary>
public static class AppInfo
{
    public const string DisplayName = "Teams Mention Notification Center";
    public const string Version = "1.2";

    // Entwickler-/Impressum-Angaben (für den Info-Tab) – bei Bedarf anpassen.
    public const string Developer = "Robert H. Kieschke";
    public const string Company = "inno-focus digital gmbh";
    public const string Year = "2026";

    public const string Description =
        "Überwacht das Live-Transkript in Microsoft Teams und schlägt Alarm, sobald einer deiner " +
        "hinterlegten Namen/Begriffe gesprochen wird – auch über mehrere parallele Calls hinweg. " +
        "So kannst du Teams leise stellen und ungestört (z. B. mit Musik) arbeiten, ohne ständig " +
        "auf das Untertitel-Fenster schauen zu müssen.";

    public const string TechStack = ".NET / WPF · UI Automation (FlaUI) · NAudio · WinRT SMTC";
}
