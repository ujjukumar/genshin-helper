import time
import sys
import os

# Add src to path
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'src'))

from autoskip_dialogue import PixelSampler

def test_performance():
    sampler = PixelSampler()
    start_time = time.perf_counter()
    for _ in range(1000):
        sampler.get(100, 100)
    end_time = time.perf_counter()
    print(f"Time taken for 1000 pixel reads: {end_time - start_time:.4f} seconds")

def test_tolerance():
    c1 = (255, 255, 255)
    c2 = (250, 255, 255)
    c3 = (240, 255, 255)
    
    assert PixelSampler.colors_match(c1, c2, tolerance=10) == True
    assert PixelSampler.colors_match(c1, c3, tolerance=10) == False
    print("Tolerance tests passed!")

if __name__ == "__main__":
    test_performance()
    test_tolerance()
