"""
The wildlife show's photo pack: a picture for any hunt mark you care to give it one.

    python tools/make_hunt_photos.py

Drop images into tools/hunt_photos/, one per mark, named after the mark as the game names it
("Laideronnette.png", "Sabotender Bailarina.jpg", "Vogaal_Ja.png" — case, spaces and underscores
do not matter). Any screenshot will do; the show treats it as a trail-cam photograph, terrain
and all, on the WANTED poster, the fact card and Mogwyn's field notes. In the field the mark
stays a shape in the grass, since nobody has seen one up close.

Each image is trimmed of any uniform border, scaled to ninety-six rows with a box filter and a
nearest pick so it pixelates rather than blurs, lightly quantised, and written to
src/Aetherstream.Plugin/data/hunts.json.gz as
    [{"n": name, "w": width, "h": height, "p": [rgb ints, row-major]}]

The photos on the community wikis are screenshots of Square Enix's game; keeping a handful for
a plugin that runs inside that game is fan use.
"""
import gzip
import json
import os
import sys

from PIL import Image, ImageChops

HEIGHT = 96
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "hunt_photos")
OUT = os.path.join(HERE, "..", "src", "Aetherstream.Plugin", "data", "hunts.json.gz")


def trim(im):
    """Cuts away a uniform border, the way a wiki thumbnail often carries one."""
    bg = Image.new(im.mode, im.size, im.getpixel((0, 0)))
    diff = ImageChops.difference(im, bg)
    bbox = diff.getbbox()
    return im.crop(bbox) if bbox else im


def sprite(path):
    im = Image.open(path).convert("RGB")
    im = trim(im)
    w, h = im.size
    tw = max(8, round(w * HEIGHT / h))
    small = im.resize((tw * 2, HEIGHT * 2), Image.BOX).resize((tw, HEIGHT), Image.NEAREST)
    px = []
    for r, g, b in small.getdata():
        r, g, b = ((v & 0xF8) | 4 for v in (r, g, b))
        px.append((255 << 24) | (b << 16) | (g << 8) | r)
    return tw, HEIGHT, px


os.makedirs(SRC, exist_ok=True)
out = []
for file in sorted(os.listdir(SRC)):
    stem, ext = os.path.splitext(file)
    if ext.lower() not in (".png", ".jpg", ".jpeg", ".webp", ".bmp"):
        continue
    name = stem.replace("_", " ").strip()
    try:
        w, h, px = sprite(os.path.join(SRC, file))
        out.append({"n": name, "w": w, "h": h, "p": px})
        print(f"{name:28} {w}x{h}")
    except Exception as ex:  # noqa: BLE001
        print(f"{name:28} skipped: {ex}", file=sys.stderr)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with gzip.open(OUT, "wt", encoding="utf-8") as z:
    json.dump(out, z, separators=(",", ":"))
print(f"{len(out)} photos -> {os.path.normpath(OUT)} ({os.path.getsize(OUT)} bytes)")
