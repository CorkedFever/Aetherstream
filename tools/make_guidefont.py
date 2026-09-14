"""Rasterises VT323 into an alpha-only glyph atlas the guide channel draws from at runtime.

Format (little-endian):
  4 bytes  magic "AEF1"
  u16      first code point (32)
  u16      glyph count (95: space .. tilde)
  u16      cell width
  u16      cell height
  then count * width * height bytes of coverage, one glyph after another, row-major.
Gzipped. VT323 is monospace, so one cell width serves every glyph.
"""
import gzip
import os
import struct

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "Aetherstream.Plugin")
FONT = ROOT + r"\Fonts\VT323-Regular.ttf"
OUT = ROOT + r"\images\guidefont.a8.gz"

SIZE = 40
FIRST, COUNT = 32, 95

font = ImageFont.truetype(FONT, SIZE)
ascent, descent = font.getmetrics()
cell_h = ascent + descent
cell_w = int(round(font.getlength("M")))

# Any glyph that draws wider than its advance (VT323 has none, but check) would be clipped.
widest = max(font.getbbox(chr(c))[2] for c in range(FIRST, FIRST + COUNT))
assert widest <= cell_w + 1, (widest, cell_w)

data = bytearray()
for code in range(FIRST, FIRST + COUNT):
    img = Image.new("L", (cell_w, cell_h), 0)
    ImageDraw.Draw(img).text((0, 0), chr(code), font=font, fill=255)
    data += img.tobytes()

blob = b"AEF1" + struct.pack("<HHHH", FIRST, COUNT, cell_w, cell_h) + bytes(data)
with gzip.open(OUT, "wb", compresslevel=9) as f:
    f.write(blob)

print(f"cell {cell_w}x{cell_h}, {len(blob)} bytes raw -> {OUT}")
