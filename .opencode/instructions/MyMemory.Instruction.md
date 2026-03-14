# My Memory Context

**Stand:** 2026-03-14 00:49

## Fortschritt

**Aktueller Stand laufender Aufgaben:**

- Der Kick-off für `Hekiris` ist als erste lauffähige .NET-10-Console-App mit xUnit-Testprojekt umgesetzt.

**Risiken:**

- Der aktuell vorgegebene OpenCode-Endpunkt nutzt HTTP im LAN; die App warnt deshalb bei `check` und `start`, solange kein Passwort gesetzt ist.

- Die Projektordner wurden auf Wunsch direkt ins Repo-Root verschoben; alte Referenzen auf `src/` und `tests/` dürfen nicht wieder eingeführt werden.

- Der Start-/Stop- und Offline-Monitor ist per Code umgesetzt, aber ein echtes langlaufendes Telegram-Ende-zu-Ende wurde in dieser Session nicht manuell beobachtet.

- Die produktive Konfiguration liegt jetzt außerhalb des Repos unter `C:\Users\attila\.config\Hekiris\config.json`; Repo-Änderungen an `config.template.json` wirken erst nach Übernahme in diese Datei.

- Der Chat unterstützt jetzt neben `/cN` auch `/cNs` zum gezielten Stop eines laufenden konfigurierten Kommandos; `/ss` zeigt kompakte Laufzeitstates statt der Konfiguration.

- Scheduler- und Recovery-Verhalten für `commands[].timeLoop` sind per Build und Tests abgesichert, aber noch nicht als echter Langlauftest über mehrere Intervalle in Telegram beobachtet.

**Offene Fragen:**

**Nächste Schritte:**

- [X] `OpenCodeSessionId` in `C:\Users\attila\.config\Hekiris\config.json` auf die bestehende Session `ses_317f98079ffewZTBZBlX9YR4On` setzen.

- [X] `Hekiris check` erneut ausführen.

- [X] Einen direkten OpenCode-Ping mit der Session `ses_317f98079ffewZTBZBlX9YR4On` gegen die API verifizieren.

- [ ] Optional `Hekiris start` mit Telegram-Ende-zu-Ende testen.

- [ ] Optional das Offline-/Recovery-Verhalten gegen einen bewusst gestoppten OpenCode-Server manuell beobachten.

- [ ] Optional die automatische `timeLoop`-Ausführung über mehrere Intervalle im echten Telegram-Betrieb beobachten.

## Entscheidungen

### Öffentliche Naming-Richtung weg von `Bridge` und `Telegram` (2026-03-14)

**Entscheidung:**

- Die öffentliche Produktbenennung soll nicht die interne Bridge-Technik oder den Drittkanal `Telegram` betonen, sondern den Nutzen als externe Mensch-Schnittstelle für OpenCode über etablierte Kanäle plus Scheduler- und Schnellkommando-Funktionen; als aktuell bevorzugter Kandidat wurde `Hekiris` gewählt.

**Begründung:**

- Der User hat klargestellt, dass `Bridge` nur ein technisches Element ist und der eigentliche Mehrwert in der Nutzung bestehender Drittkanäle, Intervall-Prompts und hinterlegten Schnellkommandos liegt; zusätzlich ist `Telegram` im öffentlichen Produktnamen marken- und term-seitig unattraktiv.

**Konsequenz:**

- Künftige Naming-, Branding- und Rename-Vorschläge sollen kanaloffen, produktnutzennah und mythologisch-komponiert gedacht werden; `Hekiris` ist aktuell der Favorit und muss vor finaler Festlegung nur noch gegen Register, Handles und Kernmarkt-Domains vertieft geprüft werden.

- Eine erste vertiefte Vorprüfung ergab für `Hekiris` aktuell keine exakten Treffer in USPTO, TMview und WIPO sowie keine sichtbaren Repo-, Domain- oder Paketkollisionen; als Restvorsicht bleibt ein ähnlicher Teiltreffer `HEKIRISTONS` in TMview/USPTO außerhalb des geplanten Produktkontexts.

**Verworfen:**

- Eine öffentliche Hauptmarke mit Fokus auf `Telegram`, `Bridge`, `Relay` oder rein transporttechnischer Semantik.

### Externe Telegram-Secrets zur Laufzeit laden (2026-03-13)

**Entscheidung:**

- Das Telegram-Token bleibt außerhalb des Repos in einer externen Secret-Datei; die App lädt es über `Telegram.SecretSourcePath` zur Laufzeit nach.

**Begründung:**

- Der Arbeitsauftrag verlangt die Übernahme der vorhandenen Telegram-Secret-Datei, gleichzeitig dürfen Secrets nicht ins Repo eingecheckt oder unmaskiert ausgegeben werden.

**Konsequenz:**

- `Hekiris config show` maskiert `BotToken` und `OpenCode.Password`; die produktive Datei `C:\Users\attila\.config\Hekiris\config.json` enthält keinen Klartext-Token, sondern nur den Pfad zur Secret-Datei.

**Verworfen:**

- Das Token direkt in eine versionierte App-Config zu kopieren.

### Serielle Verarbeitung pro Chat über Chat-Queues (2026-03-13)

**Entscheidung:**

- Nachrichten werden je Telegram-Chat über eine eigene Queue seriell verarbeitet; beim Stop werden aktive Requests abgebrochen und wartende Requests abgewiesen.

**Begründung:**

- Der Auftrag verbietet parallele OpenCode-Anfragen pro Chat und verlangt ein verständliches Verhalten bei CTRL+C.

**Konsequenz:**

- Die Logik liegt in `Hekiris.Console/Processing/ChatRequestQueue.cs` und wird durch xUnit-Tests abgesichert.

**Verworfen:**

- Parallele Fire-and-Forget-Verarbeitung mit bloßen Semaphoren ohne kontrollierten Shutdown.

### Repo-Struktur direkt im Root (2026-03-13)

**Entscheidung:**

- Die Lösung ist als `Hekiris.slnx` mit `Hekiris.Console` und `Hekiris.Console.Tests` direkt im Repo-Root aufgebaut.

**Begründung:**

- Das Repo war für .NET leer; die Trennung in Produktionscode und xUnit-Tests bleibt erhalten, aber die Projektordner liegen auf Wunsch direkt im Root statt unter `src/` und `tests/`.

**Konsequenz:**

- Gemeinsame Versionen stehen in `Directory.Build.props`, die EXE heißt `Hekiris`, und `README.md` dokumentiert Setup und Nutzung.

**Verworfen:**

- Eine Ein-Projekt-Lösung ohne separates Testprojekt sowie eine zusätzliche `src/`- und `tests/`-Schachtelung.

### Statusmeldungen und Tages-CSV-Logs (2026-03-13)

**Entscheidung:**

- Die Bridge sendet Statusmeldungen für Start, geordnetes Beenden und OpenCode-Erreichbarkeitswechsel an die konfigurierten Telegram-Chats und schreibt parallel ein tägliches CSV-Log im EXE-Ordner.

**Begründung:**

- Der User wollte sichtbare Laufzeitmeldungen in Telegram und ein ausführliches, aber inhaltssicheres Betriebslog ohne Chattexte oder Secrets.

**Konsequenz:**

- `Hekiris.Console/Application/OpenCodeAvailabilityTracker.cs` steuert Zustandswechsel, und `Hekiris.Console/ConsoleOutput/CsvBridgeLogger.cs` schreibt `yyyy-MM-dd-Hekiris.csv` mit maximal 10 Tagen Aufbewahrung.

- Die CSV-Rotation orientiert sich am Tagesdatum im Dateinamen und löscht Logs, die älter als 10 Kalendertage sind.

**Verworfen:**

- Reines Console-Logging ohne Telegram-Statusmeldungen sowie Logeinträge mit Chatnachricht-Inhalten.

### Feste Pfade für Konfiguration und Logs (2026-03-13)

**Entscheidung:**

- Die produktive Konfiguration liegt fest unter `C:\Users\attila\.config\Hekiris\config.json`, und die CSV-Logs liegen fest unter `C:\Users\attila\.logs\Hekiris\`.

**Begründung:**

- Der User wollte die Betriebsartefakte außerhalb des Repo-Outputs an stabilen Benutzerpfaden haben.

**Konsequenz:**

- `Hekiris.Console/Configuration/BridgePaths.cs` liefert die festen Zielpfade, `AppConfigurationLoader` lädt nur noch die feste Config-Datei, und `config.template.json` dient im Repo nur noch als Vorlage.

**Verworfen:**

- Eine Konfiguration im EXE-Ordner oder Logs neben `Hekiris.exe`.

### Chat-Kommandos aus Konfigurationssatz (2026-03-13)

**Entscheidung:**

- Die Bridge behandelt `/help`, `/stop`, `/ss`, `/sc` intern und führt konfigurierte Kommandos `/c1`, `/c2`, ... über den `Commands`-Block der Config aus.

**Begründung:**

- Der User wollte feste, wiederverwendbare Chat-Kommandos mit eigenem Prompt, eigener Session und eigenem Modell.

**Konsequenz:**

- `Hekiris.Console/Application/BridgeChatCommandParser.cs` parst die Befehle, und `OpenCodeClient` kann pro Kommando ein Modell senden; fehlt ein Provider im Modellstring, wird `openai` angenommen.

**Verworfen:**

- Frei eingegebene Prompt-Fragmente hinter `/c1` sowie eine separate Kommandoverwaltung außerhalb der JSON-Konfiguration.

### Status- und Stop-Kommandos für konfigurierten Command-Lauf (2026-03-13)

**Entscheidung:**

- `/ss` zeigt nur noch Laufzeitstatus der Bridge, der Grund-Session und aller konfigurierten Kommandos, und `/cNs` stoppt gezielt ein laufendes Kommando.

**Begründung:**

- Der User wollte keinen Konfigurationsdump in `/ss`, sondern operative Zustände wie `frei`, `läuft` oder `wartet`, plus eine direkte Stop-Möglichkeit für Command-Sessions wie im UI.

**Konsequenz:**

- `Hekiris.Console/Processing/ChatRequestQueue.cs` verwaltet jetzt Pending-/Running-Status je Grund-Session und Kommando; Antworten aus Command-Sessions werden in Telegram mit `/cN <Titel>` gekennzeichnet.

**Verworfen:**

- Ein globales `/stop` für einzelne Kommandos sowie eine Statusausgabe mit kompletter maskierter Konfiguration.

### CamelCase-Konfiguration und feste Scheduler-Loops (2026-03-13)

**Entscheidung:**

- Die feste Konfiguration unter `C:\Users\attila\.config\Hekiris\config.json` wird konsequent in `camelCase` geführt, und `commands[].timeLoop` steuert optionale automatische Command-Läufe mit persistiertem `lastRun`.

**Begründung:**

- Der User wollte ein einheitliches JSON-Format und automatische Command-Ausführung nach Intervall, auch nach Start oder OpenCode-Recovery.

**Konsequenz:**

- `Hekiris.Console/config.template.json` ist auf `camelCase` umgestellt; `Hekiris.Console/Application/CommandTimeLoopScheduler.cs` bewertet Fälligkeit, `Hekiris.Console/Configuration/CommandTimeLoopStateStore.cs` persistiert `lastRun`, und `BridgeApplication` plant fällige Commands bei Start, nach Wiederverbindung und zyklisch ein.

**Verworfen:**

- Gemischte JSON-Schreibweisen sowie rein flüchtige Scheduler-Zustände ohne Rückschreiben in die feste Config.

### Erweiterter `/ss`-Status und Stopmeldung vor Cancel (2026-03-13)

**Entscheidung:**

- `/ss` zeigt pro Kommando zusätzlich Loop-Status, Intervall und `lastRun`, und bei `CTRL+C` wird die Telegram-Stopmeldung vor dem Cancel des Polling-Tokens gesendet.

**Begründung:**

- Der User wollte in `/ss` die relevanten Loop-Daten sehen und sicherstellen, dass Chats beim manuellen Stop zuverlässig noch benachrichtigt werden.

**Konsequenz:**

- `Hekiris.Console/Application/BridgeApplication.cs` ergänzt die Statusausgabe um `Loop an/aus`, `Intervall` und `LastRun`; außerdem wird die Beenden-Meldung jetzt vor dem Cancel ausgelöst.

**Verworfen:**

- Ein reiner Laufzeitstatus ohne Loop-Metadaten sowie ein Shutdown-Ablauf, bei dem die Telegram-Meldung erst nach dem Cancel versucht wird.

## User-Profil

### Entscheidungsstil

- [6] Reduziert breite Themen aktiv auf den kleinsten entscheidbaren Ausschnitt bei Standard-Aufgaben im Projektkontext ohne widersprechende aktuelle Anweisung; die KI soll Antworten und Vorschläge auf den kleinsten sinnvoll entscheidbaren Scope zuschneiden.

- [6] Korrigiert Annahmen, Zahlen und Fehlfokus direkt und ohne Umwege bei Klärungen, Reviews und Planungsfragen; die KI soll Präzision vor diplomatischer Umschreibung priorisieren.

- [6] Erwartet konkrete Handlungsempfehlungen mit klarer Begründung statt bloßer Analyse bei Strategie-, Review- und Umsetzungsfragen; die KI soll einen klaren Vorschlag mit Begründung geben.

### Kommunikation

- [6] Formuliert Aufgaben direktiv und auftragsorientiert bei normalen Arbeitsaufträgen im Projektkontext; die KI soll knapp, direkt und umsetzungsorientiert antworten.

- [6] Nutzt oft knappe Steuer- und Statussignale statt langer Rückmeldungen bei laufender Zusammenarbeit; die KI soll Bestätigungen und Statusmeldungen kurz halten.

- [6] Markiert klar, wenn die Antwort am eigentlichen Problem vorbeigeht bei Korrekturen und Nachschärfungen; die KI soll Fokusabweichungen schnell korrigieren und nicht verteidigen.

- [3] Gibt bei Tests oder formatkritischen Aufgaben sehr präzise Ausgabevorgaben bei prüfbaren oder formatgebundenen Ergebnissen; die KI soll Ausgabeform und Struktur genau einhalten.

- [4] Korrigiert Konfigurationsformate und sichtbare User-Texte direkt auf die gewünschte Endform bei JSON- und Chat-Interfaces; die KI soll Namenskonventionen wie `camelCase` und gewünschte Befehlstexte konsequent durchziehen.

### Prioritäten

- [6] Praktischer Nutzen und Umsetzbarkeit sind wichtiger als Vollständigkeit oder Theorie bei Lösungs- und Entscheidungsvorschlägen; die KI soll praktikable Umsetzungen priorisieren.

- [4] Erwartet sichtbare Konsistenz über das gesamte Repo, nicht nur punktuelle Korrekturen bei Änderungen mit Wiederholungsmustern; die KI soll betroffene Stellen repo-weit mitdenken.

- [6] Will, dass Regeln, Begriffe und dauerhaft nützliches Wissen in Repo-Artefakten landen bei verallgemeinerbaren Erkenntnissen; die KI soll stabiles Wissen in geeignete Repo-Dateien überführen.

### Autonomie

- [6] Erwartet hohe Eigenständigkeit des Agenten, wenn Ziel und Richtung klar sind bei normalen Aufgaben ohne besondere Freigabeanforderung; die KI soll selbständig arbeiten und nur bei echten Blockern nachfragen.

- [6] Setzt bei riskanten oder unerwünschten Eingriffen klare Grenzen bei Aktionen mit erhöhter Tragweite; die KI soll konservativ bleiben und bestehende Schutzregeln beachten.

- [6] Erwartet proaktive Folgeschritte, Statusbilder und sinnvolle nächste Aktionen bei mehrstufigen Aufgaben; die KI soll naheliegende nächste Schritte aktiv mitliefern.

### Arbeitsweise

- [6] Arbeitet iterativ und schärft Anforderungen mit neuen Daten und Korrekturen nach bei Entwürfen, Reviews und Umsetzungen; die KI soll schrittweise verbessern statt unnötig neu aufzusetzen.

- [6] Liefert lieber konkrete Artefakte, Pfade, Rohdaten und Beispiele als abstrakte Beschreibungen bei Arbeits- und Analyseaufträgen; die KI soll greifbare Ergebnisse bevorzugen.

- [6] Prüft Ergebnisse am realen Zustand von System, Repo oder UI statt an bloßen Behauptungen bei verifizierbaren Aufgaben; die KI soll nach Möglichkeit zustandsbasiert prüfen.

## Probleme & Lösungen

### Telegram-Bot-Token im URL-Pfad (2026-03-13)

**Problem:**

- `Hekiris check` scheiterte zuerst mit `The 'bot8234111616' scheme is not supported.`

**Ursache:**

- Der relative Telegram-API-Pfad begann direkt mit `bot<TOKEN>/...`; wegen des Doppelpunkts im Token interpretierte `HttpClient` das erste Segment als URI-Schema.

**Lösung:**

- Telegram-Methodenpfade beginnen jetzt mit `/bot<TOKEN>/...`, und ein Test in `Hekiris.Console.Tests/Telegram/TelegramBotClientTests.cs` sichert dieses Verhalten ab.

### Echte OpenCode-Session für den Hauptchat gesetzt (2026-03-13)

**Problem:**

- Die erste Konfiguration enthielt nur den Platzhalter `ses_REPLACE_ME`, dadurch blieb `Hekiris check` rot.

**Ursache:**

- Beim Kick-off war noch keine konkrete Session-ID festgelegt.

**Lösung:**

- `C:\Users\attila\.config\Hekiris\config.json` verwendet jetzt die Session `ses_317f98079ffewZTBZBlX9YR4On`; `Hekiris check` ist damit grün, und ein direkter API-Ping mit `ping` wurde erfolgreich gegen diese Session verifiziert.

## Nützliche Kommandos (kurz)

- `dotnet test Hekiris.slnx`: Build und xUnit-Tests ausführen.

- `dotnet run --project Hekiris.Console -- check`: Konfiguration, Telegram, OpenCode und Sessions prüfen.

- `dotnet run --project Hekiris.Console -- start`: Bridge starten.

- `C:\Users\attila\.logs\Hekiris\YYYY-MM-DD-Hekiris.csv`: Tageslog der Bridge.

- `C:\Users\attila\.config\Hekiris\config.json`: feste Runtime-Konfiguration inkl. `commands[].timeLoop`.

## Context

- **Projektzweck:** Telegram-Bot-Nachrichten aus freigegebenen Chats an eine feste OpenCode-Session weiterleiten und Antworten zurücksenden.

- **Leitdokumente:** `.Tasks/Kick-Off.md`, `AGENTS.md`, `README.md`

- **Arbeitsmodus:** Erst Repo-Kontext und Vorgaben prüfen, dann kleinste lauffähige .NET-Struktur umsetzen und mit echten Commands verifizieren.

- **Constraints:** .NET 10, Windows-Console-App, xUnit-Testprojekt, keine Secrets im Repo, keine Windows-Service-Infrastruktur.

## Referenzen

- **Dateien:** `Hekiris.Console/Application/BridgeApplication.cs`

- **Dateien:** `Hekiris.Console/Processing/ChatRequestQueue.cs`

- **Dateien:** `Hekiris.Console/config.template.json`

- **Dateien:** `Hekiris.Console/Application/BridgeChatCommandParser.cs`

- **Dateien:** `Hekiris.Console/ConsoleOutput/CsvBridgeLogger.cs`

- **Dateien:** `Hekiris.Console/Application/OpenCodeAvailabilityTracker.cs`

- **Dateien:** `Hekiris.Console/Application/CommandTimeLoopScheduler.cs`

- **Dateien:** `Hekiris.Console/Configuration/CommandTimeLoopStateStore.cs`

- **Dateien:** `Hekiris.Console.Tests/`

## Notes

- `dotnet new sln -n Hekiris` hat in dieser Umgebung eine `.slnx` erzeugt; die Solution-Datei heißt deshalb `Hekiris.slnx`.
