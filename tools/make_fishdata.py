"""
Trims the Carbuncle Plushy fish tracker's data into what the fishing channel needs.

    python tools/make_fishdata.py path/to/fishData.yaml

The source is private/fishData.yaml in https://github.com/icykoneko/ff14-fish-tracker-app (MIT).
Only fish with a window — a weather, a preceding weather, or hours — are kept: the channel is
about what is up now and what comes next, and a fish that is always there has no "next". Rod
fishing only; spearfishing has no weather or hours and is a different log.

Output: src/Aetherstream.Plugin/data/fish.json.gz, a list of
    {"n": name, "l": spot name, "s": start hour, "e": end hour, "w": [weathers],
     "p": [preceding weathers], "b": [bait chain], "m": {predator: count}, "f": folklore,
     "v": patch, "t": tug}
"""
import gzip
import json
import os
import sys

import yaml

src = sys.argv[1]
out = os.path.join(os.path.dirname(__file__), "..", "src", "Aetherstream.Plugin", "data", "fish.json.gz")

fish = yaml.safe_load(open(src, encoding="utf-8"))
kept = []
for f in fish:
    loc = f.get("location")
    if not isinstance(loc, str) or f.get("gig"):
        continue

    start, end = f.get("startHour", 0), f.get("endHour", 24)
    weather, prev = f.get("weatherSet") or [], f.get("previousWeatherSet") or []
    if not weather and not prev and start == 0 and end == 24:
        continue

    kept.append({
        "n": f["name"],
        "l": loc,
        "s": start,
        "e": end,
        "w": weather,
        "p": prev,
        "b": f.get("bestCatchPath") or [],
        "m": f.get("predators") or {},
        "f": bool(f.get("folklore")),
        "v": float(f.get("patch") or 0),
        "t": f.get("tug") or "",
    })

os.makedirs(os.path.dirname(out), exist_ok=True)
with gzip.open(out, "wt", encoding="utf-8") as z:
    json.dump(kept, z, separators=(",", ":"))

print(f"{len(kept)} of {len(fish)} fish -> {os.path.normpath(out)} ({os.path.getsize(out)} bytes)")
