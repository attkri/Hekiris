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

## Integrations- und Diagnose-Regeln

- Wenn ein bekannter funktionierender Referenzaufruf vorliegt, muss dieser bei API- oder Integrationsproblemen zuerst 1:1 als Goldstandard gegen den aktuellen Codepfad gespiegelt und geprüft werden, bevor abstrahiert oder vereinfacht wird.
- Bei OpenCode-Diagnosen müssen immer drei Ebenen getrennt betrachtet und benannt werden: Konfiguration, tatsächlich gesendeter HTTP-Request und tatsächlich beobachteter Servereffekt.
- Änderungen an Request-Aufbau, Agent-, Session-, Directory- oder Fallback-Logik müssen gegen den Live-Server verifiziert werden, nicht nur gegen OpenAPI oder lokale Heuristik.
- Vor `dotnet build`, `dotnet test` oder manuellem Start von Hekiris müssen laufende `Hekiris`-Prozesse beendet werden, damit `bin/Debug` und `bin/Release` sicher den neuesten Stand enthalten.
- Wenn der User auf widersprüchliches Verhalten oder einen möglichen Kontextsprung hinweist, muss zuerst der aktuelle Repo- und Runtime-Zustand neu eingelesen und knapp als Ist-Bild zusammengefasst werden, bevor weitere Änderungen erfolgen.
