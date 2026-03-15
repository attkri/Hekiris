# Release Notes

## 0.2.0 [Unreleased]

- **Breaking:** Access control now uses a single allowed user instead of lists. Replace `AllowedUserIds` and `AllowedUsernames` with `AllowedUserId` and `AllowedUsername` in your config file.

- **Breaking:** The `StartupSessionValidation` config field has been removed. Delete it from your config if present.

- **New:** CLI now shows the current version on every command and `config show` displays the config file path, making it easier to verify which configuration is active.

- **New:** `start` always runs a connection check before launching, so misconfigurations are caught immediately.

- **New:** `/ss` now sends the base status and each command as separate Telegram messages for better readability.

- **New:** Commands can reuse the base chat defaults for session, agent, and working directory, so shared repo setups need less duplicate configuration.

- **New:** HTML responses from OpenCode are forwarded to Telegram with proper formatting. Responses that exceed Telegram limits automatically fall back to plain text.

- **New:** JSON responses from OpenCode are detected and displayed as a distinct format.

- **New:** Unknown commands in Telegram and CLI now return a clear message with available commands instead of being silently ignored.

- **New:** Scheduled commands are rechecked for missed runs when OpenCode becomes available again, even after a successful request.

- **Improved:** Requests now reliably reach OpenCode with the correct agent and working directory, so repo-local agents resolve correctly.

- **Improved:** The request queue now processes messages per chat channel instead of per command number, preventing unrelated commands from blocking each other.

- **Improved:** Internal architecture has been restructured for better maintainability without changing user-facing behavior.