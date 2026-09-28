"""Run the unmodified Python CLI with only matrix/thunderstorm's clock virtualized.

Native Python random and native iteration order are retained. One virtual 1/60s
step follows each printed frame, matching ttfx --frame-rate 0 --virtual-clock.
"""
import time
import sys

now = 0.0
time.time = lambda: now
time.monotonic = lambda: now

from terminaltexteffects.engine.terminal import Terminal
from terminaltexteffects.__main__ import main

original_print = Terminal.print

def print_frame(self, frame):
    global now
    original_print(self, frame)
    now += 1.0 / 60.0

Terminal.print = print_frame
main()
