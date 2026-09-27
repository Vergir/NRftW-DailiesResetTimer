"""Builds the Nexus / README images in docs/pics/nexus from the raw screenshots in docs/pics.

Needs Pillow (pip install pillow). Usage: python make_nexus_images.py
Raw screenshots (git-ignored) are 2878 px wide game frames; *_short / *_full = default / Always Show Full Time.
All coordinates below are in raw screenshot pixels.
"""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "nexus")
FONT = "C:/Windows/Fonts/segoeuib.ttf"
FONT_LIGHT = "C:/Windows/Fonts/segoeui.ttf"
GOLD = (216, 201, 163)
W, H = 1920, 1080
RAW_W = 2878
S = W / RAW_W  # raw -> 1920 scale

# Top of the 16:9 game frame inside each raw screenshot (they were cropped by hand, with black bars).
FRAME_TOP = {"shop_short": 0, "shop_full": 0, "quests_short": 40, "quests_full": 10, "board_short": 0, "board_full": 30}

# Timer regions (raw pixels, left, top, right, bottom).
SHOP_TIMER = {"shop_short": (1180, 190, 1650, 245), "shop_full": (1120, 195, 1650, 250)}
SHOP_ROW = (890, 170, 1660, 270)            # "Vendor" heading + timer, for the zoom inset
QUESTS_TIMER = {"quests_short": (140, 355, 630, 435), "quests_full": (140, 315, 630, 395)}
BOARD_DAILY = {"board_short": (200, 95, 500, 210), "board_full": (180, 145, 500, 260)}
BOARD_WEEKLY = {"board_short": (2380, 95, 2700, 210), "board_full": (2330, 145, 2720, 260)}

# Settings screenshot (1761 px wide): our rows, and one crop per row for the captions.
SETTINGS_ROWS = (230, 290, 1440, 630)       # our four rows
SETTINGS_ROW_Y = {"board": 335, "quests": 418, "vendor": 502, "fulltime": 586}


def font(size, light=False):
    return ImageFont.truetype(FONT_LIGHT if light else FONT, size)


def raw(name):
    return Image.open(os.path.join(HERE, name + ".png")).convert("RGB")


def frame(name):
    """The 16:9 game frame of a raw screenshot, scaled to 1920x1080 (black where the screenshot is shorter)."""
    im = raw(name)
    top = FRAME_TOP[name]
    canvas = Image.new("RGB", (RAW_W, round(RAW_W * 9 / 16)), (0, 0, 0))
    canvas.paste(im.crop((0, top, RAW_W, min(im.height, top + canvas.height))), (0, 0))
    return canvas.resize((W, H), Image.LANCZOS)


def to_frame(name, box, pad=0):
    """Raw box -> box in the 1920x1080 frame, grown by pad."""
    top = FRAME_TOP[name]
    l, t, r, b = box
    return (round(l * S) - pad, round((t - top) * S) - pad, round(r * S) + pad, round((b - top) * S) + pad)


def spotlight(img, boxes, dim=0.38, radius=12):
    """Darken everything outside the boxes and outline them in gold."""
    dark = Image.eval(img, lambda v: int(v * dim))
    mask = Image.new("L", img.size, 0)
    d = ImageDraw.Draw(mask)
    for b in boxes:
        d.rounded_rectangle(b, radius, fill=255)
    out = Image.composite(img, dark, mask.filter(ImageFilter.GaussianBlur(3)))
    d = ImageDraw.Draw(out)
    for b in boxes:
        d.rounded_rectangle(b, radius, outline=GOLD, width=3)
    return out


def shadowed_paste(dst, src, xy, border=GOLD):
    """Paste with a soft drop shadow and a thin border."""
    x, y = xy
    sh = Image.new("RGBA", (src.width + 60, src.height + 60), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rectangle((30, 30, 30 + src.width, 30 + src.height), fill=(0, 0, 0, 200))
    sh = sh.filter(ImageFilter.GaussianBlur(14))
    dst.paste(sh, (x - 30 + 6, y - 30 + 8), sh)
    dst.paste(src, (x, y))
    ImageDraw.Draw(dst).rectangle((x - 1, y - 1, x + src.width, y + src.height), outline=border, width=2)


def zoom(name, box, factor):
    """A raw-resolution crop, scaled by factor relative to the 1920 frame."""
    im = raw(name).crop(box)
    return im.resize((round(im.width * S * factor), round(im.height * S * factor)), Image.LANCZOS)


def connector(img, a, b):
    ImageDraw.Draw(img).line((a, b), fill=GOLD, width=2)


def settings_row(key, width):
    """The real settings row ("Bounty Board Reset Timers   On [x]") as a caption strip."""
    y = SETTINGS_ROW_Y[key]
    im = raw("settings").crop((255, y - 36, 1440, y + 36))
    return im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)


def caption(img, key, cx, y, width=720):
    """Settings row centered at (cx, y) on a dark strip, with a small 'Options > Gameplay' label above it."""
    row = settings_row(key, width)
    x = cx - row.width // 2
    d = ImageDraw.Draw(img, "RGBA")
    d.rounded_rectangle((x - 24, y - 48, x + row.width + 24, y + row.height + 14), 10, fill=(0, 0, 0, 190))
    d.text((x, y - 42), "Options > Gameplay", font=font(24, light=True), fill=(170, 170, 170))
    img.paste(row, (x, y))


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name), quality=92)
    print(f"{name:28} {os.path.getsize(os.path.join(OUT, name)) // 1024:5} KB")


# ---- Screens: spotlight + zoom inset + the matching settings row -----------------------------------------------------

def board():
    n = "board_short"
    img = frame(n)
    bd, bw = to_frame(n, BOARD_DAILY[n], 6), to_frame(n, BOARD_WEEKLY[n], 6)
    img = spotlight(img, [bd, bw])
    zd, zw = zoom(n, BOARD_DAILY[n], 2.2), zoom(n, BOARD_WEEKLY[n], 2.2)
    pd, pw = (30, 560), (W - 30 - zw.width, 560)
    # Down the outer edges, clear of the "Daily" / "Weekly" titles and the bounty icons.
    connector(img, (bd[0] + 12, bd[3]), (bd[0] + 12, pd[1]))
    connector(img, (bw[2] - 12, bw[3]), (bw[2] - 12, pw[1]))
    shadowed_paste(img, zd, pd)
    shadowed_paste(img, zw, pw)
    caption(img, "board", W // 2, 800)
    save(img, "board.jpg")


def quests():
    n = "quests_short"
    img = frame(n)
    b = to_frame(n, QUESTS_TIMER[n], 6)
    img = spotlight(img, [b])
    z = zoom(n, QUESTS_TIMER[n], 2.4)
    p = (560, 470)
    connector(img, (b[2], (b[1] + b[3]) // 2), (p[0], p[1] + z.height // 2))
    shadowed_paste(img, z, p)
    caption(img, "quests", p[0] + z.width // 2, p[1] + z.height + 110)
    save(img, "quests.jpg")


def shop():
    n = "shop_short"
    img = frame(n)
    b = to_frame(n, SHOP_TIMER[n], 6)
    img = spotlight(img, [b])
    z = zoom(n, SHOP_ROW, 1.8)
    p = (W - 70 - z.width, 650)
    connector(img, ((b[0] + b[2]) // 2, b[3]), (p[0] + z.width * 3 // 4, p[1]))
    shadowed_paste(img, z, p)
    caption(img, "vendor", p[0] + z.width // 2, p[1] + z.height + 100)
    save(img, "shop.jpg")


# ---- Settings: the Gameplay tab with our rows lit ---------------------------------------------------------------------

def settings():
    im = raw("settings").crop((0, 40, 1761, 785))   # from below the half-cut row at the top
    scale = min(W / im.width, (H - 150) / im.height)
    im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    l, t, r, b = SETTINGS_ROWS
    box = (round(l * scale) - 8, round((t - 40) * scale) - 8, round(r * scale) + 8, round((b - 40) * scale) + 8)
    im = spotlight(im, [box], dim=0.45)
    img = Image.new("RGB", (W, H), (0, 0, 0))
    img.paste(im, ((W - im.width) // 2, 130))
    d = ImageDraw.Draw(img)
    d.text((W // 2, 62), "Options > Gameplay", font=font(56), fill="white", anchor="mm")
    save(img, "settings.jpg")


# ---- Default vs Always Show Full Time ----------------------------------------------------------------------------------

def full_time():
    img = Image.new("RGB", (W, H), (12, 12, 14))
    d = ImageDraw.Draw(img)
    d.text((W // 2, 100), "Always Show Full Time", font=font(60), fill="white", anchor="mm")
    colx = (W // 4 + 20, 3 * W // 4 - 20)
    d.text((colx[0], 200), "Off (default)", font=font(40, light=True), fill=GOLD, anchor="mm")
    d.text((colx[1], 200), "On", font=font(40, light=True), fill=GOLD, anchor="mm")
    rows = [
        ("Bounty board", [("board_short", BOARD_WEEKLY["board_short"]), ("board_full", BOARD_WEEKLY["board_full"])]),
        ("Quests menu", [("quests_short", QUESTS_TIMER["quests_short"]), ("quests_full", QUESTS_TIMER["quests_full"])]),
        ("Vendor", [("shop_short", SHOP_TIMER["shop_short"]), ("shop_full", SHOP_TIMER["shop_full"])]),
    ]
    y = 290
    for label, pair in rows:
        crops = [raw(n).crop(box) for n, box in pair]
        f = min(1.5, min(820 / c.width for c in crops))          # same scale for both columns of a row
        crops = [c.resize((round(c.width * f), round(c.height * f)), Image.LANCZOS) for c in crops]
        rh = max(c.height for c in crops)
        d.text((40, y - 6), label, font=font(26, light=True), fill=(150, 150, 150))
        for cx, c in zip(colx, crops):
            shadowed_paste(img, c, (cx - c.width // 2, y + 30 + (rh - c.height) // 2), border=(90, 85, 70))
        y += rh + 110
    save(img, "full_time.jpg")


# ---- Cover and Nexus header ----------------------------------------------------------------------------------------

def cover():
    img = frame("board_short").filter(ImageFilter.GaussianBlur(6))
    img = Image.eval(img, lambda v: int(v * 0.45))
    d = ImageDraw.Draw(img)
    d.text((W // 2, 150), "Dailies Reset Timer", font=font(104), fill="white", anchor="mm")
    d.text((W // 2, 250), "Reset and restock countdowns for No Rest for the Wicked", font=font(42, light=True),
           fill=GOLD, anchor="mm")
    cards = [
        ("Bounty board", zoom("board_short", BOARD_DAILY["board_short"], 2.6)),
        ("Quests menu", zoom("quests_short", QUESTS_TIMER["quests_short"], 1.9)),
        ("Vendors", zoom("shop_short", SHOP_TIMER["shop_short"], 1.9)),
    ]
    gap = 50
    total = sum(c.width for _, c in cards) + gap * (len(cards) - 1)
    x = (W - total) // 2
    ch = max(c.height for _, c in cards)
    for label, c in cards:
        y = 470 + (ch - c.height) // 2
        shadowed_paste(img, c, (x, y))
        d.text((x + c.width // 2, 470 + ch + 50), label, font=font(34, light=True), fill=(200, 200, 200), anchor="mm")
        x += c.width + gap
    d.text((W // 2, H - 90), "Daily & weekly bounties and challenges  ·  vendor restock", font=font(34, light=True),
           fill=(170, 170, 170), anchor="mm")
    save(img, "cover.jpg")


def header():
    """Nexus page header, 1300x372: the board with the title on the right."""
    img = frame("board_short").crop((0, 0, W, round(W * 372 / 1300))).resize((1300, 372), Image.LANCZOS)
    # Transparent over the left half (the daily timer stays visible), then darkening to near black behind the title.
    grad = Image.linear_gradient("L").rotate(90).resize((1300, 372))
    img = Image.composite(Image.new("RGB", img.size, (0, 0, 0)), img,
                          grad.point(lambda v: 0 if v < 110 else min(235, int((v - 110) * 5))))
    d = ImageDraw.Draw(img)
    d.text((1260, 150), "Dailies Reset Timer", font=font(60), fill="white", anchor="rm")
    d.text((1262, 225), "Reset and restock countdowns", font=font(28, light=True), fill=GOLD, anchor="rm")
    save(img, "header.jpg")


def banner():
    """GitHub README banner, 1600x320 (5:1): the cover's title and timer cards in one strip."""
    bw, bh = 1600, 320
    bg = frame("board_short").filter(ImageFilter.GaussianBlur(6))
    img = Image.eval(bg.crop((0, (H - round(W * bh / bw)) // 2, W, (H + round(W * bh / bw)) // 2)).resize((bw, bh), Image.LANCZOS),
                     lambda v: int(v * 0.42))
    d = ImageDraw.Draw(img)
    d.text((bw // 2, 52), "Dailies Reset Timer", font=font(58), fill="white", anchor="mm")
    d.text((bw // 2, 104), "Reset and restock countdowns for No Rest for the Wicked", font=font(24, light=True),
           fill=GOLD, anchor="mm")
    cards = [
        ("Bounty board", zoom("board_short", BOARD_DAILY["board_short"], 1.45)),
        ("Quests menu", zoom("quests_short", QUESTS_TIMER["quests_short"], 1.6)),
        ("Vendors", zoom("shop_short", SHOP_TIMER["shop_short"], 1.6)),
    ]
    gap = 44
    total = sum(c.width for _, c in cards) + gap * (len(cards) - 1)
    x = (bw - total) // 2
    ch = max(c.height for _, c in cards)
    top = 138
    for label, c in cards:
        shadowed_paste(img, c, (x, top + (ch - c.height) // 2))
        d.text((x + c.width // 2, top + ch + 22), label, font=font(20, light=True), fill=(200, 200, 200), anchor="mm")
        x += c.width + gap
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(HERE, "banner.jpg"), quality=92)
    print(f"{'banner.jpg (docs/pics)':28} {os.path.getsize(os.path.join(HERE, 'banner.jpg')) // 1024:5} KB")


if __name__ == "__main__":
    for f in (cover, board, quests, shop, settings, full_time, header, banner):
        f()
