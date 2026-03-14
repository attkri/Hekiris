namespace Hekiris.Application;

public static class BridgeChatHelpText
{
    public static string GetText()
    {
        return """
/help => zeigt die verfügbaren Hekiris-Befehle an
/stop => stoppt Hekiris
/ss => zeigt den aktuellen Status von Hekiris an
/sc => zeigt die verfügbaren Befehle an
/cNs => stoppt das laufende Kommando N (siehe /ss)

Konfigurierte Kommandos rufst du mit /c1, /c2, /c3 usw. auf.
""";
    }
}
