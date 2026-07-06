namespace TeamsMentionNotificationCenter.Localization;

public enum AppLanguage
{
    De,
    En,
    It
}

/// <summary>
/// Einfache Lokalisierung. Schlüssel ist der deutsche Ausgangstext; fehlt eine Übersetzung,
/// wird auf Deutsch zurückgefallen. <see cref="T"/> übersetzt, <see cref="Tf"/> formatiert zusätzlich.
/// </summary>
public static class Loc
{
    public static AppLanguage Language { get; set; } = AppLanguage.De;

    public static string T(string de)
    {
        if (Language == AppLanguage.De) return de;
        if (Map.TryGetValue(de, out var t))
            return Language == AppLanguage.En ? t.En : t.It;
        return de; // Fallback: Deutsch
    }

    public static string Tf(string de, params object[] args) => string.Format(T(de), args);

    private static readonly Dictionary<string, (string En, string It)> Map = new(StringComparer.Ordinal)
    {
        // --- Tray ---
        ["Erkennung aktiv"] = ("Detection active", "Rilevamento attivo"),
        ["In Gesprächs-Modus wechseln"] = ("Switch to conversation mode", "Passa alla modalità conversazione"),
        ["In Ruhe-Modus wechseln"] = ("Switch to quiet mode", "Passa alla modalità silenziosa"),
        ["Test: Glow auslösen"] = ("Test: trigger glow", "Test: attiva bordo"),
        ["Einstellungen…"] = ("Settings…", "Impostazioni…"),
        ["Einstellungen neu laden (aus Datei)"] = ("Reload settings (from file)", "Ricarica impostazioni (da file)"),
        ["Beenden"] = ("Exit", "Esci"),
        ["Modus: Ruhe"] = ("Mode: Quiet", "Modalità: Silenzioso"),
        ["Modus: Gespräch"] = ("Mode: Conversation", "Modalità: Conversazione"),

        // --- Status ---
        ["Starte …"] = ("Starting …", "Avvio …"),
        ["Name erkannt: {0}"] = ("Name detected: {0}", "Nome rilevato: {0}"),
        ["Einstellungen übernommen ({0} Trigger-Wörter)"] = ("Settings applied ({0} trigger words)", "Impostazioni applicate ({0} parole chiave)"),
        ["Teams läuft nicht."] = ("Teams is not running.", "Teams non è in esecuzione."),
        ["Kein Teams-Fenster gefunden."] = ("No Teams window found.", "Nessuna finestra di Teams trovata."),
        ["Untertitel aktiv ({0} Zeilen, {1} Fenster)."] = ("Captions active ({0} lines, {1} windows).", "Sottotitoli attivi ({0} righe, {1} finestre)."),
        ["Fenster gefunden, aber keine Untertitel (sind Untertitel aktiv?)."] = ("Window found, but no captions (are captions on?).", "Finestra trovata, ma nessun sottotitolo (i sottotitoli sono attivi?)."),
        ["Fehler beim Lesen – versuche erneut …"] = ("Error while reading – retrying …", "Errore di lettura – nuovo tentativo …"),

        // --- Einstellungen: Rahmen ---
        [" – Einstellungen"] = (" – Settings", " – Impostazioni"),
        ["Übernehmen"] = ("Apply", "Applica"),
        ["Verwerfen"] = ("Discard", "Annulla"),
        ["Schließen"] = ("Close", "Chiudi"),
        ["Übernommen ✓"] = ("Applied ✓", "Applicato ✓"),
        ["Fehler beim Übernehmen"] = ("Error while applying", "Errore nell'applicazione"),
        ["Verworfen"] = ("Discarded", "Annullato"),
        ["Standardwerte geladen – bitte übernehmen"] = ("Defaults loaded – please apply", "Predefiniti caricati – applica"),

        ["Die Anwendung läuft bereits – du findest sie als Symbol im Infobereich der Taskleiste (Tray). Diese zusätzliche Instanz wird jetzt beendet."] =
            ("The application is already running – look for its icon in the taskbar notification area (tray). This additional instance will now close.",
             "L'applicazione è già in esecuzione – trovi la sua icona nell'area di notifica della barra delle applicazioni (tray). Questa istanza aggiuntiva verrà ora chiusa."),

        // --- Tabs ---
        ["Erkennung"] = ("Detection", "Rilevamento"),
        ["Ton & Musik"] = ("Sound & music", "Audio e musica"),
        ["Glow-Rand"] = ("Glow border", "Bordo luminoso"),
        ["Einblendung"] = ("Banner", "Banner"),
        ["Signalton"] = ("Alert sound", "Suono di avviso"),
        ["Tastenkürzel"] = ("Shortcuts", "Scorciatoie"),
        ["Sonstiges"] = ("Other", "Altro"),
        ["Info"] = ("Info", "Info"),

        // --- Erkennung ---
        ["Trigger-Wörter (eines pro Zeile):"] = ("Trigger words (one per line):", "Parole chiave (una per riga):"),
        ["Eigener Name (im Transkript):"] = ("Your name (in the transcript):", "Il tuo nome (nella trascrizione):"),
        ["Nicht auslösen, wenn ich selbst spreche"] = ("Don't trigger when I'm speaking", "Non attivare quando parlo io"),
        ["Fuzzy-Match (Erkennungsfehler tolerieren)"] = ("Fuzzy match (tolerate recognition errors)", "Corrispondenza fuzzy (tollera errori)"),
        ["Fuzzy-Toleranz (max. Abweichung):"] = ("Fuzzy tolerance (max. distance):", "Tolleranza fuzzy (distanza max.):"),
        ["Cooldown zwischen Auslösungen (ms):"] = ("Cooldown between triggers (ms):", "Pausa tra le attivazioni (ms):"),

        // --- Ton & Musik ---
        ["Teams im Ruhe-Modus:"] = ("Teams in quiet mode:", "Teams in modalità silenziosa:"),
        ["Teams Ruhe-Lautstärke (%):"] = ("Teams quiet volume (%):", "Volume Teams silenzioso (%):"),
        ["Teams Gesprächs-Lautstärke (%):"] = ("Teams conversation volume (%):", "Volume Teams conversazione (%):"),
        ["Musik-App (SMTC-Kennung):"] = ("Music app (SMTC id):", "App musicale (id SMTC):"),
        ["Teams-Ton automatisch steuern (laut/leise)"] = ("Control Teams volume automatically (up/down)", "Controlla il volume di Teams automaticamente (su/giù)"),
        ["Musik automatisch steuern (Pause/Fortsetzen)"] = ("Control music automatically (pause/resume)", "Controlla la musica automaticamente (pausa/ripresa)"),
        ["Bei Namensnennung automatisch in den Gesprächs-Modus"] = ("Auto-switch to conversation mode on a mention", "Passa alla conversazione quando il nome viene detto"),
        ["Leiser stellen"] = ("Lower volume", "Abbassa volume"),
        ["Stumm schalten"] = ("Mute", "Disattiva audio"),

        // --- Glow ---
        ["Farbe (Hex, z. B. #FF3B30):"] = ("Color (hex, e.g. #FF3B30):", "Colore (hex, es. #FF3B30):"),
        ["Dicke:"] = ("Thickness:", "Spessore:"),
        ["Dauer (ms):"] = ("Duration (ms):", "Durata (ms):"),
        ["Dauer-Rand im Gespräch zeigen:"] = ("Persistent border in conversation:", "Bordo permanente in conversazione:"),
        ["Zu beleuchtende Monitore (nichts markiert = alle):"] = ("Monitors to light up (none checked = all):", "Monitor da illuminare (nessuno = tutti):"),
        ["Glow testen"] = ("Test glow", "Prova bordo"),
        ["[primär]"] = ("[primary]", "[primario]"),
        ["Nie"] = ("Never", "Mai"),
        ["Nur bei Namensnennung (Trigger)"] = ("Only on a mention (trigger)", "Solo quando il nome viene detto"),
        ["Immer im Gesprächs-Modus"] = ("Always in conversation mode", "Sempre in conversazione"),

        ["Bei eingehendem Anruf automatisch in den Gesprächs-Modus (Klingeln wird laut)"] =
            ("Automatically enter conversation mode on an incoming call (ringing gets loud)",
             "Passa automaticamente alla modalità conversazione quando arriva una chiamata (lo squillo diventa forte)"),
        ["Eingehender Anruf – Gesprächs-Modus aktiviert"] =
            ("Incoming call – conversation mode activated", "Chiamata in arrivo – modalità conversazione attivata"),
        ["Diese Wiedergabegeräte nie automatisch anpassen:"] =
            ("Never adjust these playback devices automatically:", "Non regolare mai automaticamente questi dispositivi di riproduzione:"),
        ["Tipp: In Teams ein 'Zweites Rufsignal' auf ein hier ausgeschlossenes Gerät legen (z. B. Lautsprecher) – dann bleibt das Klingeln zusätzlicher Anrufe immer laut, während der Call leise wird."] =
            ("Tip: In Teams, set a 'secondary ringer' to a device excluded here (e.g. speakers) – the ringing of additional incoming calls then always stays loud while the call itself gets quiet.",
             "Suggerimento: in Teams imposta una 'suoneria secondaria' su un dispositivo escluso qui (es. altoparlanti) – lo squillo delle chiamate in arrivo resta sempre forte mentre la chiamata diventa silenziosa."),

        // --- Einblendung ---
        ["Bei Erkennung einblenden, wer dich gerufen hat"] =
            ("Show who called you when your name is detected", "Mostra chi ti ha chiamato quando viene rilevato il nome"),
        ["Text ({Name} = Sprecher):"] = ("Text ({Name} = speaker):", "Testo ({Name} = chi parla):"),
        ["Vertikale Position:"] = ("Vertical position:", "Posizione verticale:"),
        ["Horizontale Position:"] = ("Horizontal position:", "Posizione orizzontale:"),
        ["Oben"] = ("Top", "In alto"),
        ["Mitte"] = ("Center", "Al centro"),
        ["Unten"] = ("Bottom", "In basso"),
        ["Links"] = ("Left", "A sinistra"),
        ["Rechts"] = ("Right", "A destra"),
        ["Schriftgröße:"] = ("Font size:", "Dimensione carattere:"),
        ["Anzeigedauer (ms):"] = ("Display time (ms):", "Durata visualizzazione (ms):"),
        ["Deckkraft (%):"] = ("Opacity (%):", "Opacità (%):"),
        ["Monitore für die Einblendung (nichts markiert = alle):"] =
            ("Monitors for the banner (none checked = all):", "Monitor per il banner (nessuno = tutti):"),
        ["Einblendung testen"] = ("Test banner", "Prova banner"),
        ["Jemand"] = ("Someone", "Qualcuno"),

        // --- Signalton ---
        ["Bei Erkennung zusätzlich einen Ton abspielen"] = ("Also play a sound on detection", "Riproduci anche un suono al rilevamento"),
        ["Ton:"] = ("Sound:", "Suono:"),
        ["Lautstärke (%):"] = ("Volume (%):", "Volume (%):"),
        ["Ausgabegerät:"] = ("Output device:", "Dispositivo di uscita:"),
        ["Ton testen"] = ("Test sound", "Prova suono"),
        ["Auswahl aus %WINDIR%\\Media. Der Test nutzt die aktuell eingestellten Werte, ohne zu speichern."] =
            ("Selection from %WINDIR%\\Media. The test uses the current values without saving.",
             "Selezione da %WINDIR%\\Media. Il test usa i valori correnti senza salvare."),
        ["Standard (Standardgerät)"] = ("Default (default device)", "Predefinito (dispositivo predefinito)"),

        // --- Tastenkürzel ---
        ["Format z. B. Ctrl+Alt+G, Shift+F9 …"] = ("Format e.g. Ctrl+Alt+G, Shift+F9 …", "Formato es. Ctrl+Alt+G, Shift+F9 …"),
        ["Umschalten Ruhe/Gespräch:"] = ("Toggle quiet/conversation:", "Alterna silenzioso/conversazione:"),
        ["In Ruhe-Modus:"] = ("To quiet mode:", "Alla modalità silenziosa:"),
        ["In Gesprächs-Modus:"] = ("To conversation mode:", "Alla modalità conversazione:"),
        ["Erkennung an/aus:"] = ("Toggle detection on/off:", "Attiva/disattiva rilevamento:"),

        // --- Sonstiges ---
        ["Sprache:"] = ("Language:", "Lingua:"),
        ["Beim Start im Gesprächs-Modus starten (erster Ruhe-Modus muss aktiv gewählt werden)"] =
            ("Start in conversation mode (first quiet mode must be chosen manually)",
             "Avvia in modalità conversazione (la prima modalità silenziosa va scelta manualmente)"),
        ["Automatisch zurück in den Ruhe-Modus, wenn ich eine Zeit lang nichts sage"] =
            ("Automatically return to quiet mode when I'm silent for a while",
             "Torna automaticamente al silenzioso quando resto in silenzio per un po'"),
        ["Zeit ohne eigene Wortmeldung (Sekunden):"] = ("Time without me speaking (seconds):", "Tempo senza che io parli (secondi):"),
        ["Auto-Rückkehr auch bei manueller Aktivierung (Shortcut/Menü)"] =
            ("Auto-return also on manual activation (shortcut/menu)", "Ritorno automatico anche con attivazione manuale (scorciatoia/menu)"),
        ["Mit Windows starten"] = ("Start with Windows", "Avvia con Windows"),
        ["Beim Start auf Updates prüfen"] = ("Check for updates at startup", "Cerca aggiornamenti all'avvio"),
        ["Updates automatisch im Hintergrund installieren (ohne Nachfrage)"] =
            ("Install updates automatically in the background (no prompt)",
             "Installa gli aggiornamenti automaticamente in background (senza chiedere)"),
        ["Update wird im Hintergrund installiert …"] =
            ("Installing update in the background …", "Installazione dell'aggiornamento in background …"),
        ["Installation nach %LOCALAPPDATA%\\Programs anbieten (bei portablem Start)"] =
            ("Offer installing to %LOCALAPPDATA%\\Programs (when started portable)",
             "Proponi l'installazione in %LOCALAPPDATA%\\Programs (quando avviata in modo portatile)"),
        ["Soll die App nach {0} installiert werden? Von dort aktualisiert sie sich zuverlässig selbst, erhält einen Startmenü-Eintrag und funktioniert unabhängig von der heruntergeladenen Datei. Sie startet danach automatisch aus dem neuen Ordner."] =
            ("Install the app to {0}? From there it updates itself reliably, gets a Start menu entry and no longer depends on the downloaded file. It will restart from the new folder automatically.",
             "Installare l'app in {0}? Da lì si aggiorna in modo affidabile, ottiene una voce nel menu Start e non dipende più dal file scaricato. Si riavvierà automaticamente dalla nuova cartella."),
        ["Nicht mehr fragen"] = ("Don't ask again", "Non chiedere più"),
        ["Jetzt installieren"] = ("Install now", "Installa ora"),
        ["Installation fehlgeschlagen: {0}"] = ("Installation failed: {0}", "Installazione non riuscita: {0}"),
        ["Auf Updates prüfen …"] = ("Check for updates …", "Cerca aggiornamenti …"),
        ["Eine neue Version {0} ist verfügbar (installiert: {1}). Jetzt aktualisieren? Die Anwendung startet danach automatisch neu."] =
            ("A new version {0} is available (installed: {1}). Update now? The application will restart automatically afterwards.",
             "È disponibile una nuova versione {0} (installata: {1}). Aggiornare ora? L'applicazione si riavvierà automaticamente."),
        ["Jetzt aktualisieren"] = ("Update now", "Aggiorna ora"),
        ["Später"] = ("Later", "Più tardi"),
        ["Du verwendest bereits die neueste Version ({0})."] =
            ("You are already using the latest version ({0}).", "Stai già usando la versione più recente ({0})."),
        ["Update fehlgeschlagen: {0}"] = ("Update failed: {0}", "Aggiornamento non riuscito: {0}"),
        ["Update wird heruntergeladen …"] = ("Downloading update …", "Download dell'aggiornamento in corso …"),
        ["Der Programmordner ist nicht beschreibbar – verschiebe die App z. B. nach %LOCALAPPDATA%\\Programs, damit sie sich selbst aktualisieren kann."] =
            ("The application folder is not writable – move the app e.g. to %LOCALAPPDATA%\\Programs so it can update itself.",
             "La cartella dell'applicazione non è scrivibile – sposta l'app ad es. in %LOCALAPPDATA%\\Programs perché possa aggiornarsi da sola."),
        ["Debug-Log schreiben (%APPDATA%\\TeamsMentionNotificationCenter\\log.txt)"] =
            ("Write debug log (%APPDATA%\\TeamsMentionNotificationCenter\\log.txt)", "Scrivi log di debug (%APPDATA%\\TeamsMentionNotificationCenter\\log.txt)"),
        ["Poll-Intervall (ms):"] = ("Poll interval (ms):", "Intervallo di polling (ms):"),

        // --- Info ---
        ["Version {0}"] = ("Version {0}", "Versione {0}"),
        ["Funktionen"] = ("Features", "Funzioni"),
        ["Voraussetzungen"] = ("Requirements", "Requisiti"),
        ["Datenschutz"] = ("Privacy", "Privacy"),
        ["Entwickler"] = ("Developer", "Sviluppatore"),
        ["Technik: "] = ("Technology: ", "Tecnologia: "),
        ["Alle Einstellungen auf Standard zurücksetzen"] = ("Reset all settings to defaults", "Ripristina tutte le impostazioni"),
        ["Überwacht das Live-Transkript in Microsoft Teams und schlägt Alarm, sobald einer deiner hinterlegten Namen/Begriffe gesprochen wird – auch über mehrere parallele Calls hinweg. So kannst du Teams leise stellen und ungestört (z. B. mit Musik) arbeiten, ohne ständig auf das Untertitel-Fenster schauen zu müssen."] =
            ("Watches the live transcript in Microsoft Teams and alerts you as soon as one of your configured names/terms is spoken – even across several parallel calls. This lets you keep Teams quiet and work undisturbed (e.g. with music) without constantly watching the captions window.",
             "Monitora la trascrizione in tempo reale di Microsoft Teams e ti avvisa non appena viene pronunciato uno dei nomi/termini configurati – anche su più chiamate in parallelo. Così puoi tenere Teams a basso volume e lavorare indisturbato (es. con la musica) senza dover guardare di continuo la finestra dei sottotitoli."),
        ["• Erkennt deine Namen/Begriffe im Teams-Live-Transkript (per UI Automation) – auch über mehrere parallele Calls hinweg."] =
            ("• Detects your names/terms in the Teams live transcript (via UI Automation) – even across several parallel calls.",
             "• Rileva i tuoi nomi/termini nella trascrizione live di Teams (tramite UI Automation) – anche su più chiamate in parallelo."),
        ["• Roter, konfigurierbarer Bildschirm-Glow bei Nennung; optional zusätzlich ein Signalton (Lautstärke und Ausgabegerät wählbar)."] =
            ("• Configurable screen glow on a mention; optionally an alert sound too (volume and output device selectable).",
             "• Bordo dello schermo configurabile alla menzione; opzionalmente anche un suono di avviso (volume e dispositivo selezionabili)."),
        ["• Einblendung, wer dich gerufen hat – Text, Position, Größe, Farbe, Dauer, Deckkraft und Monitore frei einstellbar."] =
            ("• On-screen banner showing who called you – text, position, size, colour, duration, opacity and monitors fully configurable.",
             "• Banner a schermo che mostra chi ti ha chiamato – testo, posizione, dimensione, colore, durata, opacità e monitor configurabili."),
        ["• Auf Wunsch automatisch Teams lauter stellen und Musik (z. B. Spotify) pausieren; per Shortcut oder Tray zurück in den Ruhe-Modus."] =
            ("• Optionally raise Teams volume and pause music (e.g. Spotify) automatically; back to quiet mode via shortcut or tray.",
             "• Se vuoi, alza automaticamente il volume di Teams e mette in pausa la musica (es. Spotify); ritorno al silenzioso tramite scorciatoia o tray."),
        ["• Ruhe-/Gesprächs-Modus mit globalen Tastenkürzeln, wählbarem Start-Modus und automatischer Rückkehr nach eigener Redepause."] =
            ("• Quiet/conversation mode with global shortcuts, a selectable start mode and automatic return after you stop speaking.",
             "• Modalità silenzioso/conversazione con scorciatoie globali, modalità di avvio selezionabile e ritorno automatico dopo che smetti di parlare."),
        ["• Glow je Monitor wählbar; Erkennung sprachunabhängig; alles über diese Oberfläche einstellbar (kein fest hinterlegter Name)."] =
            ("• Glow selectable per monitor; language-independent detection; everything configurable via this UI (no hard-coded name).",
             "• Bordo selezionabile per monitor; rilevamento indipendente dalla lingua; tutto configurabile da questa interfaccia (nessun nome fisso)."),
        ["Windows 10/11, neues Microsoft Teams, im Call aktivierte Live-Untertitel (idealerweise als eigenes Fenster ausgekoppelt)."] =
            ("Windows 10/11, new Microsoft Teams, live captions enabled in the call (ideally popped out as a separate window).",
             "Windows 10/11, nuovo Microsoft Teams, sottotitoli live attivati nella chiamata (idealmente in una finestra separata)."),
        ["Das Transkript wird ausschließlich lokal und nur im Arbeitsspeicher verarbeitet – es wird nichts gespeichert, geloggt (außer optional das Debug-Log) oder übertragen. Es wird ausschließlich nach den konfigurierten Begriffen gesucht."] =
            ("The transcript is processed exclusively locally and only in memory – nothing is stored, logged (except the optional debug log) or transmitted. Only the configured terms are searched for.",
             "La trascrizione viene elaborata esclusivamente in locale e solo in memoria – non viene salvato, registrato (tranne il log di debug opzionale) o trasmesso nulla. Vengono cercati solo i termini configurati."),
    };
}
