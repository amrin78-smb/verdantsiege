"""Branding compositor (PIL). Run after `branding.py`:  python3 Tools/Blender/branding_compose.py
Writes Art/Branding/icon_foreground.png, icon_background.png, icon_legacy.png, logo.png."""
import os, json, math, random
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Art", "Branding")
S = 1024


def radial_glow(size, center, radius, color, strength=1.0):
    """RGBA soft glow (gaussian-ish falloff)."""
    w, h = size
    g = Image.new("L", size, 0)
    d = ImageDraw.Draw(g)
    cx, cy = center
    for i in range(40, 0, -1):
        r = radius * i / 40
        a = int(255 * strength * (1 - i / 40) ** 2)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=a)
    g = g.filter(ImageFilter.GaussianBlur(radius * 0.08))
    layer = Image.new("RGBA", size, color + (0,))
    layer.putalpha(g)
    return layer


def icon_foreground():
    raw = Image.open(os.path.join(OUT, "_icon_fg_raw.png")).convert("RGBA")
    meta = json.load(open(os.path.join(OUT, "_icon_meta.json")))
    hx, hy = meta["heart"][0] * S, meta["heart"][1] * S
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.alpha_composite(radial_glow((S, S), (hx, hy), 330, (255, 214, 102), 0.85))
    out.alpha_composite(radial_glow((S, S), (hx, hy), 170, (255, 244, 190), 0.9))
    out.alpha_composite(raw)
    # keep everything inside the 66 % safe circle: fade alpha outside r = 0.33*S
    mask = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(mask)
    for i in range(30):
        r = S * 0.33 + i * 3
        pass
    d.ellipse([S * 0.5 - S * 0.36, S * 0.5 - S * 0.36, S * 0.5 + S * 0.36, S * 0.5 + S * 0.36], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(18))
    a = ImageChops.multiply(out.getchannel("A"), mask)
    out.putalpha(a)
    out.save(os.path.join(OUT, "icon_foreground.png"))
    return out


def icon_background():
    img = Image.new("RGB", (S, S))
    px = img.load()
    c0 = (14, 46, 28)        # deep moss rim
    c1 = (64, 132, 58)       # mid green
    c2 = (255, 214, 120)     # warm sunlit centre
    cx, cy = S * 0.5, S * 0.44
    for y in range(S):
        for x in range(S):
            t = min(1.0, math.hypot(x - cx, y - cy) / (S * 0.72))
            if t < 0.35:
                k = t / 0.35
                c = tuple(int(c2[i] + (c1[i] - c2[i]) * k ** 0.8) for i in range(3))
            else:
                k = (t - 0.35) / 0.65
                c = tuple(int(c1[i] + (c0[i] - c1[i]) * k ** 0.9) for i in range(3))
            px[x, y] = c
    # subtle leaf silhouettes around the rim
    rnd = random.Random(7)
    leaves = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for k in range(14):
        ang = 2 * math.pi * k / 14 + rnd.uniform(-0.2, 0.2)
        r = S * rnd.uniform(0.42, 0.56)
        x, y = cx + math.cos(ang) * r, cy + math.sin(ang) * r
        L = rnd.uniform(130, 220); W = L * 0.42
        leaf = Image.new("RGBA", (int(L * 2), int(L * 2)), (0, 0, 0, 0))
        dl = ImageDraw.Draw(leaf)
        pts = []
        for i in range(25):
            u = i / 24
            pts.append((L * 0.5 + u * L, L - W * 0.5 * math.sin(math.pi * u) ** 0.8))
        for i in range(24, -1, -1):
            u = i / 24
            pts.append((L * 0.5 + u * L, L + W * 0.5 * math.sin(math.pi * u) ** 0.8))
        shade = rnd.choice([(30, 80, 40), (40, 100, 48), (22, 64, 34)])
        dl.polygon(pts, fill=shade + (rnd.randint(70, 120),))
        dl.line([(L * 0.5, L), (L * 1.5, L)], fill=(255, 255, 255, 18), width=3)
        leaf = leaf.rotate(math.degrees(-ang) + rnd.uniform(-40, 40), resample=Image.BICUBIC, center=(L * 0.5, L))
        leaves.alpha_composite(leaf, (int(x - L * 0.5), int(y - L)))
    leaves = leaves.filter(ImageFilter.GaussianBlur(2.5))
    bg = img.convert("RGBA")
    bg.alpha_composite(leaves)
    # soft vignette
    vig = radial_glow((S, S), (cx, cy), S * 0.5, (255, 236, 170), 0.35)
    bg.alpha_composite(vig)
    bg = bg.convert("RGB")
    bg.save(os.path.join(OUT, "icon_background.png"))
    return bg


def icon_legacy(fg, bg):
    img = bg.convert("RGBA")
    img.alpha_composite(fg)
    mask = Image.new("L", (S * 4, S * 4), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, S * 4 - 1, S * 4 - 1], radius=int(S * 4 * 0.08), fill=255)
    mask = mask.resize((S, S), Image.LANCZOS)
    img.putalpha(mask)
    img.save(os.path.join(OUT, "icon_legacy.png"))


def logo():
    raw = Image.open(os.path.join(OUT, "_logo_raw.png")).convert("RGBA")
    w, h = raw.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    # soft warm glow behind the wordmark (keeps readability on dark moss), plus the raw render
    a = raw.getchannel("A").filter(ImageFilter.GaussianBlur(28))
    glow = Image.new("RGBA", (w, h), (255, 200, 90, 0))
    glow.putalpha(a.point(lambda v: int(v * 0.35)))
    out.alpha_composite(glow)
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sh = raw.getchannel("A").filter(ImageFilter.GaussianBlur(10)).point(lambda v: int(v * 0.55))
    shadow.putalpha(sh)
    out.alpha_composite(shadow, (0, 14))
    out.alpha_composite(raw)
    out.save(os.path.join(OUT, "logo.png"))


if __name__ == "__main__":
    fg = icon_foreground()
    bg = icon_background()
    icon_legacy(fg, bg)
    logo()
    print("branding composed:", sorted(f for f in os.listdir(OUT) if not f.startswith("_")))
