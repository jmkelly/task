# Config Command Specification

## Overview
Add a config command to the CLI tool to persist commonly used settings, starting with API URL and default output format. This eliminates the need to repeat `--api-url` on every command and allows customization of default output behavior.

## Configuration Scope
- `api-url`: Base URL for the Task API (with reachability validation)
- `defaultOutput`: Default output format - either `"json"` or `"plain"` (defaults to `"plain"`)

## Configuration File
- **Location**: `~/.task/config.json`
- **Format**:
```json
{
  "apiUrl": "https://api.example.com",
  "defaultOutput": "plain"
}
```

## ConfigCommand Implementation

### Subcommands
- `task config set <key> <value>` - Set a configuration value
- `task config get <key>` - Get a configuration value
- `task config unset <key>` - Remove a configuration value
- `task config list` - Show all current config values

### Supported Keys
- `api-url`: Maps to `apiUrl` in JSON
- `defaultOutput`: Maps to `defaultOutput` in JSON (accepts `"json"` or `"plain"`)

### Validation Rules
- **api-url**: Must be valid HTTP/HTTPS URL format + reachable via HEAD request (5 second timeout)
- **defaultOutput**: Must be exactly `"json"` or `"plain"`
- **Error Handling**: Fail with descriptive error messages for invalid values or unreachable URLs

## Code Changes Required

### New Files
- `Config.cs`: Handles loading/saving JSON config file with System.Text.Json
- `ConfigCommand.cs`: Implements the config command using Spectre.Console.Cli subcommands

### Modified Files
- `Program.cs`:
  - Add ConfigCommand registration in app.Configure()
  - Update DynamicDependency attribute to include ConfigCommand
  - Modify TaskCommandSettings to load config defaults
- `TaskCommandSettings.cs`: Load values from config file as defaults, allow CLI options to override

## Implementation Details

### Config.cs
- Creates `~/.task/` directory if needed
- Lazy-loads config file on first access
- Saves config atomically to prevent corruption
- Handles missing/corrupted files by falling back to defaults
- Default values: `defaultOutput: "plain"`, `apiUrl: null`

### TaskCommandSettings Updates
- Load config on initialization
- Map `defaultOutput` to appropriate boolean flags:
  - `"json"` → `Json = true`
  - `"plain"` → `Plain = true`
- CLI options (`--json`, `--plain`) override config settings
- If config specifies conflicting output modes, CLI flags take precedence

### ConfigCommand.cs
- Uses Spectre.Console.Cli's subcommand infrastructure
- Provides clear help text and validation feedback
- Supports case-insensitive boolean input where applicable

## Edge Cases
- Config file doesn't exist: Use hardcoded defaults
- Config file corrupted: Log warning and use defaults
- Network timeout during URL validation: Clear error message
- Invalid config values: Reject with specific error messages

## Dependencies
- `System.Text.Json` (already included)
- `System.Net.Http` (for URL validation, included via existing packages)

## Testing Considerations
- Unit tests for Config.cs file operations
- Integration tests for config loading in TaskCommandSettings
- End-to-end tests for config command functionality
- Mock network requests for URL validation testing

## Future Extensions
This framework can easily accommodate additional config options such as:
- Default priority for new tasks
- Common tags
- List command filters
- Custom timeouts</content>
<parameter name="filePath">/home/james/Documents/code/Task/.planning/specs/CONFIG_COMMAND_SPEC.md