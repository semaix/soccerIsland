#!/usr/bin/env python3
"""
SoccerIsland のドット絵素材を生成する（すべてオリジナル）。
16bit 時代の見下ろし型RPG風：タイル 16x16、キャラ 16x24（4方向×3コマ）。

キャラは「キー色」で描き、Unity 側（CharacterSpriteBuilder）が島民ごとに色を塗り替える。
  S/s: 肌（明/暗）  H/h: 髪  O/o: 服  A: 服のアクセント（ネクタイ・ボタン）
  P/p: ズボン・スカート  B: 靴  C/c: 帽子  K: 輪郭（固定色）  E: 目（固定色）  W: 白（固定色）

使い方: python3 Tools/PixelArt/generate_art.py
出力:   Assets/Resources/Art/*.png
"""
import os
import random
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Art")

# ---------------------------------------------------------------- キー色（C# の CharacterPalette と一致させる）
KEY = {
    "S": (255, 0, 255), "s": (200, 0, 200),
    "H": (0, 255, 0), "h": (0, 170, 0),
    "O": (0, 0, 255), "o": (0, 0, 160),
    "A": (255, 255, 0),
    "P": (0, 255, 255), "p": (0, 160, 160),
    "B": (255, 128, 0),
    "C": (128, 0, 255), "c": (90, 0, 180),
    "K": (24, 20, 32),
    "E": (32, 32, 72),
    "W": (248, 248, 248),
}

def frame(rows):
    img = Image.new("RGBA", (16, 24), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        assert len(row) == 16, (y, row, len(row))
        for x, ch in enumerate(row):
            if ch in KEY:
                img.putpixel((x, y), KEY[ch] + (255,))
    return img

def pad(rows, top=0, total=24):
    return ["." * 16] * top + rows + ["." * 16] * (total - top - len(rows))

def mirror(rows):
    return [r[::-1] for r in rows]

# ---------------------------------------------------------------- 体（頭・胴・脚）
HEAD_DOWN = [
    "................",
    ".....KKKKKK.....",
    "....KSSSSSSK....",
    "...KSSSSSSSSK...",
    "..KSSSSSSSSSSK..",
    "..KSSSSSSSSSSK..",
    "..KSSESSSSESSK..",
    "..KSSESSSSESSK..",
    "..KsSSSSSSSSsK..",
    "...KsSSSSSSsK...",
    "....KKsSSsKK....",
    ".....KKSSKK.....",
]
HEAD_UP = [
    "................",
    ".....KKKKKK.....",
    "....KSSSSSSK....",
    "...KSSSSSSSSK...",
    "..KSSSSSSSSSSK..",
    "..KSSSSSSSSSSK..",
    "..KSSSSSSSSSSK..",
    "..KSSSSSSSSSSK..",
    "..KsSSSSSSSSsK..",
    "...KsSSSSSSsK...",
    "....KKsssssK....",
    ".....KKSSKK.....",
]
HEAD_LEFT = [
    "................",
    "......KKKKK.....",
    ".....KSSSSSK....",
    "....KSSSSSSSK...",
    "...KSSSSSSSSSK..",
    "...KSSSSSSSSSK..",
    "...KSESSSSSSSK..",
    "...KSESSSSSSSK..",
    "..KSSSSSSSSSsK..",
    "...KsSSSSSSsK...",
    "....KKSSSSKK....",
    ".....KKSSKK.....",
]
TORSO_DOWN = [
    "....KOOAAOOK....",
    "...KOOOAAOOOK...",
    "..KOKOOAAOOKOK..",
    "..KOKOOOOOOKOK..",
    "..KSKOOOOOOKSK..",
    "...K.KooooK.K...",
    ".....KPPPPK.....",
]
TORSO_UP = [
    "....KOOOOOOK....",
    "...KOOOOOOOOK...",
    "..KOKOOOOOOKOK..",
    "..KOKOOOOOOKOK..",
    "..KSKOOOOOOKSK..",
    "...K.KooooK.K...",
    ".....KPPPPK.....",
]
TORSO_LEFT = [
    ".....KOOOOK.....",
    "....KOOAOOOK....",
    "....KOKOOOOK....",
    "....KOKOOOOK....",
    "....KSKOOOOK....",
    ".....KKooooK....",
    ".....KPPPPK.....",
]
LEGS_FRONT = [  # 3コマ：立ち・左足・右足
    [".....KPPPPK.....", ".....KPKKPK.....", ".....KPKKPK.....", "....KBBKKBBK....", "....KKK..KKK...."],
    [".....KPPPPK.....", ".....KPKKPK.....", ".....KPKKBK.....", "....KBBKKKK.....", "....KKK........."],
    [".....KPPPPK.....", ".....KPKKPK.....", ".....KBKKPK.....", ".....KKKKBBK....", ".........KKK...."],
]
LEGS_SIDE = [
    [".....KPPPPK.....", ".....KPPPPK.....", ".....KPKKPK.....", "....KBBKKBK.....", "....KKKK.KK....."],
    [".....KPPPPK.....", "....KPPK.KPK....", "...KPPK...KPK...", "...KBBK...KBK...", "...KKK....KKK..."],
    [".....KPPPPK.....", ".....KPPPPK.....", ".....KPPKPK.....", ".....KBBKBK.....", ".....KKKKKK....."],
]

def body_frames():
    frames = {}
    for i in range(3):
        frames[("down", i)] = HEAD_DOWN + TORSO_DOWN + LEGS_FRONT[i]
        frames[("up", i)] = HEAD_UP + TORSO_UP + LEGS_FRONT[i]
        frames[("left", i)] = HEAD_LEFT + TORSO_LEFT + LEGS_SIDE[i]
        frames[("right", i)] = mirror(frames[("left", i)])
    return frames

ORDER = [("down", 0), ("down", 1), ("down", 2), ("left", 0), ("left", 1), ("left", 2),
         ("right", 0), ("right", 1), ("right", 2), ("up", 0), ("up", 1), ("up", 2)]

def sheet(frames):
    img = Image.new("RGBA", (16 * 12, 24), (0, 0, 0, 0))
    for i, key in enumerate(ORDER):
        img.paste(frame(frames[key]), (i * 16, 0))
    return img

def overlay_frames(down, up, left, top=0):
    """髪・帽子など、方向ごとに1枚で全コマ共通の重ね絵。"""
    f = {}
    for i in range(3):
        f[("down", i)] = pad(down, top)
        f[("up", i)] = pad(up, top)
        f[("left", i)] = pad(left, top)
        f[("right", i)] = mirror(pad(left, top))
    return f

# ---------------------------------------------------------------- 髪型
HAIR = {
    "short": (
        [".....KKKKKK.....", "....KHHHHHHK....", "...KHHHHHHHHK...", "..KHHhHHHHhHHK..", "..KHH......HHK..", "..KH........HK.."],
        [".....KKKKKK.....", "....KHHHHHHK....", "...KHHHHHHHHK...", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHhHHHHhHHK..", "..KhHHHHHHHHhK..", "...KhhhhhhhhK..."],
        ["......KKKKK.....", ".....KHHHHHK....", "....KHHHHHHHK...", "...KHHhHHHHHHK..", "...KH.....HHHK..", "...K......HHHK..", "..........HhHK.."],
    ),
    "spiky": (
        ["...K.KK..KK.K...", "..KHKHHKKHHKHK..", "..KHHHHHHHHHHK..", "..KHHhHHHHhHHK..", "..KHH......HHK..", "..KH........HK.."],
        ["...K.KK..KK.K...", "..KHKHHKKHHKHK..", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHhHHHHhHHK..", "..KhHHHHHHHHhK..", "...KhhhhhhhhK..."],
        ["....K.KK.KK.....", "...KHKHHKHHK....", "...KHHHHHHHHK...", "...KHHhHHHHHHK..", "...KH.....HHHK..", "...K......HHHK..", "..........HhHK.."],
    ),
    "long": (
        [".....KKKKKK.....", "....KHHHHHHK....", "...KHHHHHHHHK...", "..KHHhHHHHhHHK..", "..KHH......HHK..", "..KH........HK..", "..KH........HK..", "..KH........HK..", "..KH........HK..", "..KHh......hHK..", "..KK........KK.."],
        [".....KKKKKK.....", "....KHHHHHHK....", "...KHHHHHHHHK...", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHhHHHHhHHK..", "..KHHHHHHHHHHK..", "..KhHHHHHHHHhK..", "..KhhhhhhhhhhK..", "...KKKKKKKKKK..."],
        ["......KKKKK.....", ".....KHHHHHK....", "....KHHHHHHHK...", "...KHHhHHHHHHK..", "...KH.....HHHK..", "...K......HHHK..", "..........HHHK..", "..........HHHK..", "..........HHHK..", "..........HhHK..", "..........KKK..."],
    ),
    "bob": (
        [".....KKKKKK.....", "....KHHHHHHK....", "...KHHHHHHHHK...", "..KHHHHHHHHHHK..", "..KHHH....HHHK..", "..KHH......HHK..", "..KH........HK..", "..KH........HK..", "..KHh......hHK..", "..KK........KK.."],
        [".....KKKKKK.....", "....KHHHHHHK....", "...KHHHHHHHHK...", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHhHHHHhHHK..", "..KhHHHHHHHHhK..", "..KKhhhhhhhhKK.."],
        ["......KKKKK.....", ".....KHHHHHK....", "....KHHHHHHHK...", "...KHHHHHHHHHK..", "...KHH....HHHK..", "...K......HHHK..", "..........HHHK..", "..........HhHK..", "..........KKK..."],
    ),
    "ponytail": (
        [".....KKKKKK.....", "....KHHHHHHK....", "...KHHHHHHHHK...", "..KHHhHHHHhHHK..", "..KHH......HHK..", "..KH........HK.."],
        [".....KKKKKK.....", "....KHHHHHHK....", "...KHHHHHHHHK...", "..KHHHHHHHHHHK..", "..KHHHHHHHHHHK..", "..KHHHHKKHHHHK..", "..KHHHKHHKHHHK..", "..KhHHKHHKHHhK..", "...KhhKHHKhhK...", "......KHHK......", "......KhhK......", ".......KK......."],
        ["......KKKKK.....", ".....KHHHHHK....", "....KHHHHHHHK...", "...KHHhHHHHHHKK.", "...KH.....HHHHHK", "...K......HHHKHK", "..........HhHKhK", "..............KK"],
    ),
    "buzz": (
        [".....KKKKKK.....", "....KhhhhhhK....", "...KhHhHhHhhK..."],
        [".....KKKKKK.....", "....KhhhhhhK....", "...KhHhHhHhhK...", "..KhhhhhhhhhhK..", "..KhHhHhHhHhhK..", "..KhhhhhhhhhhK.."],
        ["......KKKKK.....", ".....KhhhhhK....", "....KhHhHhhhK...", "...Khhhhhhhhh K.".replace(" ", "h")],
    ),
}

# ---------------------------------------------------------------- 帽子
HATS = {
    "cap": (  # 警官・漁師など（つば付き）
        [".....KKKKKK.....", "....KCCCCCCK....", "...KCCCACCCCK...", "..KCCCCCCCCCCK..", "..KKccccccccKK..", ".KccccccccccccK."],
        [".....KKKKKK.....", "....KCCCCCCK....", "...KCCCCCCCCK...", "..KCCCCCCCCCCK..", "..KccccccccccK.."],
        ["......KKKKK.....", ".....KCCCCCK....", "....KCCCCCCCK...", "...KCCCCCCCCCK..", ".KKcccccccccK..."],
    ),
    "helmet": (  # 土木・農家（麦わら）など
        ["....KKKKKKKK....", "...KCCCCCCCCK...", "..KCCCCAACCCCK..", "..KCCCCCCCCCCK..", ".KccccccccccccK."],
        ["....KKKKKKKK....", "...KCCCCCCCCK...", "..KCCCCCCCCCCK..", "..KCCCCCCCCCCK..", ".KccccccccccccK."],
        [".....KKKKKKK....", "....KCCCCCCCK...", "...KCCCCCCCCCK..", "...KCCCCCCCCCK..", "..KccccccccccK.."],
    ),
}

# ---------------------------------------------------------------- 服の重ね絵（スカート・白衣）
SKIRT = {
    "down": ["...K.KPPPPK.K...", "....KPPPPPPK....", "...KPPPPPPPPK...", "...KppppppppK...", ".....KSKKSK.....", ".....KSKKSK.....", "....KBBKKBBK....", "....KKK..KKK...."],
    "left": [".....KKPPPPK....", "....KPPPPPPK....", "...KPPPPPPPPK...", "...KppppppppK...", ".....KSKKSK.....", ".....KSKKSK.....", "....KBBKKBK.....", "....KKKK.KK....."],
}
COAT = {
    "down": ["...K.KOooOK.K...", "....KOPPPPOK....", "....KOPKKPOK....", "....KOPKKPOK...."],
    "left": [".....KKOoooK....", ".....KOPPPOK....", ".....KOPKKPK....", "....KOPPKKPK...."],
}

def lower_overlay(spec, top):
    f = {}
    for i in range(3):
        f[("down", i)] = pad(spec["down"], top)
        f[("up", i)] = pad(spec["down"], top)
        f[("left", i)] = pad(spec["left"], top)
        f[("right", i)] = mirror(pad(spec["left"], top))
    return f

# ---------------------------------------------------------------- タイル
T = 16
TILE_NAMES = [
    "grass", "grass2", "flowers", "path", "sand", "water", "water2", "shore",
    "rock", "tree", "pitch", "pitch_dark", "line_h", "line_v", "goal", "floor",
    "roof_red", "roof_blue", "roof_green", "roof_gray", "roof_brown", "roof_white", "wall", "wall_window",
    "door", "fence", "sign", "lantern", "asphalt", "road_line", "pier", "stone_wall",
    "cross_sign", "stall", "bush", "dark_grass", "bench", "well", "crop", "lamp",
]

def noise_tile(rng, base, var, dots=None):
    img = Image.new("RGBA", (T, T), base + (255,))
    for y in range(T):
        for x in range(T):
            if rng.random() < 0.18:
                d = rng.randint(-var, var)
                c = tuple(max(0, min(255, base[i] + d)) for i in range(3))
                img.putpixel((x, y), c + (255,))
    if dots:
        for color, count in dots:
            for _ in range(count):
                img.putpixel((rng.randrange(T), rng.randrange(T)), color + (255,))
    return img

def draw_rect(img, x0, y0, x1, y1, color):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= x < T and 0 <= y < T:
                img.putpixel((x, y), color + (255,))

def roof(rng, base):
    img = Image.new("RGBA", (T, T), base + (255,))
    dark = tuple(int(c * 0.7) for c in base)
    light = tuple(min(255, int(c * 1.15) + 10) for c in base)
    for y in range(T):
        for x in range(T):
            if y % 4 == 3: img.putpixel((x, y), dark + (255,))
            elif (x + (y // 4) * 4) % 8 == 0: img.putpixel((x, y), dark + (255,))
            elif y % 4 == 0: img.putpixel((x, y), light + (255,))
    return img

def make_tiles():
    rng = random.Random(20261117)
    tiles = {}
    G = (86, 150, 72)
    tiles["grass"] = noise_tile(rng, G, 14)
    tiles["grass2"] = noise_tile(rng, G, 14, [((60, 120, 52), 10), ((120, 180, 96), 6)])
    fl = noise_tile(rng, G, 12)
    for _ in range(5):
        x, y = rng.randrange(1, 15), rng.randrange(1, 15)
        col = rng.choice([(240, 240, 120), (240, 140, 160), (250, 250, 250)])
        fl.putpixel((x, y), col + (255,)); fl.putpixel((x, y + 1), (70, 120, 60, 255))
    tiles["flowers"] = fl
    tiles["path"] = noise_tile(rng, (186, 156, 110), 16, [((150, 120, 84), 8)])
    tiles["sand"] = noise_tile(rng, (232, 214, 160), 10, [((210, 190, 140), 6), ((250, 240, 200), 4)])
    for name, phase in (("water", 0), ("water2", 3)):
        w = Image.new("RGBA", (T, T), (64, 128, 200, 255))
        for y in range(T):
            for x in range(T):
                if (x + y * 2 + phase) % 11 == 0 or (x * 3 + y + phase) % 17 == 0:
                    w.putpixel((x, y), (150, 200, 240, 255))
                elif (x + y + phase) % 7 == 0:
                    w.putpixel((x, y), (52, 108, 180, 255))
        tiles[name] = w
    sh = tiles["water"].copy()
    for y in range(6):
        for x in range(T):
            sh.putpixel((x, y), (232, 214, 160, 255) if y < 4 else (240, 248, 255, 255))
    tiles["shore"] = sh
    r = noise_tile(rng, (120, 110, 104), 18)
    for x in range(T):
        r.putpixel((x, 0), (160, 150, 140, 255)); r.putpixel((x, 15), (60, 54, 52, 255))
    for y in range(0, T, 5):
        for x in range(T):
            if rng.random() < 0.6: r.putpixel((x, y), (80, 72, 70, 255))
    tiles["rock"] = r
    tr = tiles["grass"].copy()
    for y in range(T):
        for x in range(T):
            dx, dy = x - 7.5, y - 6.5
            if dx * dx + dy * dy < 44:
                c = (44, 104, 52) if (x + y) % 3 else (64, 132, 64)
                if dx < -2 and dy < -2: c = (96, 160, 80)
                tr.putpixel((x, y), c + (255,))
    draw_rect(tr, 7, 12, 8, 15, (110, 76, 44))
    tiles["tree"] = tr
    tiles["pitch"] = noise_tile(rng, (92, 168, 76), 6)
    tiles["pitch_dark"] = noise_tile(rng, (80, 152, 66), 6)
    lh = tiles["pitch"].copy(); draw_rect(lh, 0, 7, 15, 8, (240, 240, 240)); tiles["line_h"] = lh
    lv = tiles["pitch"].copy(); draw_rect(lv, 7, 0, 8, 15, (240, 240, 240)); tiles["line_v"] = lv
    g = tiles["pitch"].copy()
    draw_rect(g, 0, 2, 15, 3, (245, 245, 245))
    for y in range(4, 16):
        for x in range(T):
            if (x + y) % 3 == 0: g.putpixel((x, y), (210, 210, 220, 255))
    draw_rect(g, 0, 2, 1, 15, (245, 245, 245)); draw_rect(g, 14, 2, 15, 15, (245, 245, 245))
    tiles["goal"] = g
    f = Image.new("RGBA", (T, T), (176, 124, 76, 255))
    for y in range(T):
        for x in range(T):
            if y % 4 == 0 or (x + (y // 4) * 6) % 16 == 0: f.putpixel((x, y), (136, 92, 56, 255))
    tiles["floor"] = f
    tiles["roof_red"] = roof(rng, (184, 64, 56))
    tiles["roof_blue"] = roof(rng, (64, 96, 168))
    tiles["roof_green"] = roof(rng, (72, 132, 88))
    tiles["roof_gray"] = roof(rng, (120, 124, 136))
    tiles["roof_brown"] = roof(rng, (128, 88, 56))
    tiles["roof_white"] = roof(rng, (216, 216, 208))
    wl = noise_tile(rng, (232, 220, 192), 6)
    draw_rect(wl, 0, 15, 15, 15, (150, 130, 110))
    tiles["wall"] = wl
    ww = wl.copy(); draw_rect(ww, 4, 3, 11, 10, (60, 52, 48)); draw_rect(ww, 5, 4, 10, 9, (120, 176, 220)); draw_rect(ww, 7, 4, 8, 9, (60, 52, 48)); draw_rect(ww, 5, 6, 10, 6, (60, 52, 48))
    tiles["wall_window"] = ww
    d = wl.copy(); draw_rect(d, 3, 2, 12, 15, (60, 40, 30)); draw_rect(d, 4, 3, 11, 15, (130, 84, 52)); d.putpixel((10, 9), (240, 210, 90, 255))
    tiles["door"] = d
    fe = tiles["grass"].copy()
    draw_rect(fe, 0, 6, 15, 7, (200, 170, 120)); draw_rect(fe, 0, 11, 15, 12, (200, 170, 120))
    for x in (1, 7, 13): draw_rect(fe, x, 3, x + 1, 14, (160, 124, 80))
    tiles["fence"] = fe
    sg = tiles["grass"].copy(); draw_rect(sg, 2, 2, 13, 9, (90, 60, 36)); draw_rect(sg, 3, 3, 12, 8, (196, 160, 104)); draw_rect(sg, 7, 10, 8, 15, (90, 60, 36))
    tiles["sign"] = sg
    la = tiles["path"].copy(); draw_rect(la, 7, 6, 8, 15, (60, 40, 30)); draw_rect(la, 5, 1, 10, 6, (220, 60, 50)); draw_rect(la, 6, 2, 9, 5, (255, 200, 120))
    tiles["lantern"] = la
    tiles["asphalt"] = noise_tile(rng, (92, 92, 100), 8)
    rl = tiles["asphalt"].copy(); draw_rect(rl, 0, 7, 5, 8, (230, 220, 120)); draw_rect(rl, 10, 7, 15, 8, (230, 220, 120))
    tiles["road_line"] = rl
    pi = Image.new("RGBA", (T, T), (150, 110, 70, 255))
    for y in range(T):
        for x in range(T):
            if y % 4 == 0: pi.putpixel((x, y), (96, 66, 40, 255))
    tiles["pier"] = pi
    sw = Image.new("RGBA", (T, T), (128, 128, 136, 255))
    for y in range(T):
        for x in range(T):
            if y % 5 == 4 or (x + (y // 5) * 5) % 10 == 0: sw.putpixel((x, y), (84, 84, 96, 255))
    tiles["stone_wall"] = sw
    cs = Image.new("RGBA", (T, T), (240, 240, 240, 255)); draw_rect(cs, 6, 2, 9, 13, (210, 40, 40)); draw_rect(cs, 2, 6, 13, 9, (210, 40, 40))
    tiles["cross_sign"] = cs
    st = tiles["path"].copy()
    for x in range(T): draw_rect(st, x, 0, x, 5, (220, 60, 50) if (x // 2) % 2 == 0 else (250, 250, 240))
    draw_rect(st, 1, 6, 14, 12, (150, 100, 60)); draw_rect(st, 2, 7, 13, 9, (240, 210, 140))
    tiles["stall"] = st
    bu = tiles["grass"].copy()
    for y in range(3, 15):
        for x in range(1, 15):
            if (x - 7.5) ** 2 / 49 + (y - 9) ** 2 / 36 < 1: bu.putpixel((x, y), ((52, 116, 56) if (x * y) % 5 else (80, 150, 70)) + (255,))
    tiles["bush"] = bu
    tiles["dark_grass"] = noise_tile(rng, (64, 120, 60), 10)
    be = tiles["path"].copy(); draw_rect(be, 1, 5, 14, 8, (140, 96, 56)); draw_rect(be, 2, 9, 3, 13, (90, 60, 36)); draw_rect(be, 12, 9, 13, 13, (90, 60, 36))
    tiles["bench"] = be
    we = tiles["path"].copy()
    for y in range(T):
        for x in range(T):
            dd = (x - 7.5) ** 2 + (y - 7.5) ** 2
            if dd < 40: we.putpixel((x, y), (130, 130, 140, 255))
            if dd < 18: we.putpixel((x, y), (50, 90, 150, 255))
    tiles["well"] = we
    cr = Image.new("RGBA", (T, T), (120, 84, 52, 255))
    for y in range(0, T, 4):
        for x in range(T):
            cr.putpixel((x, y), (92, 62, 38, 255))
            if x % 3 == 1: cr.putpixel((x, y + 1 if y + 1 < T else y), (110, 170, 70, 255))
    tiles["crop"] = cr
    lp = tiles["asphalt"].copy(); draw_rect(lp, 7, 4, 8, 15, (50, 50, 60)); draw_rect(lp, 5, 1, 10, 4, (250, 240, 170))
    tiles["lamp"] = lp

    cols = 8
    rows = (len(TILE_NAMES) + cols - 1) // cols
    sheet_img = Image.new("RGBA", (cols * T, rows * T), (0, 0, 0, 0))
    for i, name in enumerate(TILE_NAMES):
        sheet_img.paste(tiles[name], ((i % cols) * T, (i // cols) * T))
    return sheet_img

def main():
    os.makedirs(OUT, exist_ok=True)
    sheet(body_frames()).save(os.path.join(OUT, "char_body.png"))
    for name, (d, u, l) in HAIR.items():
        sheet(overlay_frames(d, u, l)).save(os.path.join(OUT, f"char_hair_{name}.png"))
    for name, (d, u, l) in HATS.items():
        sheet(overlay_frames(d, u, l)).save(os.path.join(OUT, f"char_hat_{name}.png"))
    sheet(lower_overlay(SKIRT, 16)).save(os.path.join(OUT, "char_skirt.png"))
    sheet(lower_overlay(COAT, 17)).save(os.path.join(OUT, "char_coat.png"))
    make_tiles().save(os.path.join(OUT, "tiles.png"))
    with open(os.path.join(OUT, "tiles.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(TILE_NAMES) + "\n")
    print("generated:", sorted(os.listdir(OUT)))

if __name__ == "__main__":
    main()
