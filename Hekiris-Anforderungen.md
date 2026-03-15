# Erwartungen, Anforderungen und Verhaltenmuster

Dies Dokument definiert die Quelle der Wahrheit der Erwartungen und Verhaltensmuster für das Projekt Hekiris.

## §1 Grundlagen

1. [ ] Code- und Ausgabesprache ist Englisch.
2. [ ] Alle Checkboxen in diesem Dokument sind Prüfpunkte die vor jedem Publishen von Hekiris erfüllt sein müssen.
3. [ ] Während der Entwicklung prüft der umsetzende Agent betroffene Anforderungen nur vorläufig zur eigenen Orientierung.
4. [ ] Die verbindliche fachliche Bewertung, ob eine Anforderung erfüllt ist, erfolgt ausschließlich durch einen unabhängigen Prüfagenten (`@Kritiker`) spätestens vor dem Publishing.

## §2 Technik

1. [ ] .NET 10
2. [ ] Windows 11
3. [ ] C#
4. [ ] Anbindung an Telegram Bot API
5. [ ] Anbindung an OpenCode API (`opencode serve`)
6. [ ] Konsolen-Anwendung (CLI)
7. [ ] Die Architektur soll pragmatisch modular aufgebaut sein. Fachliche Regeln, Ablaufsteuerung, Infrastrukturzugriffe und CLI-Einstieg sollen klar getrennt sein, ohne für ein kleines Console-Projekt unnötige Abstraktionen oder künstliche Schichten einzuführen:
       - [ ] `Host/CLI` enthält nur Einstieg, Argumentverarbeitung und Composition Root.
       - [ ] `Application` enthält Ablaufsteuerung und Anwendungslogik.
       - [ ] `Infrastructure` kapselt externe Systeme wie Telegram, OpenCode, Dateien und Logging.
       - [ ] Fachliche Kernlogik soll nicht unnötig an Infrastruktur oder CLI gekoppelt sein.
       - [ ] Die Trennung soll der Wartbarkeit dienen und nicht zu übermäßigem Boilerplate oder rein theoretischen Abstraktionen führen.

## §3 Dateien

folgende Dateien und Ordner müssen existieren:

1. [ ] `~/.config/Hekiris/config.json` -> enthält die Laufzeitkonfiguration
       1. [ ] Das folgende Beispiel zeigt die erwartete Struktur und die notwendigen Felder der Konfigurationsdatei.
       2. [ ] `accessControl` lässt nur einen Hauptbenutzer zu von dem Nachrichten akzeptiert und weitergegeben werden.
       3. [ ] Alle Felder sind Pflicht mit folgenden Ausnahmen:
              1. `session`, `agent` und `workingDirectory`, wenn diese fehlen, erben sie diese von der Basissession der `chat`-Konfiguration.
       4. [ ] Weitere Anmkerungen zu den Feldern:
              - Wenn `TimeLoop` fehlt, wird es als deaktiviert interpretiert, d.h. es ist ein Kommando das manuell ausgeführt werden muss.

    ```json
    {
      "Telegram": {
        "ApiBaseUrl": "https://api.telegram.org",
        "SecretSourcePath": "secrets.json",
        "BotToken": "",
        "PollingTimeoutSeconds": 20
      },
      "OpenCode": {
        "BaseUrl": "http://server:port/",
        "Username": "opencode",
        "Password": "",
        "RequestTimeoutSeconds": 300
      },
      "AccessControl": {
        "AllowedUserId": 9999999,
        "AllowedUsername": "username"
      },
      "Runtime": {
        "QueueCapacityPerChat": 20,
        "TelegramRetryDelaySeconds": 5,
        "OpenCodeHealthCheckIntervalSeconds": 30,
        "RejectMessagesWhenStopping": true,
        "SkipPendingUpdatesOnStart": true
      },
      "Chat": {
        "TelegramChatId": 9999999,
        "WorkingDirectory": "WorkingDirectory",
        "OpenCodeSessionId": "ses_999999999",
        "Agent": "Nova"
      },
      "Commands": [
        {
          "Title": "Ping Test",
          "Prompt": "ping",
          "TimeLoop": {
            "Enabled": true,
            "Interval": "1h",
            "LastRun": "2026-03-14T20:41:47"
          }
        },
        {
          "Title": "News Email",
          "WorkingDirectory": "WorkingDirectory",
          "Session": "ses_999999999",
          "Agent": "Agent-Name",
          "Prompt": "Erstelle die News f\u00FCr heute",
          "TimeLoop": {
            "Enabled": true,
            "Interval": "24h",
            "LastRun": "2026-03-14T12:16:35"
          }
        }
      ]
    }
    ```

2. [ ] `~/.logs/Hekiris/` -> enthält das Programmlogging
       1. [ ] Pro Tag eine Datei `YYYY-MM-DD-Hekiris.csv`.
       2. [ ] Maximal 10 Tage Log-Historie, danach werden alte Logs gelöscht.
       3. Zum Beispiel:

    ```text
    Timestamp; severity; Message
    2026-03-14 01:28_01; WARNING   ; OpenCode is accessed unencrypted via HTTP without a password on a remote host.
    2026-03-14 01:28_01; INFO      ; Telegram: OK (@novakrickbot)
    2026-03-14 01:28_01; INFO      ; OpenCode: OK (Version 1.2.20)
    2026-03-14 01:28_01; INFO      ; OpenCode-Session found: ses_360c5d862ffeAeAoXj6479Jo8U
    ```

3. [ ] `Hekiris.exe` -> die ausführbare Datei der Anwendung, die alle Funktionen enthält.

## §4 Namenskonventionen

1. [ ] __Repository:__ Hekiris
2. [ ] __Solution:__ Hekiris
3. [ ] __Projectname:__ Hekiris.Console
4. [ ] __EXE:__ Hekiris.exe

## §5 Console (CLI)

Mögliche Befehle:

1. [ ] `Hekiris start` Starten die Bereitschaft für den Empfang von Telegram-Nachrichten und die Verarbeitung von OpenCode-Aufträgen.
2. [ ] `Hekiris check` -> Prüft die Konfiguration, Telegram-Zugriff, OpenCode-Gesundheit und Session-Mappings und zeigt das Ergebnis an.
3. [ ] `Hekiris config show` -> Zeigt die Konfigurationsdatei an.
4. [ ] `Hekiris help` -> Zeigt die Hilfe an.

### 1. Hekiris start

Nach dem starten zeigt die Console folgende Informationen an:

1. [ ] Gibt Statusinformationen aus s. `Hekiris check`.
2. [ ] Zeigt das Hekiris gestartet ist am / um.
3. [ ] Zeigt die Anweisungen zum Stoppen an (CTRL+C).
4. [ ] Zeigt die Komunikation mit Telegram und OpenCode an.

__Beispiel:__

```text
PS> .\Hekiris.exe start

[2026-03-14 16:27:29] WARNING:
Warning: OpenCode is reached over unencrypted HTTP without a password on a remote host.

STATUS:
Telegram: OK (@novakrickbot)
OpenCode: OK (Version 1.2.20)
OpenCode session found: ses_3161d40e7ffelJD1NJn94m6z2t
OpenCode session found: ses_35a709b05ffeipvcPqFpwSkF56
OpenCode session found: ses_360c5d862ffeAeAoXj6479Jo8U

Hekiris started at 2026-03-14 16:27:29. Press CTRL+C to stop.

[2026-03-14 16:27:29] HEKIRIS:
Hekiris status message sent to chat 1700580252.

[2026-03-14 16:27:29] HEKIRIS:
TimeLoop LastRun for /c1 Ping Test updated to 2026-03-14 16:27:29.

[2026-03-14 16:27:29] USER:
Configured command /c1 Ping Test

[2026-03-14 16:27:29] HEKIRIS:
Configured command /c1 Ping Test sent to OpenCode.

[2026-03-14 16:27:29] HEKIRIS:
Command /c1 Ping Test was triggered automatically (at startup).

[2026-03-14 16:28:06] HEKIRIS:
Incoming message: chat 1700580252, userId 1700580252, username attkri.

[2026-03-14 16:28:07] HEKIRIS:
Status sent to chat 1700580252.

[2026-03-14 16:28:14] AGENT:
[/c1 Ping Test]

INSTR_OK_9f3c pong
```

### 2. Hekiris check

1. [ ] Statusmeldung Telegram-Zugriff
2. [ ] Statusmeldung OpenCode-Gesundheit
3. [ ] Statusmeldung Session-Mappings.
4. [ ] Er zeigt Erfolg, Warnungen oder Fehler bzgl. der Konfiguration.

```text
PS> .\Hekiris.exe check

[2026-03-14 18:22:44] WARNING:
Warning: OpenCode is reached over unencrypted HTTP without a password on a remote host.

STATUS:
Telegram: OK (@novakrickbot)
OpenCode: OK (Version 1.2.20)
OpenCode session found: ses_3161d40e7ffelJD1NJn94m6z2t
OpenCode session found: ses_35a709b05ffeipvcPqFpwSkF56
OpenCode session found: ses_360c5d862ffeAeAoXj6479Jo8U
```

### 3. Hekiris config show

1. [ ] Zeigt Pfad zur Konfigurationsdatei an.
2. [ ] Zeigt den Inhalt der Konfigurationsdatei an.

__Beispiel:__

```text
PS> .\Hekiris.exe config show

Source: ~/.config/Hekiris/config.json
{
  "Telegram": {
    "ApiBaseUrl": "https://api.telegram.org",
    "SecretSourcePath": "secrets.json",
    "BotToken": "",
...
<!-- Ausgabe entspricht der maskierten Konfiguration aus §3 -->
```

### 4. Hekiris help

Zeigt die Hilfe an.

1. [ ] Zeigt die Programversion an.
2. [ ] Zeigt mögliche Parameter mit Beschreibung an.

```text
PS> .\Hekiris.exe help

Hekiris v1.0.0

Usage:
  Hekiris start
  Hekiris check
  Hekiris config show
  Hekiris help

Commands:
  start       Starts Hekiris and begins receiving Telegram messages.
  check       Validates configuration, Telegram access, OpenCode health, and session mappings.
  config show Shows the loaded configuration with masked secrets.
  help        Shows this help.
```

### Hekiris stoppen

Entweder wird Hekiris in der Console über `CTRL + C` oder in Telegram über `/stop` gestoppt.

1. [ ] In der Console wird folgende Meldung angezeigt:

   ```text
   [2026-03-14 19:13:46] HEKIRIS:
   Shutdown requested. Running jobs are being stopped.
   
   [2026-03-14 19:13:46] HEKIRIS:
   Hekiris status message sent to chat 1700580252.
   ```

2. [ ] In Telegram wird folgende Meldung angezeigt:

   ```text
   Hekiris is shutting down. New messages are no longer accepted.
   ```

## §6 Telegram

Mögliche Befehle:

1. [ ] `/sc` => (Show Commands) zeigt die verfügbaren Befehle an.
2. [ ] `/ss` => (Show Status) zeigt den aktuellen Hekiris-Status an.
3. [ ] `/cNs` => ( Command Nr stop) stoppt den laufenden Befehl N (siehe /ss).
4. [ ] `/stop` => stoppt Hekiris.
5. [ ] `/help` => zeigt die verfügbaren Hekiris-Befehle an.

### I) Show Commands (/sc)

1. [ ] Zeigt die konfigurierten Kommandos mit. Nr., Name und Kommando-Kürzel an.
2. [ ] Zeigt die Anweisungen zum Ausführen der Kommandos an.
3. [ ] Zeigt ein Hinweis zu möglichen Abbrechen an.

__Beispiel:__

```text
Available commands:
1. Ping Test (/c1)
2. News Email (/c2)
3. News auf AKC (/c3)

Configured commands can be run with /c1, /c2, /c3, and so on.

/cNs stops a running command
```

### II) Show Status (/ss)

1. [ ] Zeigt die aktuelle Programmversion an.
2. [ ] Zeigt den Status der OpenCode-Verbindung an.
3. [ ] Zeigt Session-id der Basissession an mit der kommuniziert wird.
4. [ ] Zeigt das Arbeitsverzeichnis der Basissession an.
5. [ ] Zeigt welcher Agent benutzt wird.
6. [ ] Jeder Command wird als eigene Telegram-Nachricht gesendet.
       - sollten keine Kommandos konfiguriert sein, wird zu Kommandos nichts angezeigt.
7. [ ] Zeigt den Status jedes Kommandos an:
       - frei oder läuft
       - Session-id
       - Arbeitsverzeichnis
       - Agent
       - ob TimeLoop aktiv ist
       - Intervall
       - wann es zuletzt ausgeführt

```text
Hekiris v1.0.0

Status:

- OpenCode: reachable
- Base session: free (ses_3161d40e7ffelJD1NJn94m6z2t)
- Base working directory: C:\Users\attila\Projects\Krick.Hub
- Last used agent in base session: Nova
```

```text
Ping Test (/c1):

Status: free
OC Session: ses_3161d40e7ffelJD1NJn94m6z2t
WorkingDirectory: C:\Users\attila\Projects\Krick.Hub
Agent: Nova
Loop: on 
Interval: 1h
LastRun: 2026-03-14 18:41:44
```

```text
News Email (/c2):
...
```

### III) Stop Command (/cNs)

1. [ ] Stoppt den laufenden Befehl N, wenn er läuft und gibt eine Meldung aus.
2. [ ] Wenn Befehl N nicht läuft, gibt eine Meldung aus.
       `Command /cN is not running right now.`

### IV) Stop Hekiris (/stop)

1. [ ] Stoppt Hekiris und gibt eine Meldung aus.
       `Hekiris is shutting down. New messages are no longer accepted.`

### V) Help (/help)

1. [ ] Zeigt die verfügbaren Hekiris-Befehle an.

```text
/help => shows the available Hekiris commands
/stop => stops Hekiris
/ss => shows the current Hekiris status
/sc => shows the available commands
/cNs => stops running command N (see /ss)
```

## §7 Workflows

### I) Global

1. [ ] Für neue Versionen gibt es einen Veröffentlichungsablauf, der Tests ausführt, die Windows-Version baut, ein ZIP erstellt und daraus ein GitHub-Release macht.

2. [ ] Wenn der OpenCode-Server über REST-API kontaktiert wird, muss immer SessionId, Agent und WorkingDirectory mitgegeben werden da sonst keine korrekt Zuordnung statt findet und somit keine Dialog. Sollten diese in einem Kommando fehlen, werden diese von der Basissession der Chat-Konfiguration vererbt.

3. [ ] Sollten Befehle in der Console oder im Telegram-Chat eingegeben werden, die nicht den Anforderungen entsprechen, gibt eine kurze Meldung aus, das dieser Befehl nicht bekannt ist und anschließend die Hilfe-Seite.

4. [ ] Die Rückmeldung von OpenCode kann Text (Markdown), Html oder JSON sein. Besonderst bei Html ist drauf zu achten, das der `parse_mode` in der Telegram-Nachricht auf `HTML` gesetzt wird, damit die Formatierung korrekt dargestellt wird.

### II) Start-Betrieb

1. [ ] Wenn Hekiris gestartet wird, lädt es zu erst die Konfiguration.
       - Sollte diese fehlen oder ungültig sein, wird eine Fehlermeldung ausgegeben und Hekiris wird mit dem Fehlercode `1` beendet.

2. [ ] Prüft ob Telegram erreichbar.
       - Sollte dies nicht der Fall sein, wird eine Fehlermeldung ausgegeben und Hekiris wird mit dem Fehlercode `1` beendet.

3. [ ] Prüft ob OpenCode erreichbar ist.
       - Sollte dies nicht der Fall sein, wird eine Fehlermeldung ausgegeben und Hekiris wird mit dem Fehlercode `1` beendet.

4. [ ] Beginnt nach erfolgreichen Start auf neue Telegram-Nachrichten zu warten.

5. [ ] Beim Start schickt Hekiris in die Console eine Statusmeldung.

6. [ ] Beim Start schickt Hekiris dem hinterlegten Telegram-Chat eine Statusmeldung.

### III) Laufender Betrieb

1. [ ] Wenn der Benutzer in Telegram etwas schreibt, dann wird das wie folgt in der Console angezeigt *ohne die Nachricht selbst*.

    ```text
    [2026-03-14 18:47:24] HEKIRIS:
    Incoming message: chat 1700580252, userId 1700580252, username attkri.
    ```

2. [ ] Hekiris akzeptiert nur private Nachrichten aus genau dem vorgesehenen Telegram-Chat (config) und nur von einer erlaubten Person.
       - [ ] Alles andere wird ignoriert aber protokolliert.

3. [ ] Schreibt man eine normale Nachricht, leitet Hekiris sie an die zugeordnete OpenCode-Session weiter und sendet die Antwort wieder zurück in denselben Telegram-Chat.

4. [ ] Schreibt man einen Hekiris-Befehl wie `/help`, `/ss`, `/sc` oder `/c1`, führt Hekiris stattdessen die passende interne Aktion aus, zum Beispiel Hilfe anzeigen, Status senden oder einen vorbereiteten Auftrag starten.

5. [ ] Für vorbereitete Aufträge gibt es feste Schnellbefehle wie `/c1`, `/c2` usw.; diese verwenden den hinterlegten Prompt und auf Wunsch auch eine eigene Session, einen eigenen Agenten oder ein eigenes Arbeitsverzeichnis.

6. [ ] Wenn für einen Ablauf bereits etwas läuft, arbeitet Hekiris neue Dinge nicht parallel im selben Kanal ab, sondern nacheinander; so sollen sich Aufträge nicht gegenseitig durcheinanderbringen.
       - [ ] Es gibt keine persistente Queue, aber Hekiris merkt sich das im laufenden Betrieb im Arbeitsspeicher und versucht, die Reihenfolge der Aufträge so zu steuern, dass sie nicht kollidieren. Wird Hekiris neu gestartet, geht diese Steuerung verloren und es wird wieder direkt auf neue Nachrichten reagiert.

7. [ ] Bestimmte vorbereitete Aufträge können automatisch in einem Zeitintervall (`m` für Minuten, `h` für Stunden) ausgelöst werden; wenn so ein Zeitpunkt erreicht ist, startet Hekiris den Auftrag selbstständig und meldet das im Telegram-Chat.

### IV) Ausfall-, Protokoll-und End-Betrieb

1. [ ] Folgende Punkte müssen mit Zeitstemplen gelogt (s. `## 3. Dateien`) werden:

   - Alle von Telegram empfangene Nachrichten mit Chat-ID, User-ID und Username ohne die Nachricht selbst.
   - Alle an Telegram gesendeten Antworten mit Chat-ID ohne die Nachricht selbst.
   - Alle Commands (`/cN`), die an OpenCode gesendet wurden, mit Session-ID, Agent, Arbeitsverzeichnis und Nachricht.
   - Alle Fehler oder Warnungen bei der Kommunikation mit Telegram oder OpenCode.
   - Das Starten und Stoppen von Hekiris.
   - Den Anfänglich überprüften Status von Telegram und OpenCode beim Starten.

2. [ ] Wenn OpenCode vorübergehend ausfällt, meldet Hekiris das an Telegram; sobald OpenCode wieder erreichbar ist, meldet Hekiris auch das und prüft erneut, ob automatische Aufträge jetzt nachgeholt werden sollen.

3. [ ] Wenn Hekiris beendet wird, nimmt es keine neuen Nachrichten mehr an, informiert den Chat darüber und versucht laufende Arbeiten sauber zu stoppen.
