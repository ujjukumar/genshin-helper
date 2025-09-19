import unittest
from pynput.mouse import Button
from unittest.mock import patch, MagicMock
from src.autoskip_dialogue import ScreenConfig, LoggerManager, PixelSampler, InputRemapper, AutoSkipper

class TestScreenConfig(unittest.TestCase):
    @patch('src.autoskip_dialogue.GetSystemMetrics')
    def test_load_with_env(self, mock_get_system_metrics):
        mock_get_system_metrics.side_effect = [1920, 1080]
        config = ScreenConfig.load(interactive=False)
        self.assertEqual(config.WIDTH, 1920)
        self.assertEqual(config.HEIGHT, 1080)

    @patch('src.autoskip_dialogue.GetSystemMetrics')
    def test_load_interactive(self, mock_get_system_metrics):
        mock_get_system_metrics.side_effect = [1920, 1080]
        with patch('builtins.input', side_effect=['y']):
            config = ScreenConfig.load(interactive=True)
        self.assertEqual(config.WIDTH, 1920)
        self.assertEqual(config.HEIGHT, 1080)

class TestLoggerManager(unittest.TestCase):
    @patch('src.autoskip_dialogue.logging')
    def test_toggle_file_logging(self, mock_logging):
        logger_mgr = LoggerManager()
        logger_mgr.toggle_file_logging()
        self.assertIsNotNone(logger_mgr.file_handler)

        logger_mgr.toggle_file_logging()
        self.assertIsNone(logger_mgr.file_handler)

class TestPixelSampler(unittest.TestCase):
    @patch('src.autoskip_dialogue.pixel')
    def test_get_pixel(self, mock_pixel):
        sampler = PixelSampler()
        mock_pixel.return_value = (255, 255, 255)
        color = sampler.get(100, 100)
        self.assertEqual(color, (255, 255, 255))

class TestInputRemapper(unittest.TestCase):
    @patch('src.autoskip_dialogue.KeyboardController')
    def test_on_click(self, mock_keyboard_controller):
        mock_is_genshin_active = MagicMock(return_value=True)
        remapper = InputRemapper(mock_is_genshin_active, MagicMock())
        remapper.on_click(0, 0, Button.x1, True)
        mock_keyboard_controller().press.assert_called_with('t')

class TestAutoSkipper(unittest.TestCase):
    @patch('src.autoskip_dialogue.gw.getActiveWindow')
    def test_is_genshin_active(self, mock_get_active_window):
        mock_get_active_window.return_value.title = "Genshin Impact"
        self.assertTrue(AutoSkipper.is_genshin_active())

    @patch('src.autoskip_dialogue.gw.getActiveWindow')
    def test_is_not_genshin_active(self, mock_get_active_window):
        mock_get_active_window.return_value.title = "Other Game"
        self.assertFalse(AutoSkipper.is_genshin_active())

if __name__ == '__main__':
    unittest.main()