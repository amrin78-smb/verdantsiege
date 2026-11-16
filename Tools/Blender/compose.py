"""compose.py out.png img1 img2 ... -> side-by-side contact strip (512 high)."""
import sys
from PIL import Image
ims = [Image.open(p).convert('RGB') for p in sys.argv[2:]]
ims = [i.resize((int(i.width * 512 / i.height), 512)) for i in ims]
out = Image.new('RGB', (sum(i.width for i in ims), 512), (255, 255, 255))
x = 0
for i in ims:
    out.paste(i, (x, 0)); x += i.width
out.save(sys.argv[1])
