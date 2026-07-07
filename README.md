# TeamsMentionNotificationCenter

Leuchtet den Bildschirmrand **rot auf, wenn im Microsoft-Teams-Live-Transkript dein Name fällt** –
damit du Teams leise/stumm lassen und in Ruhe (mit Musik) arbeiten kannst, ohne ständig aufs
Untertitel-Fenster schauen zu müssen. Auf Wunsch stellt es Teams automatisch laut und pausiert die
Musik; per Knopf/Shortcut geht es wieder in den Ruhe-Modus.

> Windows-Desktop-App (C# / .NET 10 / WPF), läuft im System-Tray. **Voll in der App konfigurierbar** –
> kein fest verdrahteter Name, mehrsprachig (DE/EN/IT), damit jede:r es mit dem eigenen Namen nutzen kann.

## Funktionen

- **Namens-Erkennung im Live-Transkript** des neuen Teams (per UI Automation; sprachunabhängig und
  auch über mehrere bzw. gehaltene Calls hinweg).
- **Roter Glow** rund um den Bildschirmrand bei einem Treffer – Farbe, Dauer, Dicke, Monitor(e) und
  optionaler Dauer-Rand einstellbar, mit Live-Vorschau.
- **Einblendung „Wer hat mich gerufen?"** – zeigt bei einem Treffer den Sprecher an
  (Text mit `{Name}`-Platzhalter, Position 3×3, Schriftgröße, Farbe, Dauer, Deckkraft
  und Monitore einstellbar, mit Live-Vorschau).
- **Optionaler Signalton** bei Erkennung (Auswahl aus den Windows-Sounds, Lautstärke und
  Ausgabegerät wählbar, mit Test-Knopf).
- **Verpasste Erwähnungen:** Antwortest du nach einer Nennung nicht innerhalb einstellbarer Zeit,
  erscheint ein Overlay im Windows-11-Benachrichtigungsstil (wer hat wann gerufen; Position, Farbe,
  Deckkraft einstellbar). Einträge lassen sich erledigen, zurückstellen („Erinnere mich in X Minuten",
  Presets konfigurierbar) oder an die **Rückkehr der Person in den Call** knüpfen. Der erste Reiter im
  Einstellungsfenster zeigt die Historie nach Tagen gruppiert (orange = offen, grau = zurückgestellt,
  grün = erledigt) mit Wieder-öffnen- und Lösch-Funktionen (pro Eintrag, pro Tag, alle).
- **Ton- & Musiksteuerung:** Teams pro Modus laut/leise/stumm (pro App, auf **allen**
  Wiedergabegeräten – auch Multi-Endpunkt-Headsets wie Razer Nari) und Musik (z. B. Spotify)
  automatisch pausieren/fortsetzen (Windows-Mediensteuerung). Einzelne Geräte lassen sich von der
  Automatik ausnehmen (z. B. das Gerät des Teams-„Zweiten Rufsignals").
- **Zwei Modi:** *Ruhe* (Teams leise, Musik läuft) ↔ *Gespräch* (Teams laut, Musik pausiert).
  Automatischer Wechsel bei Treffer, optionale Auto-Rückkehr nach eigener Sprechpause.
  Im Gesprächs-Modus pausiert die Erkennung – erneute Nennungen lösen keinen neuen Alarm aus.
- **Eingehende Anrufe:** Klingelt ein zusätzlicher Anruf (Teams-Anruf-Popup), wechselt die App
  automatisch in den Gesprächs-Modus – Klingeln und Anruf sind sofort laut, die Musik pausiert (abschaltbar).
- **Globale Hotkeys:** Modus umschalten, Ruhe, Gespräch, Erkennung an/aus – frei belegbar.
  AltGr-sicher umgesetzt: Zeichen wie `@` und `€` (AltGr+Q/E) funktionieren trotz `Ctrl+Alt`-Kürzeln.
- **System-Tray-Icon** mit Statusanzeige (grün = Gespräch, rot = Ruhe, grau = Erkennung aus) und
  Kontextmenü (Modus, Erkennung, Glow-Test, Einstellungen, Update-Prüfung, Release Notes, Beenden).
- **Einstellungs-Oberfläche** mit neun Reitern, mehrsprachig (Deutsch/Englisch/Italienisch),
  blaue Markierung ungespeicherter Änderungen, Autostart mit Windows.
- **Selbst-Update:** prüft beim Start (abschaltbar) und per Tray-Menü auf neue GitHub-Releases.
  Standardmäßig installieren sich Updates **still im Hintergrund** (zusätzlich alle 6 h geprüft);
  der aktuelle Modus wird über den Neustart hinweg wiederhergestellt. Alternativ fragt ein Popup
  „Jetzt aktualisieren / Später". Nach dem Update zeigt die App einmalig **„Was ist neu"** – in der
  eingestellten Sprache.
- **Release Notes in der App:** eigener Einstellungs-Reiter mit allen Versionen untereinander
  (neueste zuerst), direkt erreichbar über Tray → *Release Notes anzeigen*.
- **Nur eine Instanz:** Ein Doppelstart zeigt einen Hinweis und beendet die zusätzliche Instanz.
- **Datensparsam:** Transkript nur lokal im RAM, nichts wird gespeichert/geloggt/gesendet.

## Voraussetzungen

- Windows 10/11
- **Neues Microsoft Teams** („Teams 2.0") mit **aktivierten Live-Untertiteln**
  (im Call: *Weitere Aktionen → Sprache und Text → Live-Untertitel anzeigen*; idealerweise
  *Untertitel in neuem Fenster öffnen*).
- Optional: Spotify o. Ä. (für die Musik-Automatik; erkannt über die Windows-Mediensteuerung).
- Zum Nutzen der fertigen EXE ist **kein .NET nötig** (self-contained); zum Selbstbauen: .NET 10 SDK.

## Installation & Updates

1. **Neueste EXE herunterladen:**
   👉 https://github.com/robertkieschke/TeamsMentionNotificationCenter/releases/latest
   (`TeamsMentionNotificationCenter-<Version>-win-x64.exe`, ~80 MB, keine Installation nötig)
2. **Doppelklick** – die App startet im System-Tray. Beim Start von einem „portablen" Ort
   (z. B. Downloads) bietet sie an, sich nach `%LOCALAPPDATA%\Programs` zu installieren
   (inkl. Startmenü-Eintrag). Empfohlen: annehmen – von dort funktioniert das Selbst-Update
   garantiert, und die App übersteht das Aufräumen des Download-Ordners.
   (Bewusst **nicht** `C:\Programme` – dort bräuchte jedes Update Adminrechte.)
3. **Fertig.** Künftige Versionen installiert die App standardmäßig **von selbst im Hintergrund**
   und zeigt danach, was neu ist. Einstellungen bleiben bei Updates erhalten
   (`%APPDATA%\TeamsMentionNotificationCenter\settings.json`).

## Bedienung

- **Einstellungen** öffnest du per **Doppelklick aufs Tray-Icon** oder über das Tray-Menü.
  Alle Parameter sind dort in Reitern editierbar
  (Erkennung · Ton & Musik · Glow-Rand · Einblendung · Signalton · Tastenkürzel · Sonstiges ·
  Release Notes · Info) – die Sprache ist zwischen Deutsch, Englisch und Italienisch umschaltbar.
  Ungespeicherte Änderungen werden blau markiert; „Übernehmen" speichert, „Verwerfen" setzt zurück.
- **Standard-Hotkeys** (frei änderbar): `Ctrl+Alt+T` Modus umschalten · `Ctrl+Alt+Q` Ruhe ·
  `Ctrl+Alt+G` Gespräch · `Ctrl+Alt+E` Erkennung an/aus.
- **Release Notes:** Tray → *Release Notes anzeigen* öffnet die Einstellungen direkt im Reiter
  *Release Notes* – alle Versionen untereinander (neueste zuerst), in der eingestellten Sprache.

## Konfiguration

Alles wird über die **Einstellungs-Oberfläche** gepflegt (Tray → *Einstellungen…*). Persistiert wird
nach `%APPDATA%\TeamsMentionNotificationCenter\settings.json`. Die Felder im Überblick:

| Feld | Bedeutung |
|---|---|
| `TriggerWords` | Begriffe/Namen, auf die reagiert wird (z. B. Vor-/Nachname). |
| `OwnSpeakerName` / `IgnoreOwnSpeaker` | Eigener Name im Transkript – nicht auslösen, wenn du selbst sprichst. |
| `FuzzyEnabled` / `FuzzyMaxDistance` | Toleranz gegen Erkennungsfehler (Levenshtein). |
| `TriggerCooldownMs` | Mindestabstand zwischen zwei Auslösungen. |
| `DetectionEnabled` | Master-Schalter der Erkennung (auch per Tray/Hotkey umschaltbar). |
| `GlowColorHex` / `GlowDurationMs` / `GlowThickness` | Aussehen des Rand-Glows. |
| `GlowMonitors` | Zu beleuchtende Monitore (leer = alle). |
| `PersistentBorder` | Dezenter Dauer-Rand im Gesprächs-Modus: `Never` / `TriggerOnly` / `Always`. |
| `BannerEnabled` / `BannerText` | Einblendung „Wer hat mich gerufen?" an/aus; Text mit `{Name}`-Platzhalter. |
| `BannerVertical` / `BannerHorizontal` | Position: `Top`/`Center`/`Bottom` × `Left`/`Center`/`Right`. |
| `BannerFontSize` / `BannerColorHex` / `BannerDurationMs` / `BannerOpacityPercent` / `BannerMonitors` | Aussehen der Einblendung (Monitore leer = alle). |
| `QuietBehavior` / `QuietLevelPercent` / `ConversationLevelPercent` | Teams-Ton je Modus (leiser oder stumm). |
| `MusicAppHint` | Medien-App für Pause/Resume (z. B. `Spotify`). |
| `AudioExcludedDeviceIds` | Wiedergabegeräte, die nie automatisch angepasst werden – z. B. das Gerät des Teams-„Zweiten Rufsignals", damit das Klingeln zusätzlicher Anrufe laut bleibt. |
| `AutoEnterConversationOnTrigger` / `RaiseTeamsOnTrigger` / `PauseMusicOnTrigger` | Auto-Aktionen bei Treffer. |
| `EnterConversationOnIncomingCall` | Bei eingehendem Anruf (Klingel-Popup) automatisch in den Gesprächs-Modus. |
| `AutoReturnToQuietEnabled` / `AutoReturnAfterSeconds` / `AutoReturnAlsoWhenManual` | Auto-Rückkehr in den Ruhe-Modus nach eigener Sprechpause. |
| `TriggerSoundEnabled` / `TriggerSoundFile` / `TriggerSoundVolume` / `TriggerSoundDeviceId` | Optionaler Signalton bei Erkennung. |
| `MissedMentionsEnabled` / `MentionAnswerTimeoutSeconds` | Verpasste Erwähnungen erfassen; ohne eigene Antwort innerhalb dieser Zeit entsteht ein Eintrag. |
| `MentionRepeatMinutes` / `MentionRetentionDays` | Mindestabstand für neue Einträge derselben Person; Auto-Löschung nach X Tagen. |
| `MentionOverlayVertical` / `MentionOverlayHorizontal` / `MentionOverlayColorHex` / `MentionOverlayOpacityPercent` | Position und Aussehen des Verpasst-Overlays. |
| `SnoozePresetsMinutes` | Auswahlwerte für „Erinnere mich in X Minuten". |
| `HotkeyToggle` / `HotkeyQuiet` / `HotkeyConversation` / `HotkeyToggleDetection` | Globale Tastenkürzel. |
| `StartInConversationMode` / `StartWithWindows` | Startverhalten / Autostart mit Windows. |
| `CheckUpdatesOnStartup` | Beim Programmstart auf neue GitHub-Releases prüfen. |
| `SilentAutoUpdate` | Updates ohne Nachfrage im Hintergrund installieren (Modus wird wiederhergestellt). |
| `ShowNotesAfterUpdate` | Nach einem Update einmalig die Versionshinweise anzeigen. |
| `OfferInstallOnStartup` | Bei portablem Start die Installation nach `%LOCALAPPDATA%\Programs` anbieten. |
| `PollIntervalMs` | Abtast-Intervall der Transkript-Überwachung. |
| `Language` | UI-Sprache (`De` / `En` / `It`). |
| `DebugLogging` | Diagnose-Log nach `%APPDATA%\TeamsMentionNotificationCenter\log.txt` (ohne Gesprächsinhalte). |

**Mehrsprachige Release-Notes:** Werden die Notes eines Releases mit den Abschnitten `## Deutsch`,
`## English`, `## Italiano` verfasst, zeigen „Was ist neu"-Fenster und Release-Notes-Reiter nur den
Abschnitt der eingestellten Sprache (sonst den kompletten Text).

## Datenschutz

Das Transkript wird **ausschließlich lokal und nur im Arbeitsspeicher** verarbeitet, ausschließlich zum
Abgleich mit den Trigger-Wörtern – Gesprächsinhalte werden **nie gespeichert, geloggt oder übertragen**.
Einzige lokale Ablage: Für die Funktion **Verpasste Erwähnungen** speichert die App unter `%APPDATA%`
Sprechername, Uhrzeit und Bearbeitungsstatus der Nennung (keine Inhalte). Diese Einträge werden nach
einstellbarer Zeit (Standard 30 Tage) automatisch entfernt und sind jederzeit in der App löschbar.
Untertitel müssen in Teams pro Meeting aktiv sein (können durch Admin-Richtlinie deaktiviert sein).
Bitte lokale Regeln/Mitbestimmung zur Gesprächsmitschrift beachten.

## Entwicklung

```bash
# Bauen & Starten (Debug)
dotnet build src/TeamsMentionNotificationCenter/TeamsMentionNotificationCenter.csproj
src/TeamsMentionNotificationCenter/bin/Debug/net10.0-windows10.0.19041.0/TeamsMentionNotificationCenter.exe

# Tests
dotnet test tests/TeamsMentionNotificationCenter.Tests/TeamsMentionNotificationCenter.Tests.csproj

# Verteilbare Single-File-EXE (self-contained, läuft ohne installiertes .NET)
dotnet publish src/TeamsMentionNotificationCenter/TeamsMentionNotificationCenter.csproj -p:PublishProfile=win-x64
# -> src/TeamsMentionNotificationCenter/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/TeamsMentionNotificationCenter.exe
```

**Release erstellen (Maintainer):** Version in `TeamsMentionNotificationCenter.csproj`
(`Version`/`FileVersion`) **und** `Core/AppInfo.cs` erhöhen → publishen → EXE als
`TeamsMentionNotificationCenter-<Version>-win-x64.exe` per `gh release create v<Version> …` anhängen.
Release-Notes dreisprachig verfassen (`## Deutsch` / `## English` / `## Italiano`). Laufende
Installationen aktualisieren sich danach selbst.

## Architektur (Kurzüberblick)

```
src/TeamsMentionNotificationCenter/
  Transcript/UiaTranscriptSource.cs  UIA-Zugriff auf Teams-Untertitel (Chromium-A11y-Aktivierung + Parser)
                                     + Erkennung des Anruf-Popups (eingehende Anrufe)
  Detection/NameMatcher.cs           Normalisierung, Fuzzy-Match, Cooldown, „eigenen Sprecher ignorieren"
  Overlay/GlowOverlay.cs             transparente, klick-durchlässige Rand-Overlays je Monitor
  Overlay/CallerBanner.cs            Einblendung „Wer hat mich gerufen?" (klick-durchlässig, animiert)
  Overlay/MentionOverlay.cs          Verpasst-Overlay im Win11-Benachrichtigungsstil (interaktiv)
  Core/MissedMentions.cs             Modell + persistenter Speicher der verpassten Erwähnungen
  Audio/AudioController.cs           Teams-Volume auf allen Geräten (NAudio) + Musik Pause/Resume (SMTC)
  Core/AppController.cs              Zustandsautomat (Ruhe ↔ Gespräch) + Verdrahtung
  Core/UpdateManager.cs              Update-Prüfung, Silent-/Dialog-Update, Release-Notes-Abruf
  Core/InstallManager.cs             Installations-Angebot nach %LOCALAPPDATA%\Programs
  Core/SoundNotifier.cs              Signalton bei Erkennung (WASAPI, Gerät/Lautstärke)
  Core/AutostartManager.cs           Autostart über HKCU-Run-Schlüssel
  Core/Branding.cs                   Logo & Tray-Icon (programmatisch gezeichnet)
  Input/HotkeyManager.cs             globale Hotkeys (Low-Level-Hook, AltGr-sicher)
  Localization/Loc.cs                Mehrsprachigkeit (DE/EN/IT)
  Settings/AppSettings.cs            Einstellungsmodell + JSON-Persistenz
  Settings/SettingsWindow.cs         Einstellungs-Oberfläche (Reiter, blaue Dirty-Markierung)
  Tray/TrayIconManager.cs            System-Tray-Icon & Menü
tests/TeamsMentionNotificationCenter.Tests/          Unit-Tests (NameMatcher, Release-Notes-Sprachwahl, Verpasst-Logik)
tools/TeamsMentionNotificationCenter.Diagnostics/    Diagnose-Tool (UIA-/SMTC-/NAudio-Proben)
tools/IconGen/                                       erzeugt app.ico aus dem Branding-Logo
```

## Bekannte Grenzen

- Teams-Updates können die interne Struktur der Untertitel oder des Anruf-Popups ändern; die Erkennung
  sucht defensiv (Untertitel über `fui-ChatMessageCompact`-Gruppen, Anrufe über Annehmen-/Ablehnen-
  Buttons in mehreren Sprachen) und meldet ihren Status im Tray.
- Die Erkennung ist nur so gut wie die Teams-Transkription (Akzent, Nebengeräusche, überlappende Sprecher).
- Windows regelt Pro-App-Lautstärke je Audio-Session: Klingeln und Call-Ton auf **demselben** Gerät
  lassen sich nicht getrennt regeln. Lösungen: automatischer Gesprächs-Modus bei eingehenden Anrufen
  (Standard) und/oder Teams-„Zweites Rufsignal" auf ein ausgeschlossenes Gerät legen.
