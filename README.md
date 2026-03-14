# Hekiris

Hekiris is a .NET 10 console application that bridges Telegram chats to a running OpenCode server.

It keeps one base OpenCode session per Telegram chat, supports dedicated command sessions, scheduled command loops, runtime status messages, and fixed user-level config and log locations outside the repository.

## Overview

- `Hekiris.slnx` - Visual Studio solution

- `Hekiris.Console/` - production console application

- `Hekiris.Console.Tests/` - xUnit test project

## Features

- Receives Telegram messages from explicitly allowed chats and users

- Forwards normal chat messages to one configured base OpenCode session per chat

- Runs configured chat commands like `/c1` in dedicated command sessions without changing the base session

- Supports scheduled command execution through `Commands[].TimeLoop`

- Sends startup, shutdown, and OpenCode availability notifications back to Telegram

- Writes daily CSV logs to a fixed user log directory without storing chat message content

## Requirements

- Windows 11

- .NET SDK 10.x for building from source

- .NET 10 Desktop Runtime for running the packaged release build

- A running OpenCode server, for example `http://localhost:4096/`

- An existing OpenCode session for each configured base chat and command

- A local Telegram secret file, for example `%USERPROFILE%\.secrets\telegram.secrets.json`

## Installation

1. Clone the repository.

2. Restore and build the solution:

   ```powershell
   dotnet restore .\Hekiris.slnx
   dotnet build .\Hekiris.slnx
   ```

3. Create the fixed runtime config directory if it does not exist:

   ```powershell
   New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.config\Hekiris"
   ```

4. Copy the template config into the fixed runtime location:

   ```powershell
   Copy-Item .\Hekiris.Console\config.template.json "$env:USERPROFILE\.config\Hekiris\config.json"
   ```

5. Edit `%USERPROFILE%\.config\Hekiris\config.json`.

6. Run a connection check:

   ```powershell
   dotnet run --project .\Hekiris.Console -- check
   ```

## Configuration

Hekiris always loads its runtime config from `%USERPROFILE%\.config\Hekiris\config.json`.

The JSON format uses PascalCase keys such as `Telegram`, `OpenCode`, `AccessControl`, `Chats`, `Commands`, and `TimeLoop`.

`Telegram.SecretSourcePath` may be relative to the fixed config file location. The template uses `..\..\.secrets\telegram.secrets.json`.

Important sections:

- `Telegram` - Telegram API settings and bot token source

- `OpenCode` - OpenCode base URL, optional basic auth, and request timeout

- `AccessControl` - global `AllowedUserIds` and `AllowedUsernames`

- `Runtime` - queue sizes, polling behavior, health checks, and shutdown behavior

- `Chats` - base session mapping `TelegramChatId -> OpenCodeSessionId`

- `Commands` - predefined command presets with `Title`, `Session`, `Model`, `Prompt`, and optional `TimeLoop`

Example:

```json
{
  "Telegram": {
    "ApiBaseUrl": "https://api.telegram.org",
    "SecretSourcePath": "..\\..\\.secrets\\telegram.secrets.json",
    "BotToken": "",
    "PollingTimeoutSeconds": 20
  },
  "AccessControl": {
    "AllowedUserIds": [123456789],
    "AllowedUsernames": ["example_user"]
  },
  "Chats": [
    {
      "TelegramChatId": 123456789,
      "OpenCodeSessionId": "ses_base_example"
    }
  ],
  "Commands": [
    {
      "Title": "Daily Summary",
      "Session": "ses_daily_example",
      "Model": "openai/gpt-5.4",
      "Prompt": "Create today\'s summary.",
      "TimeLoop": {
        "Enabled": false,
        "Interval": "1h"
      }
    }
  ]
}
```

If `Model` does not contain a provider prefix, Hekiris defaults to `openai`.

If `Commands[].TimeLoop.Enabled=true`, Hekiris schedules the command automatically. `LastRun` is optional in the file and is updated by Hekiris when a scheduled command is queued so failed runs are not retried immediately in a tight loop.

## First-Time Setup Checklist

- Set valid Telegram allowlists in `AccessControl.AllowedUserIds` and `AccessControl.AllowedUsernames`

- Fill `Chats[]` with the Telegram chat ID and its base OpenCode session ID

- Add any optional `Commands[]` entries you want to run from chat or on a schedule

- Make sure each referenced OpenCode session already exists

- Run `Hekiris check` before `Hekiris start`

## Chat Commands

- `/help` - show available Hekiris commands

- `/stop` - stop Hekiris gracefully

- `/ss` - show the current Hekiris runtime status, including base session, command states, loop state, interval, and `LastRun`

- `/sc` - list configured commands and their `/cN` shortcuts

- `/cN` - run configured command `N`

- `/cNs` - stop configured command `N` if it is currently running

## Logging

Hekiris writes one CSV log file per day to `%USERPROFILE%\.logs\Hekiris\` using the format `yyyy-MM-dd-Hekiris.csv`.

The log format is:

```text
Timestamp; severity; Message
2026-03-14 08:15_00; INFO      ; Scheduled command /c1 queued.
```

The log keeps up to 10 days, excludes secrets, and does not store chat message text.

Incoming Telegram metadata such as `chatId`, `userId`, and `username` is logged so allowlists can be configured safely.

Unauthorized messages are silently discarded, logged, and never forwarded to OpenCode.

## Running Hekiris

```powershell
dotnet run --project .\Hekiris.Console -- help
dotnet run --project .\Hekiris.Console -- check
dotnet run --project .\Hekiris.Console -- config show
dotnet run --project .\Hekiris.Console -- start
```

## Install From Release

1. Download the latest `Hekiris-win-x64.zip` from GitHub Releases.

2. Ensure the .NET 10 Desktop Runtime is installed.

3. Extract the ZIP to a folder of your choice.

4. Create the fixed runtime config directory if it does not exist:

   ```powershell
   New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.config\Hekiris"
   ```

5. Copy `config.template.json` from the extracted release folder to `%USERPROFILE%\.config\Hekiris\config.json`.

6. Edit the config with your real Telegram token source, OpenCode URL, allowlist, chats, sessions, and commands.

7. Run:

   ```powershell
   .\Hekiris.exe check
   .\Hekiris.exe start
   ```

## Tests

```powershell
dotnet test .\Hekiris.slnx
```

## Releases

GitHub releases are built by `.github/workflows/release-publish.yml` when a tag matching `v*.*.*` is pushed.

Each release publishes a `win-x64` build archive and uses the current notes from `Release-Notes.md`.

## Release Notes

User-facing release highlights are tracked in `Release-Notes.md`.
