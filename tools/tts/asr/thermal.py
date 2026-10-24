"""Pauses long jobs before the laptop overheats (it powers off at 103 °C)."""
import pathlib
import time

ZONE = pathlib.Path("/sys/class/thermal/thermal_zone0/temp")
HOT, COOL = 80.0, 65.0


def celsius() -> float:
    try:
        return int(ZONE.read_text()) / 1000
    except OSError:
        return 0.0


def cool_down() -> None:
    if celsius() < HOT:
        return
    print(f"hot ({celsius():.0f} °C), pausing", flush=True)
    while celsius() > COOL:
        time.sleep(20)
    print(f"cooled to {celsius():.0f} °C", flush=True)
