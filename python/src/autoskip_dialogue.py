import os
import logging
import argparse
from dataclasses import dataclass, field
from logging.handlers import RotatingFileHandler
from random import Random
from threading import Thread, Event
import threading
import time
from time import perf_counter
from typing import Optional, Tuple, Callable, Dict

from win32api import GetSystemMetrics
from win32gui import GetForegroundWindow, GetWindowText
from pynput.keyboard import Key, KeyCode, Listener as KeyboardListener, Controller as KeyboardController
from pynput.mouse import Listener as MouseListener, Button
from dotenv import find_dotenv, load_dotenv, set_key
import ctypes

# --- constants ---
PLAYING_ICON_COLOR = (236, 229, 216)
WHITE = (255, 255, 255)

logger = logging.getLogger(__name__)

class LogFormatter(logging.Formatter):
    def formatTime(self, record, datefmt=None):
        ct = self.converter(record.created)
        t = time.strftime("%H:%M:%S", ct)
        return f"{t}.{int(record.msecs):03d}"

@dataclass
class ScreenConfig:
    WIDTH: int
    HEIGHT: int
    BASE_W: int = 1920
    BASE_H: int = 1080
    PLAYING_ICON: Tuple[int, int] = field(init=False)
    DIALOGUE_ICON: Tuple[int, int, int] = field(init=False)
    LOADING_PIXEL: Tuple[int, int] = field(init=False)
    WINDOW_TITLE: str = field(init=False, default="Genshin Impact")

    def __post_init__(self):
        self.PLAYING_ICON = self._calc_playing_icon()
        self.DIALOGUE_ICON = self._calc_dialogue_icon()
        self.LOADING_PIXEL = (self._wa(1200), self._ha(700))

    @classmethod
    def load(cls, interactive: bool = True) -> "ScreenConfig":
        load_dotenv()
        w_env, h_env = os.getenv("WIDTH", ""), os.getenv("HEIGHT", "")
        window_title = os.getenv("WINDOW_TITLE", "Genshin Impact")
        
        instance = None
        if w_env and h_env:
            try:
                instance = cls(int(w_env), int(h_env))
            except ValueError:
                logger.warning("Invalid WIDTH/HEIGHT in .env, re-detecting.")
        
        if instance is None:
            w, h = GetSystemMetrics(0), GetSystemMetrics(1)
            if interactive:
                print(f"Detected Resolution: {w}x{h}")
                print("Is the resolution correct? (y/n) ", end="")
                if input().strip().lower().startswith("n"):
                    w = int(input("Enter resolution width: ").strip())
                    h = int(input("Enter resolution height: ").strip())
                    print(f"New resolution set to {w}x{h}\n")
            dotenv_file = find_dotenv()
            if dotenv_file:
                set_key(dotenv_file, "WIDTH", str(w), quote_mode="never")
                set_key(dotenv_file, "HEIGHT", str(h), quote_mode="never")
            else:
                with open(".env", "w", encoding="utf-8") as f:
                    f.write(f"WIDTH={w}\nHEIGHT={h}\n")
            instance = cls(w, h)
            
        instance.WINDOW_TITLE = window_title
        return instance

    def _wa(self, x: int) -> int:
        return int(x / self.BASE_W * self.WIDTH)

    def _ha(self, y: int) -> int:
        return int(y / self.BASE_H * self.HEIGHT)

    @staticmethod
    def _scale_pos(hd: int, double_hd: int, current_w: int, extra: float = 0.0) -> int:
        if current_w <= 3840:
            extra = 0.0
        diff = double_hd - hd
        return int(hd + (current_w - 1920) * ((diff / 1920) + extra))

    def _calc_playing_icon(self) -> Tuple[int, int]:
        widescreen = self.WIDTH > 1920 and (self.HEIGHT / self.WIDTH) != 0.5625
        if widescreen:
            x = self._scale_pos(84, 230, self.WIDTH)
            x = min(x, 230)
            y = self._ha(46)
        else:
            x = self._wa(84)
            y = self._ha(46)
        return x, y

    def _calc_dialogue_icon(self) -> Tuple[int, int, int]:
        widescreen = self.WIDTH > 1920 and (self.HEIGHT / self.WIDTH) != 0.5625
        if widescreen:
            x = self._scale_pos(1301, 2770, self.WIDTH, 0.02)
            lower_y = self._ha(810)
            higher_y = self._ha(792)
        else:
            x = self._wa(1301)
            lower_y = self._ha(808)
            higher_y = self._ha(790)
        return x, lower_y, higher_y


class LoggerManager:
    def __init__(self, verbose: bool = False) -> None:
        self.file_handler: Optional[logging.FileHandler] = None
        self.formatter = LogFormatter("%(asctime)s | %(levelname)-8s | %(message)s")
        
        # Setup console logging
        level = logging.DEBUG if verbose else logging.INFO
        handler = logging.StreamHandler()
        handler.setFormatter(self.formatter)
        
        logging.basicConfig(level=level, handlers=[handler], force=True)

    def toggle_file_logging(self) -> None:
        if self.file_handler:
            logger.info("File logging disabled")
            logger.removeHandler(self.file_handler)
            self.file_handler.close()
            self.file_handler = None
            return
        # rotate at 5MB, keep 3 backups
        self.file_handler = RotatingFileHandler("autoskip_dialogue.log", encoding="utf-8", maxBytes=5_000_000, backupCount=3)
        self.file_handler.setLevel(logging.DEBUG)
        self.file_handler.setFormatter(self.formatter)
        logger.addHandler(self.file_handler)
        logger.info("File logging enabled: autoskip_dialogue.log (Level: DEBUG)")


class PixelSampler:
    def __init__(self) -> None:
        self.fail_counts: Dict[Tuple[int, int], int] = {}
        self.total_failures = 0
        self._last_warn = 0
        self._gdi32 = ctypes.windll.gdi32
        self._user32 = ctypes.windll.user32
        self._hdc = self._user32.GetDC(0)

    def __del__(self):
        try:
            if self._hdc:
                self._user32.ReleaseDC(0, self._hdc)
        except Exception:
            pass

    def get(self, x: int, y: int) -> Optional[Tuple[int, int, int]]:
        try:
            color_ref = self._gdi32.GetPixel(self._hdc, x, y)
            if color_ref == 0xFFFFFFFF:  # CLR_INVALID
                return None
            # COLORREF is 0x00bbggrr
            r = color_ref & 0xFF
            g = (color_ref >> 8) & 0xFF
            b = (color_ref >> 16) & 0xFF
            return (r, g, b)
        except Exception:
            key = (x, y)
            self.fail_counts[key] = self.fail_counts.get(key, 0) + 1
            self.total_failures += 1
            if self.total_failures - self._last_warn >= 25:
                self._last_warn = self.total_failures
                logger.warning(f"Pixel failures total={self.total_failures} unique={len(self.fail_counts)}")
            return None

    @staticmethod
    def colors_match(c1: Tuple[int, int, int], c2: Tuple[int, int, int], tolerance: int = 10) -> bool:
        if not c1 or not c2:
            return False
        return all(abs(a - b) <= tolerance for a, b in zip(c1, c2))


class InputRemapper:
    def __init__(self, is_active_fn: Callable[[], bool], rand: Random) -> None:
        self.keyboard = KeyboardController()
        self._is_genshin_active = is_active_fn
        self._spam_thread: Optional[Thread] = None
        self._lock = threading.Lock()
        # spam thread used for one-shot short bursts
        self._rand = rand

    def on_click(self, _x, _y, button, pressed) -> None:
        if not pressed:
            return
        try:
            if button == Button.x1 and self._is_genshin_active():
                self.keyboard.press('t')
                self.keyboard.release('t')
                logger.info("Remap: Mouse4 -> T")
            elif button == Button.x2:
                # one-shot spam of 'f' for a short duration (2s)
                if not self._is_genshin_active():
                    # don't spam if Genshin isn't active
                    return
                if self._spam_thread and self._spam_thread.is_alive():
                    logger.info("Spam-F: already running")
                else:
                    with self._lock:
                        if self._spam_thread and self._spam_thread.is_alive():
                            return
                        logger.info("Spam-F: 4s burst")
                        self._spam_thread = Thread(target=lambda: self._spam_for_duration(4.0), daemon=True)
                        self._spam_thread.start()
        except Exception:
            logger.exception("Mouse handler error")

    def _spam_for_duration(self, duration: float = 4.0) -> None:
        """Spam the 'f' key repeatedly for `duration` seconds, then stop."""
        logger.info(f"Spam-F for {duration:.1f}s started")
        end = perf_counter() + duration
        try:
            while perf_counter() < end:
                try:
                    if self._is_genshin_active():
                        self.keyboard.press('f')
                        self.keyboard.release('f')
                except Exception:
                    logger.exception("Spam-F error")
                # sleep but don't overshoot the end time
                remaining = end - perf_counter()
                if remaining <= 0:
                    break
                delay = min(self._rand.uniform(0.08, 0.18), remaining)
                Event().wait(delay)
        except Exception:
            logger.exception("Spam-F loop crashed")
        logger.info("Spam-F finished")


class AutoSkipper:
    def __init__(self, config: ScreenConfig, logger_mgr: LoggerManager, rand: Random) -> None:
        self.config = config
        self.logger_mgr = logger_mgr
        self.status = "pause"  # run / pause
        self._stop = False

        self.rand = rand
        self.pixel_sampler = PixelSampler()

        # Initialize burst pool before first interval calculation
        self._burst_pool = 0  # internal rapid interval counter
        
        self.keyboard = KeyboardController()
        self._cached_hwnd = None

        self._last_press_time = perf_counter()
        self._next_interval = self._next_key_interval()

        self._last_break_check = perf_counter()
        self._break_interval = 30.0
        self._break_until = 0.0

        self._skip_next = False
        self._double_next = False
        self._burst_mode = False
        self._burst_remaining = 0
        self._post_burst_pause_until = 0.0
        self._in_dialogue = False
        self._window_active = False

        self.wake_event = Event()
        self.input_remapper = InputRemapper(self.is_genshin_active, rand)

    # --- window check ---
    def is_genshin_active(self) -> bool:
        try:
            hwnd = GetForegroundWindow()
            if self._cached_hwnd and hwnd == self._cached_hwnd:
                return True
            
            window_title = GetWindowText(hwnd)
            if self.config.WINDOW_TITLE.lower() in window_title.lower():
                self._cached_hwnd = hwnd
                return True
            return False
        except Exception:
            return False

    # --- timing randomness ---
    def _next_key_interval(self) -> float:
        if self._burst_pool > 0:
            self._burst_pool -= 1
            return self.rand.uniform(0.05, 0.09)
        if self.rand.random() < 1/50:
            self._burst_pool = self.rand.randint(2, 5)
            return self.rand.uniform(0.05, 0.09)
        if self.rand.random() < 1/8:
            return self.rand.uniform(0.09, 0.25)
        return self.rand.uniform(0.11, 0.21)

    def _maybe_break(self) -> Optional[str]:
        r = self.rand.random()
        if r < 1/100:
            return "long"
        if r < 1/100 + 1/25:
            return "short"
        return None

    def _break_duration(self, kind: str) -> float:
        return self.rand.uniform(4.0, 10.0) if kind == "long" else self.rand.uniform(2.0, 6.0)

    # --- dialogue detection ---
    def _dialogue_playing(self) -> bool:
        px = self.pixel_sampler.get(*self.config.PLAYING_ICON)
        return self.pixel_sampler.colors_match(px, PLAYING_ICON_COLOR)

    def _dialogue_choice(self) -> bool:
        if self.pixel_sampler.colors_match(self.pixel_sampler.get(*self.config.LOADING_PIXEL), WHITE):
            return False
        x, low_y, hi_y = self.config.DIALOGUE_ICON
        return (self.pixel_sampler.colors_match(self.pixel_sampler.get(x, low_y), WHITE)) or \
               (self.pixel_sampler.colors_match(self.pixel_sampler.get(x, hi_y), WHITE))

    # --- hotkey input ---
    def on_key(self, key: KeyCode) -> None:
        if key in (Key.f8,):
            self.status = "run"
            logger.info("RUN")
            self.wake_event.set()
        elif key in (Key.f9,):
            self.status = "pause"
            logger.info("PAUSE")
            self.wake_event.set()
        elif key in (Key.f12,):
            logger.info("EXIT requested")
            self._stop = True
            self.wake_event.set()
        elif key in (Key.f7,):
            self.logger_mgr.toggle_file_logging()

    # --- core loop (reduced CPU) ---
    def run_loop(self) -> None:
        self._print_instructions()
        self._last_press_time = perf_counter()
        next_state_check = 0.0  # when to re-check dialogue presence

        while not self._stop:
            now = perf_counter()

            if self.status == "pause":
                # Sleep until something wakes us or small timeout to allow exit
                self.wake_event.wait(0.5)
                self.wake_event.clear()
                self._last_press_time = perf_counter()
                continue

            is_active = self.is_genshin_active()
            if is_active != self._window_active:
                self._window_active = is_active
                if is_active:
                    logger.info("Window State: ACTIVE")
                else:
                    logger.info("Window State: INACTIVE")

            if not is_active:
                self._sleep_until(now + 0.4)
                continue

            # handle active break
            if now < self._break_until:
                self._sleep_until(self._break_until)
                continue

            # periodic break check
            if now - self._last_break_check > self._break_interval:
                self._last_break_check = now
                br = self._maybe_break()
                if br:
                    dur = self._break_duration(br)
                    logger.info(f"Break: {br} {dur:.1f}s")
                    self._break_until = now + dur
                    self._next_interval = self._next_key_interval()
                    continue

            # dialogue state check (throttled)
            if now >= next_state_check:
                is_dialogue = self._dialogue_playing() or self._dialogue_choice()
                
                if is_dialogue != self._in_dialogue:
                    self._in_dialogue = is_dialogue
                    if is_dialogue:
                        logger.info("Dialogue State: DETECTED")
                    else:
                        logger.info("Dialogue State: ENDED")

                next_state_check = now + 0.15  # throttle pixel polling
                if not is_dialogue:
                    self._sleep_until(now + 0.25)
                    continue

            # post-burst pause
            if now < self._post_burst_pause_until:
                self._sleep_until(self._post_burst_pause_until)
                continue

            # decide action timing
            action_due = (now - self._last_press_time) >= self._next_interval or self._burst_mode
            if action_due:
                # group random decisions (use a few shared draws)
                r1 = self.rand.random()
                r2 = self.rand.random()
                r3 = self.rand.random()

                if not self._skip_next and r1 < 1/40:
                    self._skip_next = True
                if not self._double_next and r2 < 1/35:
                    self._double_next = True
                if (not self._burst_mode) and r3 < 1/60:
                    self._burst_mode = True
                    self._burst_remaining = self.rand.randint(3, 5)
                    logger.info(f"Burst mode: {self._burst_remaining}")

                if self._skip_next:
                    self._skip_next = False
                    self._last_press_time = now
                    self._next_interval = self._next_key_interval()
                else:
                    self._perform_press(now)

            # compute next wake time
            next_action_time = self._last_press_time + self._next_interval
            wake_target = min(next_action_time,
                              self._break_until if self._break_until > now else float("inf"),
                              next_state_check,
                              self._post_burst_pause_until if self._post_burst_pause_until > now else float("inf"))
            # cap minimum sleep
            self._sleep_until(min(wake_target, now + 0.35))

        logger.info("Closing")

    def _perform_press(self, now: float) -> None:
        try:
            # choose key
            use_space = self.rand.random() < (0.1 if not self._burst_mode else 0.1)
            key_obj = Key.space if use_space else 'f'
            
            self.keyboard.press(key_obj)
            self.keyboard.release(key_obj)
            
            key_name = "space" if use_space else "f"
            logger.debug(f"Pressed {key_name.upper()}")

            if (not use_space) and self._double_next:
                self._double_next = False
                # small delay before second press
                self.keyboard.press('f')
                self.keyboard.release('f')
                logger.debug("Double F")
                self._post_burst_pause_until = now + self.rand.uniform(0.4, 1.0)

            if self._burst_mode:
                self._burst_remaining -= 1
                if self._burst_remaining <= 0:
                    self._burst_mode = False
                    self._post_burst_pause_until = now + self.rand.uniform(0.4, 1.0)

        except Exception:
            logger.exception("Press error")

        self._last_press_time = now
        self._next_interval = self._next_key_interval()

    def _sleep_until(self, target_time: float) -> None:
        if self._stop:
            return
        timeout = max(0.0, target_time - perf_counter())
        # wait can be interrupted by wake_event (e.g., hotkey)
        self.wake_event.wait(timeout)
        self.wake_event.clear()

    @staticmethod
    def _print_instructions() -> None:
        print("Genshin Impact Dialogue Auto-Skip (Optimized)")
        print("F7: Toggle file logging")
        print("F8: Start")
        print("F9: Pause")
        print("F12: Exit")
        print("Mouse4: T key (interact remap)")
        print("Mouse5: 2s rapid F burst\n")


def main() -> None:
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--no-interactive", action="store_true", help="Disable interactive resolution prompt")
    parser.add_argument("--verbose", "-v", action="store_true", help="Enable verbose (DEBUG) logging")
    parser.add_argument("--seed", type=int, default=None, help="Deterministic RNG seed")
    args, _ = parser.parse_known_args()

    seed = args.seed
    if seed is None:
        env_seed = os.getenv("GDA_DEBUG_SEED")
        if env_seed and env_seed.isdigit():
            seed = int(env_seed)
    rand = Random(seed)
    
    # Initialize LoggerManager first as it handles logging setup
    logger_mgr = LoggerManager(verbose=args.verbose)
    
    if seed is not None:
        logger.info(f"Deterministic seed: {seed}")

    config = ScreenConfig.load(interactive=not args.no_interactive)
    skipper = AutoSkipper(config, logger_mgr, rand)

    t = Thread(target=skipper.run_loop, daemon=True)
    t.start()

    def on_release(key):
        skipper.on_key(key)

    def on_click(x, y, button, pressed):
        skipper.input_remapper.on_click(x, y, button, pressed)

    k_listener = KeyboardListener(on_release=on_release)
    m_listener = MouseListener(on_click=on_click)
    k_listener.start()
    m_listener.start()

    try:
        t.join()
    finally:
        skipper._stop = True
        skipper.wake_event.set()
        for lst in (k_listener, m_listener):
            try:
                lst.stop()
            except Exception:
                pass
        for lst in (k_listener, m_listener):
            try:
                lst.join()
            except Exception:
                pass


if __name__ == "__main__":
    main()