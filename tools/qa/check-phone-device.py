"""Capture the actual Phone Compose UI on one USB Android device; no pairing or game input.

Requires a debug Phone APK installed. Paths/serials are kept outside published results.
Usage: python tools/qa/check-phone-device.py /path/to/adb artifacts/phone-0915
"""
import json
import re
import struct
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

ADB = sys.argv[1]
OUT = Path(sys.argv[2])
OUT.mkdir(parents=True, exist_ok=True)
(OUT / "result.json").unlink(missing_ok=True)
APP = "dev.simdeck.phone"
DEVICE_DIR = "/sdcard/Android/data/dev.simdeck.phone/files/phone-preview"


def adb(*args, timeout=20):
    result = subprocess.run([ADB, *args], capture_output=True, timeout=timeout, text=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip())
    return result.stdout.strip()


def capture(profile, tab, suffix=""):
    name = profile + "-" + tab + suffix
    file = DEVICE_DIR + "/" + name + ".png"
    adb("shell", "rm", "-f", file)
    adb("shell", "am", "force-stop", APP)
    adb("shell", "am", "start", "-W", "-n", APP + "/dev.simdeck.MainActivity", "--es", "preview_profile", profile, "--es", "preview_tab", tab, "--es", "preview_capture", name)
    # PixelCopy is performed by the application, capturing content without OS panels.
    for attempt in range(8):
        time.sleep(.5)
        if adb("shell", "sh", "-c", "'test -s " + file + " && echo ready || echo waiting'") == "ready":
            break
    else:
        raise RuntimeError("Application did not capture " + name)
    # A non-empty file can still be in Bitmap.compress(); wait for PNG completion.
    for attempt in range(5):
        png = subprocess.run([ADB, "exec-out", "cat", file], capture_output=True, timeout=20)
        if not png.returncode and png.stdout.endswith(bytes.fromhex("0000000049454e44ae426082")):
            break
        time.sleep(.5)
    else:
        raise RuntimeError("Incomplete PNG transfer for " + name)
    width, height = struct.unpack(">II", png.stdout[16:24])
    assert (width > height) == bool(suffix), "Incorrect orientation for " + name
    (OUT / (name + ".png")).write_bytes(png.stdout)
    print("Captured", name, flush=True)


def hierarchy():
    file = "/sdcard/simdeck-phone-qa.xml"
    adb("shell", "uiautomator", "dump", file)
    return ET.fromstring(adb("shell", "cat", file))


def check_navigation(profile):
    adb("shell", "am", "force-stop", APP)
    adb("shell", "am", "start", "-W", "-n", APP + "/dev.simdeck.MainActivity", "--es", "preview_profile", profile)
    root = hierarchy()
    more = next(node for node in root.iter("node") if node.get("text") == "Ещё")
    x1, y1, x2, y2 = map(int, re.findall(r"\d+", more.get("bounds")))
    adb("shell", "input", "tap", str((x1+x2)//2), str((y1+y2)//2))
    assert any(node.get("text") == "ВСЕ РАЗДЕЛЫ" for node in hierarchy().iter("node")), profile
    adb("shell", "input", "keyevent", "4")
    root = hierarchy()
    assert not any(node.get("text") == "ВСЕ РАЗДЕЛЫ" for node in root.iter("node")), profile
    assert any(node.get("text") == "SIM" for node in root.iter("node")), profile
    print("Navigation and Android Back passed", profile, flush=True)


profiles = {
    "f1-24": ["drive", "condition", "map", "more"],
    "f1-25": ["drive", "condition", "map", "more"],
    "acc": ["drive", "condition", "pit", "more"],
    "ams2": ["drive", "pit", "camera", "more"],
    "beamng-default": ["drive", "condition", "camera", "more"],
    "ets2": ["drive", "map", "condition", "more"],
    "ats": ["drive", "map", "condition", "more"],
    "fs25": ["drive", "fields", "prices", "more"],
    "snowrunner": ["drive", "winch", "cargo", "more"],
}
if len([line for line in adb("devices").splitlines() if line.endswith("\tdevice")]) != 1:
    raise RuntimeError("Connect exactly one authorized QA phone")
original_rotation = adb("shell", "wm", "user-rotation")
original_fixed_rotation = adb("shell", "wm", "fixed-to-user-rotation")
try:
    adb("shell", "wm", "fixed-to-user-rotation", "enabled")
    adb("shell", "wm", "user-rotation", "lock", "0")
    if "--resume" not in sys.argv:
        for profile, tabs in profiles.items():
            for tab in tabs:
                capture(profile, tab)
    else:
        for profile, tabs in profiles.items():
            for tab in tabs:
                png = (OUT / (profile + "-" + tab + ".png")).read_bytes()
                assert png.endswith(bytes.fromhex("0000000049454e44ae426082"))
                width, height = struct.unpack(">II", png[16:24])
                assert width < height
    adb("shell", "wm", "user-rotation", "lock", "1")
    for profile in profiles:
        capture(profile, "drive", "-landscape")
    adb("shell", "wm", "user-rotation", "lock", "0")
    for profile in profiles:
        check_navigation(profile)
finally:
    rotation = original_rotation.split()
    adb("shell", "wm", "user-rotation", *rotation)
    adb("shell", "wm", "fixed-to-user-rotation", original_fixed_rotation)
    adb("shell", "am", "force-stop", APP)
    adb("shell", "am", "start", "-n", APP + "/dev.simdeck.MainActivity")
    adb("shell", "rm", "-f", "/sdcard/simdeck-phone-qa.xml")
(OUT / "result.json").write_text(json.dumps({"profiles": len(profiles), "screens": 40, "navigation_checks": 8, "source": "native Android Compose", "data": "explicit debug fixtures; game input disabled", "artwork": "shared tablet assets"}, ensure_ascii=False, indent=2), encoding="utf-8")
