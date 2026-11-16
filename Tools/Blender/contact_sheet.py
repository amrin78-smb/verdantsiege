"""Contact sheet of all previews: python3 Tools/Blender/contact_sheet.py out.png [NameGlob ...] (default: all)"""
import sys, os, glob
from PIL import Image, ImageDraw
root = os.path.join(os.path.dirname(__file__), "..", "..", "Art", "Previews")
pats = sys.argv[2:] or ["*"]
files = sorted({f for pat in pats for f in glob.glob(os.path.join(root, pat + ".png")) if not os.path.basename(f).startswith("_")})
cols, cell = 6, 256
rows = (len(files) + cols - 1) // cols
sheet = Image.new("RGB", (cols * cell, rows * (cell + 18)), (240, 242, 238))
d = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    im = Image.open(f).convert("RGB").resize((cell, cell))
    x, y = (i % cols) * cell, (i // cols) * (cell + 18)
    sheet.paste(im, (x, y))
    d.text((x + 6, y + cell + 3), os.path.basename(f)[:-4], fill=(30, 30, 30))
sheet.save(sys.argv[1])
print(len(files), "previews")
