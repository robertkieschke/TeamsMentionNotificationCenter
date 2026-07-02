# IconGen

Kleines Dev-Werkzeug, das das Datei-Icon der App (`app.ico`) aus demselben Logo erzeugt, das
`src/TeamsMentionNotificationCenter/Core/Branding.cs` zeichnet – in mehreren Auflösungen
(16, 24, 32, 48, 64, 128, 256 px) als PNG-Frames in einer echten Multi-Resolution-`.ico`.

Nur nötig, wenn sich das Logo/die Marke ändert. Neu erzeugen (aus dem Repo-Wurzelverzeichnis):

```bash
dotnet run --project tools/IconGen -- src/TeamsMentionNotificationCenter/app.ico
```

Die erzeugte `app.ico` wird über `<ApplicationIcon>` in
`TeamsMentionNotificationCenter.csproj` fest ins `.exe` eingebettet (Icon im Explorer/Taskleiste).
