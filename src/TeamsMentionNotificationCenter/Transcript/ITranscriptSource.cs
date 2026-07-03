namespace TeamsMentionNotificationCenter.Transcript;

/// <summary>Eine einzelne (neue oder aktualisierte) Untertitel-Zeile.</summary>
/// <param name="Speaker">Sprechername (kann leer sein).</param>
/// <param name="Text">Gesprochener Text.</param>
public readonly record struct CaptionLine(string Speaker, string Text);

public sealed class CaptionEventArgs : EventArgs
{
    public CaptionEventArgs(CaptionLine line) => Line = line;
    public CaptionLine Line { get; }
}

/// <summary>Meldet Statusänderungen der Quelle (für UI/Tray).</summary>
public sealed class TranscriptStatusEventArgs : EventArgs
{
    public TranscriptStatusEventArgs(bool available, string message)
    {
        Available = available;
        Message = message;
    }

    /// <summary>True, wenn gerade ein Untertitel-/Meeting-Fenster gefunden und lesbar ist.</summary>
    public bool Available { get; }
    public string Message { get; }
}

/// <summary>
/// Liefert fortlaufend neue Untertitel-Zeilen aus Teams.
/// Implementierungen laufen intern auf einem eigenen Thread; Events können daher
/// aus einem Hintergrund-Thread gefeuert werden.
/// </summary>
public interface ITranscriptSource : IDisposable
{
    event EventHandler<CaptionEventArgs>? CaptionReceived;
    event EventHandler<TranscriptStatusEventArgs>? StatusChanged;

    /// <summary>Feuert bei Sichtbarkeits-Wechsel des Teams-Anruf-Popups (true = eingehender Anruf klingelt).</summary>
    event EventHandler<bool>? IncomingCallVisibleChanged;

    void Start();
    void Stop();
}
