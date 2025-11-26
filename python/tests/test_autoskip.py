import unittest
from unittest.mock import patch, MagicMock
from pynput.mouse import Button
import ctypes
from src.autoskip_dialogue import ScreenConfig, LoggerManager, PixelSampler, InputRemapper, AutoSkipper

class TestScreenConfig(unittest.TestCase):
    @patch('src.autoskip_dialogue.GetSystemMetrics')
    @patch('src.autoskip_dialogue.os.getenv')
    def test_load_with_env(self, mock_getenv, mock_get_system_metrics):
        mock_get_system_metrics.side_effect = [1920, 1080]
        # Mock env vars: WIDTH, HEIGHT, WINDOW_TITLE
        def getenv_side_effect(key, default=None):
            if key == "WIDTH": return "1920"
            if key == "HEIGHT": return "1080"
            if key == "WINDOW_TITLE": return "Genshin Impact"
            return default
        mock_getenv.side_effect = getenv_side_effect
        
        config = ScreenConfig.load(interactive=False)
        self.assertEqual(config.WIDTH, 1920)
        self.assertEqual(config.HEIGHT, 1080)
        self.assertEqual(config.WINDOW_TITLE, "Genshin Impact")

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
    def setUp(self):
        # Patch ctypes.windll.gdi32 and user32
        self.gdi32_patcher = patch('ctypes.windll.gdi32')
        self.user32_patcher = patch('ctypes.windll.user32')
        self.mock_gdi32 = self.gdi32_patcher.start()
        self.mock_user32 = self.user32_patcher.start()
        
    def tearDown(self):
        self.gdi32_patcher.stop()
        self.user32_patcher.stop()

    def test_get_pixel(self):
        sampler = PixelSampler()
        # Mock GetPixel to return white (0x00FFFFFF)
        # COLORREF is 0x00bbggrr. White is 0x00FFFFFF.
        self.mock_gdi32.GetPixel.return_value = 0x00FFFFFF
        
        color = sampler.get(100, 100)
        self.assertEqual(color, (255, 255, 255))
        self.mock_gdi32.GetPixel.assert_called()

    def test_colors_match(self):
        c1 = (255, 255, 255)
        c2 = (250, 255, 255)
        # Mock env vars: WIDTH, HEIGHT, WINDOW_TITLE
        def getenv_side_effect(key, default=None):
            if key == "WIDTH": return "1920"
            if key == "HEIGHT": return "1080"
            if key == "WINDOW_TITLE": return "Genshin Impact"
            return default
        mock_getenv.side_effect = getenv_side_effect
        
        config = ScreenConfig.load(interactive=False)
        self.assertEqual(config.WIDTH, 1920)
        self.assertEqual(config.HEIGHT, 1080)
        self.assertEqual(config.WINDOW_TITLE, "Genshin Impact")

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
    def setUp(self):
        # Patch ctypes.windll.gdi32 and user32
        self.gdi32_patcher = patch('ctypes.windll.gdi32')
        self.user32_patcher = patch('ctypes.windll.user32')
        self.mock_gdi32 = self.gdi32_patcher.start()
        self.mock_user32 = self.user32_patcher.start()
        
    def tearDown(self):
        self.gdi32_patcher.stop()
        self.user32_patcher.stop()

    def test_get_pixel(self):
        sampler = PixelSampler()
        # Mock GetPixel to return white (0x00FFFFFF)
        # COLORREF is 0x00bbggrr. White is 0x00FFFFFF.
        self.mock_gdi32.GetPixel.return_value = 0x00FFFFFF
        
        color = sampler.get(100, 100)
        self.assertEqual(color, (255, 255, 255))
        self.mock_gdi32.GetPixel.assert_called()

    def test_colors_match(self):
        c1 = (255, 255, 255)
        c2 = (250, 255, 255)
        c3 = (240, 255, 255)
        
        self.assertTrue(PixelSampler.colors_match(c1, c2, tolerance=10))
        self.assertFalse(PixelSampler.colors_match(c1, c3, tolerance=10))


class TestInputRemapper(unittest.TestCase):
    @patch('src.autoskip_dialogue.KeyboardController')
    def test_on_click(self, mock_keyboard_controller):
        mock_is_genshin_active = MagicMock(return_value=True)
        remapper = InputRemapper(mock_is_genshin_active, MagicMock())
        remapper.on_click(0, 0, Button.x1, True)
        mock_keyboard_controller().press.assert_called_with('t')


class TestAutoSkipper(unittest.TestCase):
    def setUp(self):
        self.mock_config = MagicMock(spec=ScreenConfig)
        self.mock_config.WINDOW_TITLE = "Genshin Impact"
        self.mock_config.PLAYING_ICON = (10, 10)
        self.mock_config.DIALOGUE_ICON = (20, 20, 30)
        self.mock_config.LOADING_PIXEL = (40, 40)
        
        self.mock_logger = MagicMock(spec=LoggerManager)
        self.mock_rand = MagicMock()
        self.mock_rand.random.return_value = 0.5
        self.mock_rand.uniform.return_value = 0.1
        self.mock_rand.randint.return_value = 3
        
        self.skipper = AutoSkipper(self.mock_config, self.mock_logger, self.mock_rand)

    @patch('src.autoskip_dialogue.GetForegroundWindow')
    @patch('src.autoskip_dialogue.GetWindowText')
    def test_is_genshin_active(self, mock_get_window_text, mock_get_foreground):
        mock_get_foreground.return_value = 123
        mock_get_window_text.return_value = "Genshin Impact"
        self.assertTrue(self.skipper.is_genshin_active())

    @patch('src.autoskip_dialogue.GetForegroundWindow')
    @patch('src.autoskip_dialogue.GetWindowText')
    def test_is_not_genshin_active(self, mock_get_window_text, mock_get_foreground):
        mock_get_foreground.return_value = 123
        mock_get_window_text.return_value = "Other Game"
        self.assertFalse(self.skipper.is_genshin_active())


if __name__ == '__main__':
    unittest.main()