"""纯标准库生成 NetConfigPro 应用图标：深蓝底 + 青色网络节点发光图。"""
import math
import os
import struct
import zlib

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "app.ico")
SIZES = [16, 24, 32, 48, 64, 128, 256]
BASE = 256

# 归一化坐标（0~1）下的拓扑：中心 + 三个外围节点
NODES = [
    (0.500, 0.460, 0.100, (0, 229, 255)),    # 中心：青
    (0.262, 0.690, 0.068, (77, 208, 225)),   # 左下
    (0.738, 0.690, 0.068, (77, 208, 225)),   # 右下
    (0.500, 0.196, 0.068, (124, 92, 255)),   # 顶部：紫
]
EDGES = [(0, 1), (0, 2), (0, 3), (1, 2)]


def smoothstep(edge0: float, edge1: float, x: float) -> float:
    t = max(0.0, min(1.0, (x - edge0) / (edge1 - edge0)))
    return t * t * (3 - 2 * t)


def dist_to_segment(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    seg_len2 = dx * dx + dy * dy
    if seg_len2 == 0:
        return math.hypot(px - ax, py - ay)
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / seg_len2))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def rounded_rect_alpha(x, y, size, radius_ratio=0.22):
    r = size * radius_ratio
    cx = min(max(x, r), size - r)
    cy = min(max(y, r), size - r)
    d = math.hypot(x - cx, y - cy)
    return 1.0 - smoothstep(r - 1.0, r, d)


def render(size: int) -> bytes:
    rows = []
    aa = 1.2  # 抗锯齿半径

    for py in range(size):
        row = bytearray()
        for px in range(size):
            x, y = px + 0.5, py + 0.5
            nx, ny = x / size, y / size

            # 背景：斜向渐变 + 圆角遮罩
            t = (nx * 0.6 + ny * 0.4)
            bg = (
                int(10 + 12 * t),
                int(22 + 30 * t),
                int(38 + 48 * t),
            )
            alpha = rounded_rect_alpha(x, y, size)

            r, g, b = bg

            # 中心柔光
            glow = math.exp(-((nx - 0.5) ** 2 + (ny - 0.48) ** 2) / 0.075)
            r += int(18 * glow)
            g += int(52 * glow)
            b += int(70 * glow)

            # 连线
            for a, bidx in EDGES:
                ax, ay, ar, ac = NODES[a]
                bx, by, br, bc = NODES[bidx]
                d = dist_to_segment(nx, ny, ax, ay, bx, by)
                w = 0.013
                line_a = (1.0 - smoothstep(w, w + 0.012, d)) * 0.95
                line_a += math.exp(-(d * d) / 0.0016) * 0.5
                line_a = min(line_a, 1.0) * alpha
                if line_a > 0:
                    r = int(r * (1 - line_a) + ac[0] * line_a)
                    g = int(g * (1 - line_a) + ac[1] * line_a)
                    b = int(b * (1 - line_a) + ac[2] * line_a)

            # 节点（含外发光）
            for cx, cy, cr, cc in NODES:
                d = math.hypot(nx - cx, ny - cy)
                halo = math.exp(-((d - cr) ** 2) / 0.0032) * 0.85
                core = 1.0 - smoothstep(cr - 0.012, cr + 0.012, d)
                na = min(1.0, core + halo) * alpha
                if na <= 0:
                    continue
                # 节点内部带轻微高光，增强立体感
                hi = math.exp(-(((nx - cx + cr * 0.3) ** 2) + ((ny - cy + cr * 0.3) ** 2)) / 0.004) * 0.35
                mix_r = min(255, int(cc[0] + (255 - cc[0]) * hi))
                mix_g = min(255, int(cc[1] + (255 - cc[1]) * hi))
                mix_b = min(255, int(cc[2] + (255 - cc[2]) * hi))
                r = int(r * (1 - na) + mix_r * na)
                g = int(g * (1 - na) + mix_g * na)
                b = int(b * (1 - na) + mix_b * na)

            row += bytes((max(0, min(255, r)),
                          max(0, min(255, g)),
                          max(0, min(255, b)),
                          int(alpha * 255)))
        rows.append(bytes(row))
    return b"".join(b"\x00" + r for r in rows)


def png_bytes(size: int, raw: bytes) -> bytes:
    def chunk(tag: bytes, data: bytes) -> bytes:
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


def downscale(raw: bytes, src: int, dst: int) -> bytes:
    """按最近邻缩放 RGBA 像素块，用于生成小尺寸图标。"""
    if src == dst:
        return raw
    src_rows = [raw[i * (src * 4 + 1) + 1:(i + 1) * (src * 4 + 1)] for i in range(src)]
    out = []
    for y in range(dst):
        sy = min(src - 1, int((y + 0.5) * src / dst))
        row = bytearray()
        for x in range(dst):
            sx = min(src - 1, int((x + 0.5) * src / dst))
            row += src_rows[sy][sx * 4:sx * 4 + 4]
        out.append(bytes(row))
    return b"".join(b"\x00" + r for r in out)


def main():
    base_raw = render(BASE)
    images = [(s, png_bytes(s, downscale(base_raw, BASE, s))) for s in SIZES]

    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries, blobs = b"", b""
    for s, data in images:
        entries += struct.pack("<BBBBHHII",
                               s if s < 256 else 0,
                               s if s < 256 else 0,
                               0, 0, 1, 32, len(data), offset)
        blobs += data
        offset += len(data)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as f:
        f.write(header + entries + blobs)
    print(f"icon written: {OUT} ({os.path.getsize(OUT)} bytes, sizes={SIZES})")


if __name__ == "__main__":
    main()
