# G-Helper Dynamic Mode Switching

This implementation adds dynamic mode switching to G-Helper without requiring app restarts.

## Features

- **Always Running**: G-Helper now stays in the system tray regardless of CLI arguments
- **Dynamic Mode Switching**: Send mode commands to running instance via IPC
- **Real-time UI Updates**: Tray icon and UI update immediately when mode changes
- **Backward Compatibility**: Original CLI behavior maintained when no instance is running

## Usage

### Command Line Interface
```bash
# If G-Helper is already running, sends command to running instance
g-helper.exe -mode turbo
g-helper.exe -mode performance  
g-helper.exe -mode silent

# If no instance is running, starts G-Helper and sets the mode
```

### IPC Client Example
```bash
# Compile the example client
csc GHelperModeClient.cs

# Use the client to send commands
GHelperModeClient.exe turbo
GHelperModeClient.exe performance
GHelperModeClient.exe silent
```

### Programmatic Usage
You can also send commands directly via TCP socket on localhost:12345:
```
Protocol: Send "mode:turbo", "mode:performance", or "mode:silent"
Response: "OK" on success, "ERROR" on failure
```

## Implementation Details

### IPC Communication
- **Protocol**: TCP socket on localhost:12345
- **Format**: Text-based commands (e.g., "mode:turbo")
- **Response**: "OK" or "ERROR" with optional message

### Mode Mapping
- `turbo` → PerfMode.Turbo (index 2)
- `performance` → PerfMode.Balanced (index 0) 
- `silent` → PerfMode.Silent (index 1)

### Thread Safety
- IPC commands are handled on background threads
- UI updates are marshaled to the main thread using `Invoke()`
- Proper cleanup on application exit

## Files Modified
- `Program.cs`: Added IPC listener and mode handling
- `GHelperModeClient.cs`: Example client utility (new file)
- `README_IPC.md`: This documentation (new file)

## Testing
1. Start G-Helper normally
2. Use CLI: `g-helper.exe -mode turbo` (should send to running instance)
3. Use client: `GHelperModeClient.exe silent`
4. Verify tray icon and UI update without restart
