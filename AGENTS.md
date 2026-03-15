# AGENTS

## Versionierung

- Die öffentliche Release-Version ist 3-stellig (`a.b.c`). Sie wird für `Version`, GitHub-Tags und GitHub-Releases verwendet.
- `AssemblyVersion` und `FileVersion` sind 4-stellig (`a.b.c.d`); `d` ist nur für interne lokale Test- und Zwischenstände gedacht und wird für öffentliche Releases auf `0` gesetzt.
- `a` erhöht nur der User.
- `b` erhöht ausschließlich `@Code-Publisher`, wenn eine Funktionsänderung für Endanwender spürbar ist. Grundlage ist die zuletzt auf GitHub veröffentlichte öffentliche Version.
- `c` erhöht ausschließlich `@Code-Publisher`, wenn Fehler behoben oder kleine Änderungen veröffentlicht werden, die Endanwender nicht oder kaum spüren. Grundlage ist die zuletzt auf GitHub veröffentlichte öffentliche Version.
- Falls noch kein GitHub-Release existiert, gilt die im Repo vorhandene öffentliche Version als Startwert.
- GitHub-Release-Tags basieren nur auf `a.b.c` (z. B. `v1.0.2`). `d` gehört nie in Tag, Release-Titel oder Release-Notes.
- Die zentrale Release-Datei ist `Release-Notes.md`; sie wird nur durch `@Code-Publisher` geändert.
- `Release-Notes.md` verwendet genau diese Zustände:
  - Arbeitsstand vor Versionsfestlegung: `## [Unreleased]`
  - vorgemerkter Release-Kandidat vor erfolgreichem Publish: `## a.b.c [Unreleased]`
  - erfolgreich veröffentlichter Stand: `## a.b.c`
- Vor Tag und Publish muss der freizugebende Abschnitt als `## a.b.c [Unreleased]` existieren.
- Nach erfolgreichem Publish wird `## a.b.c [Unreleased]` zu `## a.b.c` finalisiert und direkt darüber wieder `## [Unreleased]` angelegt.
- Wenn ein Publish abbricht oder unvollständig bleibt, muss `## a.b.c [Unreleased]` erhalten bleiben, damit der Zwischenstand im Repo sichtbar ist.
- Release-Notes enthalten nur Endanwender-Nutzen, Bedienungsänderungen, wichtige Verhaltensänderungen und relevante Korrekturen; keine Klassen-, Methoden-, Datei- oder Architekturdetails.
- `README.md` enthält nur Endanwender-relevante Nutzung, Voraussetzungen, Bedienhinweise und rechtlich nötige Informationen.
- `README.md` enthält keine Release-Notes, keine Changelog-Zusammenfassungen und keine internen Publish- oder Versionsworkflow-Regeln.
- Bei Änderungen mit Benutzerwirkung muss vor Publish geprüft werden, ob `README.md` wegen geänderter Nutzung oder Bedienung angepasst werden muss.
- Bei Änderungen an der Versionierungs- oder Release-Logik muss dieser Abschnitt aktuell gehalten werden.

## Bridge-Konventionen

- Die produktive Konfiguration liegt fest unter `C:\Users\attila\.config\Hekiris\config.json`.
- Die Logdateien liegen fest unter `C:\Users\attila\.logs\Hekiris\`.
- Die JSON-Konfiguration verwendet großgeschriebene JSON-Keys wie `Telegram`, `OpenCode`, `AccessControl`, `Commands` und `TimeLoop`.
- `Commands[].TimeLoop.LastRun` wird von der Anwendung in die feste Konfigurationsdatei zurückgeschrieben.

## Integrations- und Diagnose-Regeln

- Wenn ein bekannter funktionierender Referenzaufruf vorliegt, muss dieser bei API- oder Integrationsproblemen zuerst 1:1 als Goldstandard gegen den aktuellen Codepfad gespiegelt und geprüft werden, bevor abstrahiert oder vereinfacht wird.
- Bei OpenCode-Diagnosen müssen immer drei Ebenen getrennt betrachtet und benannt werden: Konfiguration, tatsächlich gesendeter HTTP-Request und tatsächlich beobachteter Servereffekt.
- Änderungen an Request-Aufbau, Agent-, Session-, Directory- oder Fallback-Logik müssen gegen den Live-Server verifiziert werden, nicht nur gegen OpenAPI oder lokale Heuristik.
- Vor `dotnet build` oder manuellem Start von Hekiris müssen laufende `Hekiris`-Prozesse beendet werden, damit `bin/Debug` und `bin/Release` sicher den neuesten Stand enthalten.
- Wenn der User auf widersprüchliches Verhalten oder einen möglichen Kontextsprung hinweist, muss zuerst der aktuelle Repo- und Runtime-Zustand neu eingelesen und knapp als Ist-Bild zusammengefasst werden, bevor weitere Änderungen erfolgen.

## Anforderungsvalidierung

- `Hekiris-Anforderungen.md` ist für erwartetes Verhalten die verbindliche Quelle der Wahrheit.
- `Hekiris-Anforderungen.md` wird nicht durch Agenten geändert; fachliche Änderungen an dieser Datei kommen nur vom User.
- Während der Entwicklung prüft der umsetzende Agent betroffene Änderungen gegen die jeweils relevanten Abschnitte und Checkboxen in `Hekiris-Anforderungen.md` nur vorläufig zur eigenen Orientierung.
- Die verbindliche fachliche Bewertung, ob Anforderungen aus `Hekiris-Anforderungen.md` erfüllt sind, erfolgt ausschließlich durch den unabhängigen Prüfagenten `@Kritiker` oder direkt durch den User.
- Vor jedem Publish muss das gesamte freizugebende Verhalten vollständig gegen alle Checkboxen in `Hekiris-Anforderungen.md` unabhängig durch `@Kritiker` oder den User validiert werden.
- Wenn `@Kritiker` Abweichungen meldet, gelten die betroffenen Anforderungen als nicht erfüllt, bis die Befunde behoben und erneut unabhängig geprüft wurden.
- Prüfprotokolle unter `.logs/Anforderungsprüfung/YYYYMMDD-HHMM.md` sind optionale Nachweise oder technische Belege, aber niemals selbst die Freigabequelle.
- Klassische Builds und sonstige technische Verifikation bleiben erlaubt und nützlich, ersetzen aber weder die unabhängige fachliche Validierung gegen `Hekiris-Anforderungen.md` noch deren Befunde.
- Hekiris verwendet keine Unit-Tests. Die fachliche Qualitätssicherung erfolgt ausschließlich über `Hekiris-Anforderungen.md` und die unabhängige Prüfung durch `@Kritiker` oder den User.
