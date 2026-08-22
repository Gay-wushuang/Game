"""SVG text -> outlined path converter (for Godot ThorVG import compatibility).
Usage: python svg_text_to_path.py <in.svg> <out.svg>
Fonts are substituted: AdobeSongStd-Light-GBpc-EUC-H -> SimSun, Arial-BoldMT -> Arial Bold.
"""
import re
import sys

from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen

FONT_RULES = [
    ("yahei", r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\msyhbd.ttc"),
    ("arial", r"C:\Windows\Fonts\arial.ttf", r"C:\Windows\Fonts\arialbd.ttf"),
    ("song", r"C:\Windows\Fonts\simsun.ttc", None),
]
DEFAULT_FONT = r"C:\Windows\Fonts\simsun.ttc"


def load_style_classes(style_block: str) -> dict:
    classes = {}
    for m in re.finditer(r"\.(st\d+)\{([^}]+)\}", style_block):
        attrs = {}
        for k, v in re.findall(r"([\w-]+)\s*:\s*([^;]+);?", m.group(2)):
            attrs[k] = v.strip()
        classes[m.group(1)] = attrs
    return classes


def parse_matrix(t: str):
    if not t:
        return 1.0, 0.0, 0.0, 1.0, 0.0, 0.0
    m = re.search(r"matrix\(([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\)", t)
    if not m:
        return 1.0, 0.0, 0.0, 1.0, 0.0, 0.0
    return tuple(float(v) for v in m.groups())


def pick_font(family: str, bold: bool = False) -> str:
    fam = family.lower()
    if "bold" in fam:
        bold = True
    for key, normal, bold_path in FONT_RULES:
        if key in fam:
            return bold_path if (bold and bold_path) else normal
    return DEFAULT_FONT


def text_width(content, family, size, a, font):
    glyf = font.getGlyphSet()
    cmap = font.getBestCmap()
    upem = font["head"].unitsPerEm
    unit = size / upem
    w = 0.0
    for ch in content:
        if ch == " ":
            gname = cmap.get(0x20)
            if gname: w += glyf[gname].width * unit * a
            continue
        gname = cmap.get(ord(ch), ".notdef")
        w += glyf[gname].width * unit * a
    return w


def transform_d(raw_d: str, cx: float, cy: float, x_scale: float, y_scale: float, unit: float) -> str:
    """Scale font-unit coordinates into viewBox coordinates.
    SVG y grows downward; font glyphs grow upward, so flip y around the baseline.
    Handles H (single x) and V (single y) commands correctly."""
    tokens = re.findall(r"[A-Za-z]|-?\d+\.?\d*(?:[eE][-+]?\d+)?", raw_d)
    out = []
    cmd = None
    i = 0
    while i < len(tokens):
        tok = tokens[i]
        if tok.isalpha():
            cmd = tok
            out.append(tok)
            i += 1
            continue
        nums = []
        while i < len(tokens) and not tokens[i].isalpha():
            nums.append(float(tokens[i]))
            i += 1
        parts = []
        k = 0
        while k < len(nums):
            if cmd in ("H", "h"):
                parts.append(f"{cx + nums[k] * unit * x_scale:.3f}")
                k += 1
            elif cmd in ("V", "v"):
                parts.append(f"{cy - nums[k] * unit * y_scale:.3f}")
                k += 1
            else:
                parts.append(f"{cx + nums[k] * unit * x_scale:.3f}")
                parts.append(f"{cy - nums[k + 1] * unit * y_scale:.3f}")
                k += 2
        out.append(" ".join(parts))
    return "".join(out)


class TextConverter:
    def __init__(self):
        self._font_cache = {}

    def get_font(self, path: str) -> TTFont:
        if path not in self._font_cache:
            self._font_cache[path] = TTFont(path, fontNumber=0)
        return self._font_cache[path]


def main():
    if len(sys.argv) != 3:
        print("usage: python svg_text_to_path.py <in.svg> <out.svg>")
        sys.exit(1)
    src, dst = sys.argv[1], sys.argv[2]
    with open(src, encoding="utf-8") as f:
        doc = f.read()
    style_m = re.search(r"<style[^>]*>(.*?)</style>", doc, re.S)
    classes = load_style_classes(style_m.group(1)) if style_m else {}

    def attr(class_list, name):
        for c in class_list:
            if c in classes and name in classes[c]:
                return classes[c][name]
        return None

    def replace_text(m):
        props = {}
        class_list = re.findall(r"st\d+", m.group("attrs") or "")
        family = attr(class_list, "font-family") or "SimSun"
        size_t = attr(class_list, "font-size") or "16px"
        weight = attr(class_list, "font-weight") or ""
        props["family"] = family
        props["size"] = float(size_t.replace("px", ""))
        props["bold"] = "bold" in weight.lower()
        fill_m = re.search(r"fill=\"([^\"]+)\"", m.group("attrs") or "")
        fill = fill_m.group(1) if fill_m else (attr(class_list, "fill") or "#FFFFFF")
        tr_m = re.search(r"transform=\"([^\"]+)\"", m.group("attrs") or "")
        a, b, c, d, e, f = parse_matrix(tr_m.group(1) if tr_m else None)
        x_attr = re.search(r"x=\"([-\d.]+)\"", m.group("attrs") or "")
        y_attr = re.search(r"y=\"([-\d.]+)\"", m.group("attrs") or "")
        cx = e + (float(x_attr.group(1)) if x_attr else 0.0)
        cy = f + (float(y_attr.group(1)) if y_attr else 0.0)
        anchor_m = re.search(r"text-anchor=\"([^\"]+)\"", m.group("attrs") or "")
        anchor = anchor_m.group(1) if anchor_m else "start"
        content = m.group("content")
        if anchor != "start" and content.strip():
            font = conv.get_font(pick_font(props["family"], props["bold"]))
            tw = text_width(content, props["family"], props["size"], a, font)
            cx -= tw / 2 if anchor == "middle" else tw
        p = render_text(content, props["family"], props["size"], a, d, cx, cy, props["bold"])
        return f'<path d="{p}" fill="{fill}" fill-rule="evenodd"/>'


    def render_text(content, family, size, a, d, e, f, bold=False):
        font = conv.get_font(pick_font(family, bold))
        glyf = font.getGlyphSet()
        cmap = font.getBestCmap()
        upem = font["head"].unitsPerEm
        unit = size / upem
        pen = SVGPathPen(glyf)
        x = e
        paths = []
        for ch in content:
            if ch == " ":
                gname = cmap.get(0x20)
                if gname: x += glyf[gname].width * unit * a
                continue
            gname = cmap.get(ord(ch), ".notdef")
            pen._commands = []
            glyf[gname].draw(pen)
            raw = pen.getCommands()
            if raw.strip():
                paths.append(transform_d(raw, x, f, a, d, unit))
            x += glyf[gname].width * unit * a
        return "".join(paths)

    conv = TextConverter()
    out = re.sub(
        r"<text(?P<attrs>[^>]*)>(?P<content>[^<]*)</text>",
        replace_text,
        doc,
        flags=re.S,
    )
    with open(dst, "w", encoding="utf-8", newline="\n") as f:
        f.write(out)
    print("outlined:", dst)


if __name__ == "__main__":
    main()