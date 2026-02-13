# Genshin AutoSkip

Genshin AutoSkip is a .NET 10 application designed to automate the dialogue skipping process in the game Genshin Impact. It achieves this by monitoring specific screen pixels to detect active dialogue sequences and then simulates keyboard input to skip through them.

## Features

*   **Automatic Dialogue Skipping**: Automatically presses 'F' or 'Space' to advance through dialogue.
*   **Intelligent Dialogue Detection**: Identifies active dialogue based on configurable screen pixel colors, adapting to various screen resolutions.
*   **Human-like Input Simulation**: Employs randomized intervals and a "burst mode" to make automated key presses less detectable.
*   **Global Hotkeys**:
    *   `F7`: Toggle logging (enables/disables output to `autoskip_dialogue.log`).
    *   `F8`: Start the auto-skipping process.
    *   `F9`: Pause the auto-skipping process.
*   **Mouse Button Remapping**:
    *   `Mouse Button 4` (XBUTTON1): Remapped to 'T'.
    *   `Mouse Button 5` (XBUTTON2): Initiates a rapid "burst" of 'F' key presses.
*   **Native AOT**: Published as a Native Ahead-of-Time (AOT) application for improved performance and single-file deployment.

## How to Use

### Building the Application

To build the application, ensure you have the .NET 10 SDK installed. Navigate to the project directory in your terminal and run the following command to create a self-contained executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

This command will publish the application for Windows x64. The executable will be located in `bin\Release\net10.0\win-x64\publish\`.

### Running the Application

1.  Navigate to the `publish` directory (e.g., `bin\Release\net10.0\win-x64\publish\`).
2.  Run the executable: `AutoSkipper.exe`
3.  Ensure Genshin Impact is running and is the active window.
4.  Use the global hotkeys to control the skipper:
    *   Press `F8` to start auto-skipping.
    *   Press `F9` to pause.
    *   Press `F7` to toggle logging to a file.
    *   Right-click the tray icon and select "Exit" to close the application.

### Command Line Arguments

*   `--verbose` or `-v`: Enables verbose debug logging.
*   `--benchmark`: Runs a pixel reading benchmark (primarily for development).

## Configuration

The application generates a minimal `config.json` file on first run with only essential settings:

| Setting | Default | Description |
| :--- | :--- | :--- |
| `WindowTitle` | `"Genshin Impact"` | The window title to look for (for non-English clients). |
| `StandardDelayMin` | `0.13` | Minimum delay between key presses (seconds). Lower = faster. |
| `StandardDelayMax` | `0.17` | Maximum delay between key presses (seconds). |
| `ColorTolerance` | `10` | RGB color matching tolerance (0-255). Higher = more lenient matching. |

Delete `config.json` to reset to defaults.
