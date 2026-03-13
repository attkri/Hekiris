namespace TelegramOpenCodeBridge.Application;

public static class BridgeChatHelpText
{
    public static string GetText()
    {
        return """
/help => zeigt die verfügbaren Bridge-Befehle an
/stop => stoppt die Bridge
/ss => zeigt den aktuellen Status der Bridge an
/sc => zeigt die verfügbaren Befehle an
/cNs => stoppt das laufende Kommando N (siehe /ss)

Konfigurierte Kommandos rufst du mit /c1, /c2, /c3 usw. auf.
""";
    }
}
