# Telegram Provider

The Telegram provider lets Task.Api send a notification when there are no
active tasks left in `todo` or `in_progress`.

## Configuration

Task.Api binds the `Telegram` section from `appsettings.json` (or environment
overrides) to `TelegramProviderOptions`.

```json
{
  "Telegram": {
    "Enabled": false,
    "BotToken": "",
    "ChatId": "",
    "DefaultMessage": "No tasks are currently in todo or in_progress."
  }
}
```

### Option meanings

- `Enabled`: Controls whether notifications are sent. When `false`, the provider
  logs and skips sends.
- `BotToken`: Telegram bot token used to build the API base address.
- `ChatId`: Target chat ID for the message.
- `DefaultMessage`: Message text when notifications trigger. Blank or whitespace
  falls back to the default string shown above.

If `Enabled` is `true` but `BotToken` or `ChatId` are missing, the provider
throws an error to surface misconfiguration early.

## Trigger behavior

`TasksController.GetTasks` calls `TelegramNotificationService` after fetching
tasks. The notification triggers only when there are zero tasks whose status is
`todo` or `in_progress`. If any active task exists, the service logs that the
notification was skipped.

This design keeps notification logic explicit and centralized at the API
boundary where task reads already happen.
