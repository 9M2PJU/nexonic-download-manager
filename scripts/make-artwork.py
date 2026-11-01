#!/usr/bin/env python3
"""Draws the app icon (Mac, Windows, extension) and the Mac installer window background. Needs Pillow and Inter."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONT = "/usr/share/fonts/opentype/inter/"
TOP, BOTTOM = (56, 189, 248), (37, 64, 205)  # sky to deep blue


def gradient(size, top, bottom):
    w, h = size
    g = Image.new("RGB", (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        g.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)))
    return g.resize((w, h))


def icon(px=1024):
    s = 4  # draw large, then shrink for smooth edges
    n = px * s
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    m = round(n * 0.1)  # macOS icon grid margin
    body = (m, m, n - m, n - m)
    r = round((n - 2 * m) * 0.225)
    # soft shadow
    shadow = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((m, m + n * 0.015, n - m, n - m + n * 0.015), r, fill=(10, 20, 60, 110))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(n * 0.02)))
    # body with gradient
    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle(body, r, fill=255)
    img.paste(gradient((n, n), TOP, BOTTOM), (0, 0), mask)
    # top sheen
    sheen = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(sheen).ellipse((-n * 0.2, -n * 0.75, n * 1.2, n * 0.42), fill=(255, 255, 255, 26))
    img = Image.alpha_composite(img, Image.composite(sheen.filter(ImageFilter.GaussianBlur(n * 0.03)), Image.new("RGBA", (n, n)), mask))
    d = ImageDraw.Draw(img)
    c = n / 2
    white = (255, 255, 255, 255)
    # arrow: stem + head
    sw = n * 0.105
    d.rounded_rectangle((c - sw / 2, n * 0.25, c + sw / 2, n * 0.56), sw * 0.3, fill=white)
    hw, top_y, tip_y = n * 0.215, n * 0.47, n * 0.665
    d.polygon([(c - hw, top_y), (c + hw, top_y), (c, tip_y)], fill=white)
    # tray
    tw, ty, th = n * 0.27, n * 0.725, n * 0.06
    d.rounded_rectangle((c - tw, ty, c + tw, ty + th), th / 2, fill=white)
    # speed lines, blended over the blue
    lines = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lines)
    for i, (y, ln) in enumerate([(0.28, 0.11), (0.355, 0.16), (0.43, 0.09)]):
        x2 = c - sw / 2 - n * 0.04
        ld.rounded_rectangle((x2 - n * ln, n * y, x2, n * y + n * 0.036), n * 0.018, fill=(255, 255, 255, 200 - i * 40))
    img = Image.alpha_composite(img, lines)
    return img.resize((px, px), Image.LANCZOS)


def background(scale):
    w, h = 660 * scale, 500 * scale
    img = gradient((w, h), (250, 251, 254), (232, 238, 250)).convert("RGBA")
    d = ImageDraw.Draw(img)
    f = lambda name, size: ImageFont.truetype(FONT + name, size * scale)
    ink, muted, accent = (24, 32, 52), (92, 102, 122), (37, 99, 235)

    def center(text, y, font, fill):
        tw = d.textlength(text, font=font)
        d.text(((w - tw) / 2, y * scale), text, font=font, fill=fill)

    center("Install Nexonic Download Manager", 26, f("InterDisplay-Bold.otf", 24), ink)
    center("Drag the app onto Applications", 60, f("Inter-Regular.otf", 14), muted)
    # arrow between the two icons (icons sit at x 180 and 480, y 165)
    y = 165 * scale
    d.line((262 * scale, y, 392 * scale, y), fill=(150, 162, 186), width=3 * scale)
    d.polygon([(392 * scale, y - 9 * scale), (408 * scale, y), (392 * scale, y + 9 * scale)], fill=(150, 162, 186))
    # divider and extras caption
    d.line((60 * scale, 286 * scale, (660 - 60) * scale, 286 * scale), fill=(214, 222, 236), width=scale)
    center("Browser extension and Firefox add-on (optional)", 296, f("Inter-Medium.otf", 11), muted)
    # thank-you footer
    center("Thank you for downloading! More tools and updates:", 452, f("Inter-Regular.otf", 11), muted)
    t1, t2, sep = "github.com/Shahrukhalid", "www.shahrukhalid.com", "   ·   "
    fb = f("Inter-SemiBold.otf", 12)
    total = d.textlength(t1 + sep + t2, font=fb)
    x = (w - total) / 2
    d.text((x, 468 * scale), t1, font=fb, fill=accent)
    x += d.textlength(t1, font=fb)
    d.text((x, 468 * scale), sep, font=fb, fill=muted)
    x += d.textlength(sep, font=fb)
    d.text((x, 468 * scale), t2, font=fb, fill=accent)
    return img.convert("RGB")


def main():
    big = icon(1024)
    big.save(ROOT / "packaging/icon-1024.png")
    big.resize((256, 256), Image.LANCZOS).save(
        ROOT / "windows/App/app.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    for s in (16, 48, 128):
        icon(s * 4).resize((s, s), Image.LANCZOS).save(ROOT / f"extension/icons/{s}.png")
    background(1).save(ROOT / "packaging/dmg-background.png")
    background(2).save(ROOT / "packaging/dmg-background@2x.png")
    print("Artwork written")


if __name__ == "__main__":
    main()
