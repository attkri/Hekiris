# AGENTS

## Versionierung

- `<Version>` im csproj ist 3-stellig (`a.b.c`), NuGet-kompatibel.
- `AssemblyVersion` und `FileVersion` sind 4-stellig (`a.b.c.d`); `d` ist die interne Build-Nummer, beim Publish auf `0` zurückgesetzt.
- GitHub-Release-Tags und Release-Kanal basieren auf `a.b.c` (z. B. `v1.0.2`).
- `release-publish.yml` baut bei `v*.*.*`-Tags und extrahiert Release-Notes aus `Release-Notes.md`.
- Bei Änderungen an der Versionierungslogik muss dieser Abschnitt aktuell gehalten werden.

## Bridge-Konventionen

- Die produktive Konfiguration liegt fest unter `C:\Users\attila\.config\Hekiris\config.json`.
- Die Logdateien liegen fest unter `C:\Users\attila\.logs\Hekiris\`.
- Die JSON-Konfiguration verwendet großgeschriebene JSON-Keys wie `Telegram`, `OpenCode`, `AccessControl`, `Commands` und `TimeLoop`.
- `Commands[].TimeLoop.LastRun` wird von der Anwendung in die feste Konfigurationsdatei zurückgeschrieben.
