# TelegramOpenCodeBridge

## Überblick

`TelegramOpenCodeBridge` ist eine .NET-10-Console-App, die Telegram-Nachrichten aus freigegebenen Chats an einen laufenden OpenCode-Server weiterleitet.

Die Anwendung nutzt pro Telegram-Chat genau eine vorkonfigurierte OpenCode-Session, verarbeitet Textnachrichten seriell je Chat und sendet die Antwort zurück an Telegram.

Die Steuerung erfolgt über JSON-Konfiguration und die CLI-Kommandos `tocb start`, `tocb check`, `tocb config show` und `tocb help`.

Beim Start und beim geordneten Beenden sendet die Bridge Statusmeldungen an die konfigurierten Telegram-Chats. Zusätzlich überwacht sie den OpenCode-Server im Hintergrund und meldet Erreichbarkeitswechsel ebenfalls an Telegram.

Im Chat unterstützt die Bridge außerdem `/help`, `/stop`, `/ss`, `/sc`, konfigurierte Kommandos wie `/c1` und Stop-Kommandos wie `/c1s`.

Secrets bleiben außerhalb des Repos: Die App kann das Telegram-Token aus einer externen Secret-Datei einlesen und maskiert sensible Werte bei der Konfigurationsausgabe.

Die erste Version ist bewusst schlank gehalten: keine GUI, kein Windows-Service, kein Datenbankserver und keine Gruppenchat-Logik.

## Voraussetzungen

- Windows 11

- .NET SDK 10.x

- Ein laufender OpenCode-Server, standardmäßig `http://192.168.0.101:4096/`

- Eine vorhandene OpenCode-Session pro Telegram-Chat

- Eine lokale Telegram-Secret-Datei, z. B. unter `C:\Users\attila\.secrets\NovaKrickBot_Telegram.secrets.json`

## Projektstruktur

- `TelegramOpenCodeBridge.slnx` - Visual-Studio-Projektmappe

- `TelegramOpenCodeBridge.Console/` - Console-App

- `TelegramOpenCodeBridge.Console.Tests/` - xUnit-Tests

## Konfiguration

Die Bridge lädt ihre feste Konfiguration aus `C:\Users\attila\.config\TelegramOpenCodeBridge\config.json`. Eine Repo-Vorlage liegt in `TelegramOpenCodeBridge.Console/config.template.json`.

Die Konfiguration wird in `camelCase` geführt.

Der Eintrag `telegram.secretSourcePath` kann relativ zur festen Config-Datei gesetzt werden. Die Vorlage nutzt dafür `..\..\.secrets\NovaKrickBot_Telegram.secrets.json`.

Wichtige Bereiche:

- `telegram` - Bot-API, Polling und externer Secret-Pfad

- `openCode` - Basis-URL, optionaler Basic-Auth-Benutzer und Passwort

- `accessControl` - globale Freigaben für Telegram-Benutzer

- `runtime` - Queue-Größe, Retry-Verhalten und Startvalidierung

- `runtime.openCodeHealthCheckIntervalSeconds` - Intervall für die OpenCode-Erreichbarkeitsprüfung

- `chats` - Mapping `telegramChatId -> openCodeSessionId`

- `commands` - vorkonfigurierte Chat-Kommandos mit Titel, Session, Modell und Prompt sowie optionalem `timeLoop`

Beispiel für `Commands`:

```json
{
  "commands": [
    {
      "title": "Commando 1",
      "session": "ses_1234",
      "model": "gpt-4.1",
      "prompt": "Du bist ein hilfreicher Assistent. ...",
      "timeLoop": {
        "enabled": true,
        "interval": "5m",
        "lastRun": "2026-03-13T20:31:00"
      }
    },
    {
      "title": "Commando 2",
      "session": "ses_5678",
      "model": "openai/gpt-4o",
      "prompt": "Du bist ein Experte für Geschichte. ..."
    }
  ]
}
```

Wenn `model` keinen Provider enthält, verwendet die Bridge standardmäßig `openai`.

Ist `commands[].timeLoop.enabled=true`, setzt die Bridge das Kommando automatisch nach dem Intervall ab. `lastRun` wird dabei schon beim Einplanen aktualisiert, damit ein fehlgeschlagener Lauf nicht sofort erneut gestartet wird.

## Chatbefehle

- `/help` - zeigt die verfügbaren Bridge-Befehle an

- `/stop` - stoppt die Bridge kontrolliert

- `/ss` - sendet den aktuellen Bridge-Status inklusive maskierter Konfiguration

- `/sc` - listet alle konfigurierten Kommandos mit `/c1`, `/c2`, ... auf

- `/c1s` - stoppt ein gerade laufendes konfiguriertes Kommando gezielt

## Logging

Die Bridge legt pro Tag eine CSV-Datei im Format `yyyy-MM-dd-OCBridge.csv` unter `C:\Users\attila\.logs\TelegramOpenCodeBridge\` an, z. B. `C:\Users\attila\.logs\TelegramOpenCodeBridge\2026-03-13-OCBridge.csv`.

Die Logdatei enthält den Header `Timestamp; severity; Message`, rotiert automatisch auf maximal 10 Tage und schreibt keine Chat-Inhalte oder Secrets.

## Nutzung

```powershell
dotnet run --project .\TelegramOpenCodeBridge.Console -- help
dotnet run --project .\TelegramOpenCodeBridge.Console -- check
dotnet run --project .\TelegramOpenCodeBridge.Console -- config show
dotnet run --project .\TelegramOpenCodeBridge.Console -- start
```

## Tests

```powershell
dotnet test .\TelegramOpenCodeBridge.slnx
```
