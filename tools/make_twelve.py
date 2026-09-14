"""
Sprites of the Twelve for the story hour, made from their renders rather than drawn.

    python tools/make_twelve.py

The Final Fantasy wiki hosts Square Enix's official renders of the Twelve as
"<Name>_from_Final_Fantasy_XIV_render.png"; the wiki's image host lays files out by the MD5 of
the file name, so the links are computed here rather than scraped. Each is fetched, trimmed to
its content, scaled to a sprite height with a nearest pick so it pixelates rather than blurs,
lightly quantised, and written with its alpha to src/Aetherstream.Plugin/data/twelve.json.gz as
    [{"d": deity row id, "n": name, "w": width, "h": height, "p": [rgba ints, row-major]}]

The renders are Square Enix's; this is fan use inside a plugin for their own game.
"""
import gzip
import hashlib
import io
import json
import os
import sys
import urllib.parse
import urllib.request

from PIL import Image

HEIGHT = 96
OUT = os.path.join(os.path.dirname(__file__), "..", "src", "Aetherstream.Plugin", "data", "twelve.json.gz")
UA = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/128.0 Safari/537.36"}

TWELVE = [
    (1, "Halone"), (2, "Menphina"), (3, "Thaliak"), (4, "Nymeia"), (5, "Llymlaen"), (6, "Oschon"),
    (7, "Byregot"), (8, "Rhalgr"), (9, "Azeyma"), (10, "Nald'thal"), (10, "Thal"), (11, "Nophica"), (12, "Althyk"),
]

# A file that is not where the pattern says: name -> full link without the /revision part.
OVERRIDES = {
    "Byregot": "https://static.wikia.nocookie.net/finalfantasy/images/7/71/Byregot_render_from_FFXIV.png",
    "Rhalgr": "https://static.wikia.nocookie.net/finalfantasy/images/8/8e/Rhalgr_render_from_FFXIV.png",
    "Azeyma": "https://static.wikia.nocookie.net/finalfantasy/images/e/e5/Azeyma_render_from_FFXIV.png",
    # The Traders are two: Nald and Thal each have a render, and both belong to row ten.
    "Nald'thal": "https://static.wikia.nocookie.net/finalfantasy/images/2/24/Nald_render_from_FFXIV.png",
    "Thal": "https://static.wikia.nocookie.net/finalfantasy/images/a/ac/Thal_render_from_FFXIV.png",
}


def wiki_url(filename):
    digest = hashlib.md5(filename.encode("utf-8")).hexdigest()
    return f"https://static.wikia.nocookie.net/finalfantasy/images/{digest[0]}/{digest[:2]}/{urllib.parse.quote(filename)}"


def fetch(url):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=30) as r:
        return r.read()


def sprite(png):
    im = Image.open(io.BytesIO(png)).convert("RGBA")
    im = im.crop(im.getbbox())
    w, h = im.size
    tw = max(8, round(w * HEIGHT / h))
    small = im.resize((tw * 2, HEIGHT * 2), Image.BOX).resize((tw, HEIGHT), Image.NEAREST)
    px = []
    for r, g, b, a in small.getdata():
        if a < 96:
            px.append(0)
            continue
        r, g, b = ((v & 0xF8) | 4 for v in (r, g, b))
        px.append((255 << 24) | (b << 16) | (g << 8) | r)
    return tw, HEIGHT, px


out = []
for deity, name in TWELVE:
    filename = f"{name}_from_Final_Fantasy_XIV_render.png"
    url = OVERRIDES.get(name) or wiki_url(filename)
    try:
        w, h, px = sprite(fetch(url + "/revision/latest"))
        out.append({"d": deity, "n": name, "w": w, "h": h, "p": px})
        print(f"{name:10} {w}x{h}")
    except Exception as ex:  # noqa: BLE001
        print(f"{name:10} skipped: {ex}  ({url})", file=sys.stderr)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with gzip.open(OUT, "wt", encoding="utf-8") as z:
    json.dump(out, z, separators=(",", ":"))
print(f"{len(out)} sprites -> {os.path.normpath(OUT)} ({os.path.getsize(OUT)} bytes)")
