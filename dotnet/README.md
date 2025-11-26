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
    *   `F12`: Exit the application.
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
    *   Press `F12` to exit.

### Command Line Arguments

*   `--verbose` or `-v`: Enables verbose debug logging.
*   `--benchmark`: Runs a pixel reading benchmark (primarily for development).

## Configuration

The application generates a `config.json` file on first run. You can modify this file to tune the bot's behavior.

| Setting | Default | Description |
| :--- | :--- | :--- |
| **General** | | |
| `WindowTitle` | `"Genshin Impact"` | The window title to look for. |
| `BaseWidth` | `1920` | Base resolution width used for coordinate calculations. |
| `BaseHeight` | `1080` | Base resolution height used for coordinate calculations. |
| **Timing & Behavior** | | |
| `StandardDelayMin` | `0.06` | Minimum delay between key presses (seconds). Lower = faster. |
| `StandardDelayMax` | `0.12` | Maximum delay between key presses (seconds). |
| `FastBurstChance` | `0.025` | Probability (0-1) of entering a temporary "fast mode" where typing speed doubles. |
| `BreakChance` | `0.015` | Probability (0-1) of taking a short break to simulate human behavior. |
| `BreakDurationMin` | `2.0` | Minimum duration of a break (seconds). |
| `BreakDurationMax` | `8.0` | Maximum duration of a break (seconds). |
| `SpaceKeyChance` | `0.1` | Probability (0-1) of pressing 'Space' instead of 'F'. |
| `BurstModeDelayMs` | `120` | Delay between presses when using the manual burst hotkey (Mouse 5). |
| **Pixel Detection** | | |
| `PlayingIconX/Y` | `84`, `46` | Coordinates for the "Playing" icon (Paimon menu). |
| `DialogueIconX` | `1301` | X coordinate for the dialogue option icon. |
| `DialogueIconLowY` | `808` | Y coordinate for the lower bound of the dialogue icon. |
| `DialogueIconHighY` | `790` | Y coordinate for the upper bound of the dialogue icon. |
| `LoadingPixelX/Y` | `1200`, `700` | Coordinates to check for the white loading screen. |
| `PlayingIconWsX` | `230` | X coordinate for "Playing" icon in wide-screen mode. |
| `DialogueIconWsX` | `2770` | X coordinate for dialogue icon in wide-screen mode. |
| `DialogueIconWsLowY` | `810` | Y coordinate for lower bound of dialogue icon (wide-screen). |
| `DialogueIconWsHighY` | `792` | Y coordinate for upper bound of dialogue icon (wide-screen). |
| `DialogueIconWsExtra` | `0.02` | Extra scaling factor for wide-screen dialogue detection. |
