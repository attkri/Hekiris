# TelegramOpenCodeBridge

## Überblick

`TelegramOpenCodeBridge` ist eine .NET-10-Console-App, die Telegram-Nachrichten aus freigegebenen Chats an einen laufenden OpenCode-Server weiterleitet.

Die Anwendung nutzt pro Telegram-Chat genau eine vorkonfigurierte OpenCode-Session, verarbeitet Textnachrichten seriell je Chat und sendet die Antwort zurück an Telegram.

Die Steuerung erfolgt über JSON-Konfiguration und die CLI-Kommandos `tocb start`, `tocb check`, `tocb config show` und `tocb help`.

Beim Start und beim geordneten Beenden sendet die Bridge Statusmeldungen an die konfigurierten Telegram-Chats. Zusätzlich überwacht sie den OpenCode-Server im Hintergrund und meldet Erreichbarkeitswechsel ebenfalls an Telegram.

Im Chat unterstützt die Bridge außerdem `/help`, `/stop`, `/ss`, `/sc` und konfigurierte Kommandos wie `/c1`.

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

Der Eintrag `Telegram.SecretSourcePath` kann relativ zur festen Config-Datei gesetzt werden. Die Vorlage nutzt dafür `..\..\.secrets\NovaKrickBot_Telegram.secrets.json`.

Wichtige Bereiche:

- `Telegram` - Bot-API, Polling und externer Secret-Pfad

- `OpenCode` - Basis-URL, optionaler Basic-Auth-Benutzer und Passwort

- `AccessControl` - globale Freigaben für Telegram-Benutzer

- `Runtime` - Queue-Größe, Retry-Verhalten und Startvalidierung

- `Runtime.OpenCodeHealthCheckIntervalSeconds` - Intervall für die OpenCode-Erreichbarkeitsprüfung

- `Chats` - Mapping `TelegramChatId -> OpenCodeSessionId`

- `Commands` - vorkonfigurierte Chat-Kommandos mit Titel, Session, Modell und Prompt

Beispiel für `Commands`:

```json
{
  "Commands": [
    {
      "Title": "Commando 1",
      "Session": "ses_1234",
      "Model": "gpt-4.1",
      "Prompt": "Du bist ein hilfreicher Assistent. ..."
    },
    {
      "Title": "Commando 2",
      "Session": "ses_5678",
      "Model": "openai/gpt-4o",
      "Prompt": "Du bist ein Experte für Geschichte. ..."
    }
  ]
}
```

Wenn `Model` keinen Provider enthält, verwendet die Bridge standardmäßig `openai`.

## Chatbefehle

- `/help` - zeigt die verfügbaren Bridge-Befehle an

- `/stop` - stoppt die Bridge kontrolliert

- `/ss` - sendet den aktuellen Bridge-Status inklusive maskierter Konfiguration

- `/sc` - listet alle konfigurierten Kommandos mit `/c1`, `/c2`, ... auf

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
