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
- **Ton- & Musiksteuerung:** Teams pro Modus laut/leise/stumm (NAudio) und Musik (z. B. Spotify)
  automatisch pausieren/fortsetzen (Windows-Mediensteuerung / SMTC).
- **Zwei Modi:** *Ruhe* (Teams leise, Musik läuft) ↔ *Gespräch* (Teams laut, Musik pausiert).
  Automatischer Wechsel bei Treffer, optionale Auto-Rückkehr nach eigener Sprechpause.
  Im Gesprächs-Modus pausiert die Erkennung – erneute Nennungen lösen keinen neuen Alarm aus.
- **Eingehende Anrufe:** Klingelt ein zusätzlicher Anruf (Teams-Anruf-Popup), wechselt die App
  automatisch in den Gesprächs-Modus – Klingeln und Anruf sind sofort laut, die Musik pausiert (abschaltbar).
- **Globale Hotkeys:** Modus umschalten, Ruhe, Gespräch, Erkennung an/aus – alle frei belegbar.
- **Optionaler Signalton** bei Erkennung (eigene WAV, Lautstärke und Ausgabegerät wählbar).
- **System-Tray-Icon** mit Statusanzeige (grün = Gespräch, rot = Ruhe, grau = Erkennung aus) und Kontextmenü.
- **Einstellungs-Oberfläche** mit Reitern, mehrsprachig (Deutsch/Englisch/Italienisch), Autostart mit Windows.
- **Selbst-Update:** prüft beim Start (abschaltbar) und per Tray-Menü auf neue GitHub-Releases.
  Standardmäßig installieren sich Updates **still im Hintergrund** (zusätzlich alle 6 h geprüft);
  der aktuelle Modus wird über den Neustart hinweg wiederhergestellt. Alternativ (Häkchen aus) fragt
  ein Popup „Jetzt aktualisieren / Später". Empfohlener Ablageort ist ein benutzerbeschreibbarer
  Ordner wie `%LOCALAPPDATA%\Programs` (bewusst NICHT `C:\Programme` – dort bräuchte jedes Update
  Adminrechte).
- **Datensparsam:** Transkript nur lokal im RAM, nichts wird gespeichert/geloggt/gesendet.

## Voraussetzungen

- Windows 10/11
- **Neues Microsoft Teams** („Teams 2.0") mit **aktivierten Live-Untertiteln**
  (im Call: *Weitere Aktionen → Sprache und Text → Live-Untertitel anzeigen*; idealerweise
  *Untertitel in neuem Fenster öffnen*).
- .NET 10 Desktop Runtime (zum Bauen: .NET 10 SDK).
- Optional: Spotify (für die Musik-Automatik; erkannt über die Windows-Mediensteuerung).

## Bauen & Starten

```bash
dotnet build src/TeamsMentionNotificationCenter/TeamsMentionNotificationCenter.csproj -c Release
# Starten:
src/TeamsMentionNotificationCenter/bin/Release/net10.0-windows10.0.19041.0/TeamsMentionNotificationCenter.exe
```

Die App erscheint als **Tray-Icon** (weiße Sprechblase mit Status-Punkt). Rechtsklick öffnet das Menü:
Modus wechseln, Erkennung an/aus, **Test: Glow auslösen**, Einstellungen öffnen/neu laden, Beenden.

## Verteilung (Single-File-EXE)

Eine eigenständige, **self-contained** EXE erzeugen – läuft **ohne installiertes .NET** auf dem Zielrechner:

```bash
dotnet publish src/TeamsMentionNotificationCenter/TeamsMentionNotificationCenter.csproj -p:PublishProfile=win-x64
```

Ergebnis ist **eine einzige Datei** (~80 MB, inkl. App-Icon):

```
src/TeamsMentionNotificationCenter/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/TeamsMentionNotificationCenter.exe
```

Diese `.exe` kannst du direkt an Kolleg:innen weitergeben – Doppelklick startet die App im Tray, ganz ohne
Installation. Beim Start von einem „portablen" Ort (z. B. Downloads) bietet die App an, sich nach
`%LOCALAPPDATA%\Programs` zu installieren (inkl. Startmenü-Eintrag) – von dort funktioniert das
Selbst-Update zuverlässig. Die daneben liegende `.pdb` ist nur für Debugging und muss nicht mitverteilt
werden. Einstellungen legt die App beim ersten Start unter `%APPDATA%\TeamsMentionNotificationCenter` an.

## Bedienung

- **Einstellungen** öffnest du über das Tray-Menü. Alle Parameter sind dort in Reitern editierbar
  (Erkennung · Ton & Musik · Glow-Rand · Signalton · Tastenkürzel · Sonstiges · Info) – die Sprache ist
  zwischen Deutsch, Englisch und Italienisch umschaltbar. Ungespeicherte Änderungen werden blau markiert;
  „Übernehmen" speichert, „Verwerfen" setzt zurück.
- **Standard-Hotkeys** (frei änderbar): `Ctrl+Alt+T` Modus umschalten · `Ctrl+Alt+Q` Ruhe ·
  `Ctrl+Alt+G` Gespräch · `Ctrl+Alt+E` Erkennung an/aus.
- **Release Notes:** Tray → *Release Notes anzeigen* öffnet die Einstellungen direkt im Reiter
  *Release Notes* – alle Versionen untereinander (neueste zuerst), in der eingestellten Sprache.

## Konfiguration

Alles wird über die **Einstellungs-Oberfläche** gepflegt (Tray → *Einstellungen öffnen*). Persistiert wird
nach `%APPDATA%\TeamsMentionNotificationCenter\settings.json`. Wichtigste Felder:

| Feld | Bedeutung |
|---|---|
| `TriggerWords` | Begriffe/Namen, auf die reagiert wird (z. B. Vor-/Nachname). |
| `OwnSpeakerName` / `IgnoreOwnSpeaker` | Eigener Name im Transkript – nicht auslösen, wenn du selbst sprichst. |
| `FuzzyEnabled` / `FuzzyMaxDistance` | Toleranz gegen Erkennungsfehler (Levenshtein). |
| `TriggerCooldownMs` | Mindestabstand zwischen zwei Auslösungen. |
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
| `HotkeyToggle` / `HotkeyQuiet` / `HotkeyConversation` / `HotkeyToggleDetection` | Globale Tastenkürzel. |
| `StartInConversationMode` / `StartWithWindows` | Startverhalten / Autostart mit Windows. |
| `CheckUpdatesOnStartup` | Beim Programmstart auf neue GitHub-Releases prüfen. |
| `SilentAutoUpdate` | Updates ohne Nachfrage im Hintergrund installieren (Modus wird wiederhergestellt). |
| `ShowNotesAfterUpdate` | Nach einem Update einmalig die Versionshinweise der neuen Version anzeigen. Mehrsprachige Notes: Abschnitte `## Deutsch` / `## English` / `## Italiano` im Release-Text – angezeigt wird nur die UI-Sprache. |
| `Language` | UI-Sprache (`De` / `En` / `It`). |

## Datenschutz

Das Transkript wird **ausschließlich lokal und nur im Arbeitsspeicher** verarbeitet, ausschließlich zum
Abgleich mit den Trigger-Wörtern – es wird **nichts gespeichert, geloggt oder übertragen**. Untertitel
müssen in Teams pro Meeting aktiv sein (können durch Admin-Richtlinie deaktiviert sein). Bitte lokale
Regeln/Mitbestimmung zur Gesprächsmitschrift beachten.

## Architektur (Kurzüberblick)

```
src/TeamsMentionNotificationCenter/
  Transcript/UiaTranscriptSource.cs  UIA-Zugriff auf Teams-Untertitel (Chromium-A11y-Aktivierung + Parser)
  Detection/NameMatcher.cs           Normalisierung, Fuzzy-Match, Cooldown, „eigenen Sprecher ignorieren"
  Overlay/GlowOverlay.cs             transparente, klick-durchlässige Rand-Overlays je Monitor
  Audio/AudioController.cs           Teams-Volume (NAudio) + Musik pausieren/fortsetzen (SMTC)
  Core/SoundNotifier.cs              Signalton bei Erkennung (WASAPI, Gerät/Lautstärke)
  Input/HotkeyManager.cs             globale Hotkeys
  Core/AppController.cs              Zustandsautomat (Ruhe ↔ Gespräch) + Verdrahtung
  Core/Branding.cs                   Logo & Tray-Icon (programmatisch gezeichnet)
  Localization/Loc.cs                Mehrsprachigkeit (DE/EN/IT)
  Settings/AppSettings.cs            Einstellungsmodell + JSON-Persistenz
  Settings/SettingsWindow.cs         Einstellungs-Oberfläche (Reiter, blaue Dirty-Markierung)
  Tray/TrayIconManager.cs            System-Tray-Icon & Menü
tests/TeamsMentionNotificationCenter.Tests/          Unit-Tests (NameMatcher)
tools/TeamsMentionNotificationCenter.Diagnostics/    Diagnose-Tool (UIA-/SMTC-/NAudio-Proben)
```

## Bekannte Grenzen

- Teams-Updates können die interne Struktur der Untertitel ändern; der Parser sucht defensiv nach
  `fui-ChatMessageCompact`-Gruppen und fällt auf ältere Bezeichner zurück. Ein OCR-Fallback ist vorgesehen.
- Die Erkennung ist nur so gut wie die Teams-Transkription (Akzent, Nebengeräusche, überlappende Sprecher).
</content>
</invoke>
