# Hekiris

Hekiris = Hekate (Kreuzungen) + Iris (Übermittlung)

## Brand-Richtung

- One-liner: Hekiris bringt OpenCode in die Kanäle, die wir bereits nutzen.
- Gute Claim-Richtung: Der direkte Weg von Nachricht zu Aktion.
- Produktarchitektur, die ich empfehlen würde:

- Hekiris Core — Hauptprodukt
- Hekiris Chronos — Scheduler / Intervall-Prompts
- Hekiris Hermes — Schnellkommandos / Shortcuts
- Hekiris Argus — Health / Monitoring
- Hekiris Portals — Kanaladapter wie Telegram, WhatsApp, Voice

## Überblick

`Hekiris` ist eine .NET-10-Console-App, die Telegram-Nachrichten aus freigegebenen Chats an einen laufenden OpenCode-Server weiterleitet.

Die Anwendung nutzt pro Telegram-Chat genau eine vorkonfigurierte OpenCode-Session, verarbeitet Textnachrichten seriell je Chat und sendet die Antwort zurück an Telegram.

Die Steuerung erfolgt über JSON-Konfiguration und die CLI-Kommandos `Hekiris start`, `Hekiris check`, `Hekiris config show` und `Hekiris help`.

Beim Start und beim geordneten Beenden sendet Hekiris Statusmeldungen an die konfigurierten Telegram-Chats. Zusätzlich überwacht Hekiris den OpenCode-Server im Hintergrund und meldet Erreichbarkeitswechsel ebenfalls an Telegram.

Im Chat unterstützt Hekiris außerdem `/help`, `/stop`, `/ss`, `/sc`, konfigurierte Kommandos wie `/c1` und Stop-Kommandos wie `/c1s`.

Secrets bleiben außerhalb des Repos: Die App kann das Telegram-Token aus einer externen Secret-Datei einlesen und maskiert sensible Werte bei der Konfigurationsausgabe.

Die erste Version ist bewusst schlank gehalten: keine GUI, kein Windows-Service, kein Datenbankserver und keine Gruppenchat-Logik.

## Voraussetzungen

- Windows 11

- .NET SDK 10.x

- Ein laufender OpenCode-Server, standardmäßig `http://192.168.0.101:4096/`

- Eine vorhandene OpenCode-Session pro Telegram-Chat

- Eine lokale Telegram-Secret-Datei, z. B. unter `C:\Users\attila\.secrets\NovaKrickBot_Telegram.secrets.json`

## Projektstruktur

- `Hekiris.slnx` - Visual-Studio-Projektmappe

- `Hekiris.Console/` - Console-App

- `Hekiris.Console.Tests/` - xUnit-Tests

## Konfiguration

Hekiris lädt ihre feste Konfiguration aus `C:\Users\attila\.config\Hekiris\config.json`. Eine Repo-Vorlage liegt in `Hekiris.Console/config.template.json`.

Die Konfiguration wird mit großgeschriebenem Anfangsbuchstaben pro JSON-Key geführt, z. B. `Telegram`, `OpenCode`, `AccessControl`, `AllowedUserIds`.

Der Eintrag `Telegram.SecretSourcePath` kann relativ zur festen Config-Datei gesetzt werden. Die Vorlage nutzt dafür `..\..\.secrets\NovaKrickBot_Telegram.secrets.json`.

Wichtige Bereiche:

- `Telegram` - Bot-API, Polling und externer Secret-Pfad

- `OpenCode` - Basis-URL, optionaler Basic-Auth-Benutzer und Passwort

- `AccessControl` - globale Freigaben für Telegram-Benutzer über `AllowedUserIds` und `AllowedUsernames`

- `Runtime` - Queue-Größe, Retry-Verhalten und Startvalidierung

- `Runtime.OpenCodeHealthCheckIntervalSeconds` - Intervall für die OpenCode-Erreichbarkeitsprüfung

- `Chats` - Mapping `TelegramChatId -> OpenCodeSessionId` mit optionalen `AllowedUserIds` und `AllowedUsernames` je Chat

- `Commands` - vorkonfigurierte Chat-Kommandos mit Titel, Session, Modell und Prompt sowie optionalem `TimeLoop`

Beispiel für `Commands`:

```json
{
  "Commands": [
    {
      "Title": "Commando 1",
      "Session": "ses_1234",
      "Model": "gpt-4.1",
      "Prompt": "Du bist ein hilfreicher Assistent. ...",
      "TimeLoop": {
        "Enabled": true,
        "Interval": "5m",
        "LastRun": "2026-03-13T20:31:00"
      }
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

Wenn `Model` keinen Provider enthält, verwendet Hekiris standardmäßig `openai`.

Ist `Commands[].TimeLoop.Enabled=true`, setzt Hekiris das Kommando automatisch nach dem Intervall ab. `LastRun` wird dabei schon beim Einplanen aktualisiert, damit ein fehlgeschlagener Lauf nicht sofort erneut gestartet wird.

## Chatbefehle

- `/help` - zeigt die verfügbaren Hekiris-Befehle an

- `/stop` - stoppt Hekiris kontrolliert

- `/ss` - sendet den aktuellen Hekiris-Status inklusive Grund-Session, Command-Status sowie Loop-, Intervall- und LastRun-Infos

- `/sc` - listet alle konfigurierten Kommandos mit `/c1`, `/c2`, ... auf

- `/cNs` - stoppt ein gerade laufendes konfiguriertes Kommando gezielt

## Logging

Hekiris legt pro Tag eine CSV-Datei im Format `yyyy-MM-dd-Hekiris.csv` unter `C:\Users\attila\.logs\Hekiris\` an, z. B. `C:\Users\attila\.logs\Hekiris\2026-03-13-Hekiris.csv`.

Die Logdatei enthält den Header `Timestamp; severity; Message`, rotiert automatisch auf maximal 10 Tage und schreibt keine Chat-Inhalte oder Secrets.

Bei eingehenden Nachrichten protokolliert Hekiris zusätzlich `chatId`, `userId` und `username`, damit freigegebene Telegram-Nutzer sauber in die Konfiguration übernommen werden können. Nicht freigegebene Nachrichten werden still verworfen, nur geloggt und weder beantwortet noch an OpenCode weitergegeben.

## Nutzung

```powershell
dotnet run --project .\Hekiris.Console -- help
dotnet run --project .\Hekiris.Console -- check
dotnet run --project .\Hekiris.Console -- config show
dotnet run --project .\Hekiris.Console -- start
```

## Tests

```powershell
dotnet test .\Hekiris.slnx
```
