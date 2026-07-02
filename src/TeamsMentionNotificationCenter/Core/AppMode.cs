namespace TeamsMentionNotificationCenter.Core;

/// <summary>Betriebsmodus der App.</summary>
public enum AppMode
{
    /// <summary>Arbeiten/Musik: Teams leise/stumm, Musik läuft.</summary>
    Quiet,
    /// <summary>Gespräch: Teams laut, Musik pausiert.</summary>
    Conversation
}
