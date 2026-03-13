# TelegramOpenCodeBridge

## Überblick

`TelegramOpenCodeBridge` ist eine .NET-10-Console-App, die Telegram-Nachrichten aus freigegebenen Chats an einen laufenden OpenCode-Server weiterleitet.

Die Anwendung nutzt pro Telegram-Chat genau eine vorkonfigurierte OpenCode-Session, verarbeitet Textnachrichten seriell je Chat und sendet die Antwort zurück an Telegram.

Die Steuerung erfolgt über JSON-Konfiguration und die CLI-Kommandos `tocb start`, `tocb check`, `tocb config show` und `tocb help`.

Beim Start und beim geordneten Beenden sendet die Bridge Statusmeldungen an die konfigurierten Telegram-Chats. Zusätzlich überwacht sie den OpenCode-Server im Hintergrund und meldet Erreichbarkeitswechsel ebenfalls an Telegram.

Secrets bleiben außerhalb des Repos: Die App kann das Telegram-Token aus einer externen Secret-Datei einlesen und maskiert sensible Werte bei der Konfigurationsausgabe.

Die erste Version ist bewusst schlank gehalten: keine GUI, kein Windows-Service, kein Datenbankserver und keine Gruppenchat-Logik.

## Voraussetzungen

- Windows 11

- .NET SDK 10.x

- Ein laufender OpenCode-Server, standardmäßig `http://192.168.0.101:4096/`

- Eine vorhandene OpenCode-Session pro Telegram-Chat

- Eine lokale Telegram-Secret-Datei, z. B. `C:\Users\attila\.secrets\NovaKrickBot_Telegram.secrets.json`

## Projektstruktur

- `TelegramOpenCodeBridge.slnx` - Visual-Studio-Projektmappe

- `TelegramOpenCodeBridge.Console/` - Console-App

- `TelegramOpenCodeBridge.Console.Tests/` - xUnit-Tests

## Konfiguration

Die App lädt standardmäßig `TelegramOpenCodeBridge.Console/appsettings.json` aus dem Ausgabeverzeichnis. Optional kann eine lokale Override-Datei `appsettings.Local.json` im selben Verzeichnis liegen; diese ist per `.gitignore` ausgeschlossen.

Wichtige Bereiche:

- `Telegram` - Bot-API, Polling und externer Secret-Pfad

- `OpenCode` - Basis-URL, optionaler Basic-Auth-Benutzer und Passwort

- `AccessControl` - globale Freigaben für Telegram-Benutzer

- `Runtime` - Queue-Größe, Retry-Verhalten und Startvalidierung

- `Runtime.OpenCodeHealthCheckIntervalSeconds` - Intervall für die OpenCode-Erreichbarkeitsprüfung

- `Chats` - Mapping `TelegramChatId -> OpenCodeSessionId`

## Logging

Im EXE-Ordner legt die Bridge pro Tag eine CSV-Datei im Format `yyyy-MM-dd-OCBridge.csv` an, z. B. `TelegramOpenCodeBridge.Console/bin/Debug/net10.0/2026-03-13-OCBridge.csv`.

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
