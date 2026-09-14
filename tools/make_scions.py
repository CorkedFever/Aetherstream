"""
Sprites of the Scions for the story hour, made from their renders rather than drawn.

    python tools/make_scions.py

The wiki blocks scripts from its pages but not from its image host, so the render links are
kept in tools/scion_renders.json (collected by hand from each character's infobox) and each
is fetched from there, trimmed to its content, scaled to a sprite height with
nearest-neighbour sampling (so it pixelates rather than blurs), lightly quantised, and written
with its alpha to src/Aetherstream.Plugin/data/scions.json.gz as
    [{"n": name, "c": calling, "w": width, "h": height, "p": [rgba ints, row-major]}]

The renders are Square Enix's; this is fan use inside a plugin for their own game.
"""
import gzip
import io
import json
import os
import re
import sys
import urllib.request

from PIL import Image

HEIGHT = 56
OUT = os.path.join(os.path.dirname(__file__), "..", "src", "Aetherstream.Plugin", "data", "scions.json.gz")
UA = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/128.0 Safari/537.36"}

# Name, wiki page, calling as the story tells it.
SCIONS = [
    ("Y'shtola", "Y%27shtola_Rhul", "a conjurer of Gridania, and later a black mage"),
    ("Alphinaud", "Alphinaud_Leveilleur", "a scholar of Sharlayan, and a sage"),
    ("Alisaie", "Alisaie_Leveilleur", "a red mage of Sharlayan"),
    ("Thancred", "Thancred_Waters", "a rogue of Limsa Lominsa, and a gunbreaker"),
    ("Urianger", "Urianger_Augurelt", "an astrologian of Sharlayan"),
    ("G'raha Tia", "G%27raha_Tia", "a scholar of the Students of Baldesion"),
    ("Estinien", "Estinien_Varlineau", "a dragoon of Ishgard"),
    ("Krile", "Krile_Mayer_Baldesion", "a scholar of Sharlayan, and a pictomancer"),
    ("Tataru", "Tataru_Taru", "a receptionist, and the finest weaver in Eorzea"),
    ("Ryne", "Ryne", "an oracle of light, from the First"),
    ("Wuk Lamat", "Wuk_Lamat", "a warrior of Tural"),
]


def fetch(url):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=30) as r:
        return r.read()


def render_url(page):
    html = fetch(f"https://finalfantasy.fandom.com/wiki/{page}").decode("utf-8", "replace")
    # Infobox images, in order; the FFXIV ones carry FFXIV in the file name. Take the last.
    urls = re.findall(r'<img[^>]+src="(https://static\.wikia\.nocookie\.net/finalfantasy/images/[^"]+?\.png)/revision/latest[^"]*"', html)
    ffxiv = [u for u in urls if "FFXIV" in u or "XIV" in u]
    if not ffxiv:
        ffxiv = urls
    if not ffxiv:
        raise RuntimeError(f"no render on {page}")
    return ffxiv[-1] + "/revision/latest"


def sprite(png):
    im = Image.open(io.BytesIO(png)).convert("RGBA")
    bbox = im.getbbox()
    im = im.crop(bbox)
    w, h = im.size
    scale = HEIGHT / h
    tw = max(8, round(w * scale))
    # A gentle box filter before the nearest pick keeps the outline from breaking up.
    small = im.resize((tw * 2, HEIGHT * 2), Image.BOX).resize((tw, HEIGHT), Image.NEAREST)
    px = []
    for r, g, b, a in small.getdata():
        if a < 96:
            px.append(0)
            continue
        # Quantise to 5 bits a channel: reads as pixel art rather than a shrunken photo.
        r, g, b = ((v & 0xF8) | 4 for v in (r, g, b))
        px.append((255 << 24) | (b << 16) | (g << 8) | r)
    return tw, HEIGHT, px


RENDERS = json.load(open(os.path.join(os.path.dirname(__file__), "scion_renders.json"), encoding="utf-8"))

out = []
for name, page, calling in SCIONS:
    try:
        url = RENDERS.get(name)
        if not url:
            print(f"{name:12} skipped: no render listed", file=sys.stderr)
            continue
        url += "/revision/latest"
        w, h, px = sprite(fetch(url))
        out.append({"n": name, "c": calling, "w": w, "h": h, "p": px})
        print(f"{name:12} {w}x{h}  {url}")
    except Exception as ex:  # noqa: BLE001
        print(f"{name:12} skipped: {ex}", file=sys.stderr)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with gzip.open(OUT, "wt", encoding="utf-8") as z:
    json.dump(out, z, separators=(",", ":"))
print(f"{len(out)} sprites -> {os.path.normpath(OUT)} ({os.path.getsize(OUT)} bytes)")
