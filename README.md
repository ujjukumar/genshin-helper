# Genshin Impact Dialogue Auto-Skipper

## Overview
The Genshin Impact Dialogue Auto-Skipper is a Python-based tool designed to automate the skipping of dialogues in the game Genshin Impact. It utilizes screen resolution detection, pixel sampling for dialogue detection, and input remapping to enhance the gaming experience by allowing players to skip dialogues automatically.

## Features
- **Screen Resolution Detection**: Automatically detects and adjusts to the user's screen resolution.
- **Dialogue Detection**: Uses pixel sampling to identify when dialogues are active and skips them accordingly.
- **Input Remapping**: Allows for custom key mappings to enhance user interaction.
- **Logging**: Provides logging capabilities to track actions and errors.

## Requirements
- Python 3.x
- Required libraries listed in `requirements.txt`

## Installation
1. Clone the repository:
   ```
   git clone <repository-url>
   cd genshin-dialogue-autoskip
   ```

2. Install the required dependencies:
   ```
   pip install -r requirements.txt
   ```

3. Set up the environment variables by creating a `.env` file in the root directory with the following content:
   ```
   WIDTH=<your_screen_width>
   HEIGHT=<your_screen_height>
   ```

## Usage
1. Run the script:
   ```
   python src/autoskip_dialogue.py
   ```

2. Use the following hotkeys to control the auto-skipper:
   - **F7**: Toggle file logging
   - **F8**: Start the auto-skipper
   - **F9**: Pause the auto-skipper
   - **F12**: Exit the application
   - **Mouse4**: Remap to 'T' key for interaction
   - **Mouse5**: Toggle rapid 'F' spam

## Testing
Unit tests for the auto-skipper functionality can be found in the `tests/test_autoskip.py` file. To run the tests, use:
```
pytest tests/
```

## Contributing
Contributions are welcome! Please submit a pull request or open an issue for any enhancements or bug fixes.

## License
This project is licensed under the MIT License. See the LICENSE file for more details.