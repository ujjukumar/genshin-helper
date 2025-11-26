import unittest
from unittest.mock import patch
from random import Random

from src.autoskip_dialogue import InputRemapper


class TestSpamIntegration(unittest.TestCase):
    @patch('src.autoskip_dialogue.press')
    def test_spam_for_duration_deterministic(self, mock_press):
        # Use deterministic RNG so the intervals are reproducible
        rand = Random(12345)

        # is_active always True
        remapper = InputRemapper(lambda: True, rand)

        # Run the spam for 2.0 seconds
        remapper._spam_for_duration(2.0)

        # Count how many times press('f') was called
        calls = [c for c in mock_press.call_args_list if c.args and c.args[0] == 'f']
        # With this seed and interval range, we expect a specific number of presses.
        # If implementation changes, update this expected value.
        expected_presses = 16
        self.assertEqual(len(calls), expected_presses)


if __name__ == '__main__':
    unittest.main()
