import time
import ctypes
from threading import Thread

def benchmark_pixel_get():
    gdi32 = ctypes.windll.gdi32
    user32 = ctypes.windll.user32
    hdc = user32.GetDC(0)
    
    count = 1000
    
    print(f"Benchmarking {count} GetPixel calls in Python (ctypes)...")
    
    start = time.perf_counter()
    for _ in range(count):
        gdi32.GetPixel(hdc, 100, 100)
    end = time.perf_counter()
    
    user32.ReleaseDC(0, hdc)
    
    duration = end - start
    ops_per_sec = count / duration
    print(f"Time: {duration:.4f} seconds")
    print(f"Speed: {ops_per_sec:,.0f} ops/sec")

if __name__ == "__main__":
    benchmark_pixel_get()
