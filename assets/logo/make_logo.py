"""Popolo ワードマーク (ロゴ) を幾何学的に構成して SVG/PNG を出力する。

依存: pip install shapely matplotlib

単位: 大文字の高さ H=100。
  - 標準版   : 文字は離す。全ての閉じた穴(P,o,p)に切れ目を入れ、浮島を作らない
  - 加工版   : ベースラインの帯で全文字をつなぐ (レーザーカット・3Dプリント・ステンシル向け)
"""
import os
import math
from shapely.geometry import Point, box
from shapely.ops import unary_union
from shapely import affinity
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import PathPatch
from matplotlib.path import Path

OUT = os.path.dirname(os.path.abspath(__file__))  # このスクリプトと同じフォルダに出力
os.makedirs(OUT, exist_ok=True)

H = 100.0      # キャップハイト (P, l)
X = 74.0       # xハイト (o, p)
W = 22.0       # 線の太さ
D = 26.0       # ディセンダ (pの足)
G = 8.0        # 切れ目の幅 (2.5mm高で0.2mm)
R = X / 2      # o の外半径
RES = 64       # 円の分割数


CIRCLES = []   # ロゴに使った円 (cx, cy, r)。DXF で円弧として書き出すために記録する


def circle(cx, cy, r):
    CIRCLES.append((cx, cy, r))
    return Point(cx, cy).buffer(r, RES)


def ring(cx, cy, r_out, r_in):
    return circle(cx, cy, r_out).difference(circle(cx, cy, r_in))


def slit_top(cx, cy, r):
    """円の真上に縦の切れ目"""
    return box(cx - G / 2, cy, cx + G / 2, cy + r + 1)


def letter_P(x):
    stem = box(x, 0, x + W, H)
    bh = 74.0                      # ボウルの高さ
    rb = bh / 2
    cx, cy = x + 42.0, H - rb
    outer = unary_union([box(x, H - bh, cx, H), circle(cx, cy, rb)])
    inner = unary_union([box(x + W, H - bh + W, cx, H - W), circle(cx, cy, rb - W)])
    bowl = outer.difference(inner)
    # 切れ目はボウル下側の縦棒との付け根 (上側だと頭の横線が途切れて見えるため)
    slit = box(x + W, H - bh - 1, x + W + G, H - bh + W + 1)
    g = unary_union([stem, bowl]).difference(slit)
    return g, cx + rb               # (形, 右端)


def letter_o(x):
    cx, cy = x + R, R
    g = ring(cx, cy, R, R - W).difference(slit_top(cx, cy, R))
    return g, x + 2 * R


def letter_p(x):
    stem = box(x, -D, x + W, X)
    cx, cy = x + R, R
    bowl = ring(cx, cy, R, R - W)
    # 縦棒の上端と丸の外周の間にできる小さな段差を埋めて、肩を平らにする
    # (段差が残ると、CAD で押し出したときに極小の面が作れず、立体が閉じないことがある)
    shoulder = box(x + W - 0.01, cy, cx, X).difference(circle(cx, cy, R))
    g = unary_union([stem, bowl, shoulder]).difference(slit_top(cx, cy, R))
    return g, x + 2 * R


def letter_l(x):
    return box(x, 0, x + W, H), x + W


GLYPHS = {"P": letter_P, "o": letter_o, "p": letter_p, "l": letter_l}


def word(text, spacing):
    shapes, x = [], 0.0
    for ch in text:
        g, right = GLYPHS[ch](x)
        shapes.append(g)
        x = right + spacing
    return shapes, x - spacing


SP = 8.0       # 字間

def variant_standard():
    shapes, w = word("Popolo", SP)
    return unary_union(shapes)


def variant_baseline():
    shapes, w = word("Popolo", SP)
    bar = box(0, -10, w, 0.5)       # ベースラインの帯
    g = unary_union(shapes + [bar])
    # 帯と文字の間にできる小さな閉じた隙間は埋める (切り抜くと抜け落ちるため)
    from shapely.geometry import Polygon
    return unary_union([Polygon(q.exterior) for q in polys(g)])


def polys(geom):
    return list(geom.geoms) if hasattr(geom, "geoms") else [geom]


def to_svg(geom, path, fill="#000000", bg=None, pad=10.0, cap_mm=None):
    """cap_mm を指定すると、大文字の高さがその寸法 [mm] になる実寸SVGを出力する (基板シルク用)"""
    minx, miny, maxx, maxy = geom.bounds
    w, h = maxx - minx + 2 * pad, maxy - miny + 2 * pad
    def tr(px, py):  # y を反転
        return px - minx + pad, maxy - py + pad
    d = []
    for p in polys(geom):
        for ringc in [p.exterior] + list(p.interiors):
            pts = [tr(*c) for c in ringc.coords]
            d.append("M" + " L".join(f"{a:.2f},{b:.2f}" for a, b in pts) + " Z")
    bg_rect = f'<rect width="100%" height="100%" fill="{bg}"/>' if bg else ""
    k = cap_mm / H if cap_mm else None
    size = f'width="{w * k:.3f}mm" height="{h * k:.3f}mm"' if k else f'width="{w:.2f}" height="{h:.2f}"'
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w:.2f} {h:.2f}" '
           f'{size}>{bg_rect}'
           f'<path fill="{fill}" fill-rule="evenodd" d="{" ".join(d)}"/></svg>\n')
    open(path, "w", encoding="utf-8").write(svg)


def draw(ax, geom, fg, bg, title):
    ax.set_facecolor(bg)
    for p in polys(geom):
        verts, codes = [], []
        for ringc in [p.exterior] + list(p.interiors):
            cs = list(ringc.coords)
            verts += cs
            codes += [Path.MOVETO] + [Path.LINETO] * (len(cs) - 2) + [Path.CLOSEPOLY]
        ax.add_patch(PathPatch(Path(verts, codes), facecolor=fg, edgecolor="none"))
    minx, miny, maxx, maxy = geom.bounds
    ax.set_xlim(minx - 12, maxx + 12)
    ax.set_ylim(miny - 12, maxy + 12)
    ax.set_aspect("equal")
    ax.set_xticks([]); ax.set_yticks([])
    ax.set_title(title, fontsize=10, loc="left")


variants = [("standard", variant_standard(), "Standard (print / PCB silkscreen)"),
            ("connected", variant_baseline(), "Connected: baseline bar (laser cut / 3D print / stencil)")]

fig, axes = plt.subplots(len(variants), 2, figsize=(12, 5))
for i, (name, g, title) in enumerate(variants):
    to_svg(g, os.path.join(OUT, f"popolo_{name}.svg"))
    to_svg(g, os.path.join(OUT, f"popolo_{name}_white.svg"), fill="#ffffff")
    if name == "standard":
        # 基板シルク用の実寸版 (大文字の高さ 2.5mm, 余白なし)
        to_svg(g, os.path.join(OUT, "popolo_silk_2.5mm.svg"), pad=0.0, cap_mm=2.5)
    n = len(polys(g))
    draw(axes[i][0], g, "black", "white", f"{title}   [{n} piece(s)]")
    draw(axes[i][1], g, "white", "#14532d", "")
plt.tight_layout()
plt.savefig(os.path.join(OUT, "popolo_logo_preview.png"), dpi=110)

for name, g, _ in variants:
    print(name, "pieces:", len(polys(g)), "holes:", sum(len(p.interiors) for p in polys(g)),
          "size(units):", [round(v, 1) for v in g.bounds])


# ---------------------------------------------------------------- 立体の盛り上げ用 (ケースの天板など)
def clean(poly, tol=0.02):
    """CAD で押し出しやすいよう、極短の線分や一直線上の点を取り除く (単位: H=100 の座標)"""
    return poly.simplify(tol, preserve_topology=True)


def arc_runs(pts):
    """閉じた点列の各線分が、ロゴのどの円の弦か (円の番号, 円でなければ None) を返す"""
    n = len(pts)
    seg = []
    for i in range(n):
        (ax, ay), (bx, by) = pts[i], pts[(i + 1) % n]
        hit = None
        for ci, (cx, cy, r) in enumerate(CIRCLES):
            tol = r * (1 - math.cos(math.pi / RES)) + 1e-6      # 多角形の弦と円のずれ
            chord = 2 * r * math.sin(math.pi / RES) * 1.001 + 1e-6
            if (abs(math.hypot(ax - cx, ay - cy) - r) <= tol and abs(math.hypot(bx - cx, by - cy) - r) <= tol
                    and math.hypot(bx - ax, by - ay) <= chord):
                hit = ci
                break
        seg.append(hit)
    return seg


def to_dxf(geom, path, scale):
    """1文字ずつ閉じたポリライン (R12 の POLYLINE) で DXF を書く。中心を原点に置く。単位 mm
    円の部分は多角形ではなく円弧 (bulge) で書く。CAD で押し出したときに極小の面ができないようにするため"""
    minx, miny, maxx, maxy = geom.bounds
    ox, oy = (minx + maxx) / 2, (miny + maxy) / 2
    out = ["0", "SECTION", "2", "ENTITIES"]
    stats = {"line": [], "arc": 0}
    for p in polys(geom):
        for ringc in [p.exterior] + list(p.interiors):
            pts = list(ringc.coords)[:-1]
            seg = arc_runs(pts)
            # 円弧の途中から書き始めないよう、直線と円弧の境目から始める
            n = len(pts)
            s0 = next((i for i in range(n) if seg[i] != seg[i - 1]), 0)
            pts, seg = pts[s0:] + pts[:s0], seg[s0:] + seg[:s0]
            verts, i = [], 0
            while i < n:
                if seg[i] is None:
                    verts.append((pts[i], 0.0))
                    a, b = pts[i], pts[(i + 1) % n]
                    stats["line"].append(math.hypot(b[0] - a[0], b[1] - a[1]) * scale)
                    i += 1
                    continue
                cx, cy, r = CIRCLES[seg[i]]
                sweep, j = 0.0, i
                while j < n and seg[j] == seg[i] and abs(sweep) < math.pi / 2:   # 1つの円弧は90°まで
                    a, b = pts[j], pts[(j + 1) % n]
                    sweep += math.atan2((a[0] - cx) * (b[1] - cy) - (a[1] - cy) * (b[0] - cx),
                                        (a[0] - cx) * (b[0] - cx) + (a[1] - cy) * (b[1] - cy))
                    j += 1
                verts.append((pts[i], math.tan(sweep / 4)))
                stats["arc"] += 1
                i = j
            out += ["0", "POLYLINE", "8", "LOGO", "66", "1", "70", "1"]
            for (x, y), bulge in verts:
                out += ["0", "VERTEX", "8", "LOGO", "10", "%.5f" % ((x - ox) * scale), "20", "%.5f" % ((y - oy) * scale), "30", "0.0"]
                if bulge:
                    out += ["42", "%.8f" % bulge]
            out += ["0", "SEQEND", "8", "LOGO"]
    out += ["0", "ENDSEC", "0", "EOF"]
    open(path, "w", encoding="ascii").write("\n".join(out) + "\n")
    return stats


EMBOSS_CAP_MM = 4.8      # 大文字の高さ [mm] (C-Logger の天板: ロゴ全体の幅 約21mm)
# ロゴの比率は変えない (切れ目も含めて標準版をそのまま縮小する)
emb = unary_union([clean(q) for q in word("Popolo", SP)[0]])
k = EMBOSS_CAP_MM / H
EMBOSS_SLIT_MM = G * k
# DXF は円を円弧で書くので、簡略化していない形から作る
# (simplify は一直線上に残った余分な点だけを取り除く。形は変わらない)
st = to_dxf(unary_union(word("Popolo", SP)[0]).simplify(1e-6), os.path.join(OUT, "popolo_emboss_%.1fmm.dxf" % EMBOSS_CAP_MM), k)
print("emboss DXF: arcs %d, lines %d, shortest line %.3f mm" % (st["arc"], len(st["line"]), min(st["line"])))
to_svg(emb, os.path.join(OUT, "popolo_emboss_%.1fmm.svg" % EMBOSS_CAP_MM), pad=0.0, cap_mm=EMBOSS_CAP_MM)
fig, ax = plt.subplots(figsize=(6, 1.8))
draw(ax, emb, "black", "white", "Emboss version: cap %.1f mm, slit %.1f mm, stroke %.2f mm"
     % (EMBOSS_CAP_MM, EMBOSS_SLIT_MM, W * k))
plt.tight_layout()
plt.savefig(os.path.join(OUT, "popolo_emboss_preview.png"), dpi=110)
seg_min = min(min(((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5 for a, b in zip(r.coords[:-1], r.coords[1:]))
              for p in polys(emb) for r in [p.exterior] + list(p.interiors)) * k
print("emboss pieces:", len(polys(emb)), "holes:", sum(len(p.interiors) for p in polys(emb)),
      "width %.2f mm" % ((emb.bounds[2] - emb.bounds[0]) * k), "min segment %.3f mm" % seg_min)
