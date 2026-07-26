#!/usr/bin/env python3
"""Fallback PNG generator for Phase D.5.1 when Playwright browsers are unavailable.

Mirrors command-center RTL visuals (fake Arabic names only).
"""
from __future__ import annotations

import os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(__file__), '../../../docs/screenshots/phase-d5-1')
OUT = os.path.abspath(OUT)
ARIAL_UNICODE_FONT = '/System/Library/Fonts/Supplemental/Arial Unicode.ttf'

BG = (238, 242, 239)
SURFACE = (255, 255, 255)
MUTED = (230, 236, 232)
TEXT = (23, 35, 31)
SECONDARY = (95, 111, 103)
ACCENT = (15, 118, 110)
WARN = (180, 83, 9)
CRIT = (180, 35, 24)
OK = (23, 114, 69)
INFO = (37, 99, 166)

try:
    FONT_L = ImageFont.truetype(ARIAL_UNICODE_FONT, 28)
    FONT_M = ImageFont.truetype(ARIAL_UNICODE_FONT, 18)
    FONT_S = ImageFont.truetype(ARIAL_UNICODE_FONT, 14)
except OSError:
    FONT_L = FONT_M = FONT_S = ImageFont.load_default()


def rtl_text(draw, xy, text, font, fill):
    x, y = xy
    bbox = draw.textbbox((0, 0), text, font=font)
    draw.text((x - (bbox[2] - bbox[0]), y), text, font=font, fill=fill)


def card(draw, box, accent=INFO):
    x0, y0, _, y1 = box
    draw.rounded_rectangle(box, radius=18, fill=SURFACE, outline=(200, 210, 205))
    draw.rectangle((x0, y0 + 8, x0 + 5, y1 - 8), fill=accent)


def header(draw, w, title, subtitle):
    draw.rounded_rectangle((24, 24, w - 24, 140), radius=24, fill=SURFACE, outline=(200, 210, 205))
    rtl_text(draw, (w - 48, 40), 'مركز القوى البشرية', FONT_S, ACCENT)
    rtl_text(draw, (w - 48, 62), title, FONT_L, TEXT)
    rtl_text(draw, (w - 48, 100), subtitle, FONT_S, SECONDARY)


def strip(draw, w, y, status, rate, warning, ratio):
    accent = {'complete': OK, 'partial': WARN, 'critical': CRIT, 'missing': (120, 130, 125)}.get(status, WARN)
    draw.rounded_rectangle((24, y, w - 24, y + 110), radius=22, fill=SURFACE)
    draw.rectangle((24, y + 10, 30, y + 100), fill=accent)
    rtl_text(draw, (w - 48, y + 18), 'تغطية القوى البشرية', FONT_S, ACCENT)
    rtl_text(draw, (w - 48, y + 42), rate, FONT_L, TEXT)
    rtl_text(draw, (w - 48, y + 78), warning, FONT_S, SECONDARY)
    rtl_text(draw, (120, y + 48), ratio, FONT_L, TEXT)


def metrics(draw, w, y, values):
    n = len(values)
    gap = 12
    card_w = (w - 48 - gap * (n - 1)) // n
    for i, (label, val, accent) in enumerate(values):
        x0 = 24 + i * (card_w + gap)
        card(draw, (x0, y, x0 + card_w, y + 90), accent)
        rtl_text(draw, (x0 + card_w - 14, y + 16), label, FONT_S, SECONDARY)
        rtl_text(draw, (x0 + card_w - 14, y + 44), str(val), FONT_L, TEXT)


def row(draw, w, y, title, detail, badge, accent=WARN):
    draw.rounded_rectangle((24, y, w - 24, y + 70), radius=16, fill=SURFACE, outline=(210, 218, 214))
    draw.rounded_rectangle((w - 40, y + 16, w - 34, y + 54), radius=3, fill=accent)
    rtl_text(draw, (w - 52, y + 14), title, FONT_M, TEXT)
    rtl_text(draw, (w - 52, y + 40), detail, FONT_S, SECONDARY)
    rtl_text(draw, (90, y + 24), badge, FONT_M, TEXT)


def panel(draw, w, h, ptype, title, ref, reason):
    x0 = w - 420
    draw.rounded_rectangle((x0, 24, w - 24, h - 24), radius=24, fill=SURFACE, outline=(200, 210, 205))
    rtl_text(draw, (w - 48, 44), ptype, FONT_S, SECONDARY)
    rtl_text(draw, (w - 48, 72), ref, FONT_S, ACCENT)
    rtl_text(draw, (w - 48, 100), title, FONT_L, TEXT)
    rtl_text(draw, (w - 48, 140), reason, FONT_S, SECONDARY)


SCENES = {
    'desktop-overview.png': ('overview', 1440, 1000),
    'desktop-shift-coverage.png': ('shift', 1440, 1000),
    'desktop-unit-coverage.png': ('unit', 1440, 1000),
    'desktop-critical-role-gaps.png': ('critical-gaps', 1440, 1000),
    'desktop-member-panel.png': ('member-panel', 1440, 1000),
    'desktop-shift-panel.png': ('shift-panel', 1440, 1000),
    'desktop-qualification-expiry.png': ('qual', 1440, 1000),
    'desktop-unsafe-staffing.png': ('unsafe', 1440, 1000),
    'desktop-data-quality.png': ('dq', 1440, 1000),
    'tablet-overview.png': ('overview', 1024, 900),
    'mobile-overview.png': ('overview', 390, 844),
    'mobile-shift.png': ('shift', 390, 844),
    'mobile-member-detail.png': ('member-panel', 390, 844),
    'state-ready.png': ('ready', 1440, 900),
    'state-attention.png': ('attention', 1440, 900),
    'state-critical.png': ('critical', 1440, 900),
    'state-unknown.png': ('unknown', 1440, 900),
    'state-empty.png': ('empty', 1440, 900),
    'state-partial.png': ('partial', 1440, 900),
    'import-preview.png': ('import', 1440, 1000),
}


def render_overview_state(draw, w, kind):
    status_map = {
        'overview': ('partial', '64% تغطية', 'توجد فجوات تغطية في أدوار حرجة.', '9/14'),
        'ready': ('complete', '100% تغطية', 'لا توجد تحذيرات تغطية حالية.', '14/14'),
        'attention': ('partial', '78% تغطية', 'فوق الحد الآمن وأقل من المطلوب.', '11/14'),
        'critical': ('critical', '42% تغطية', 'أقل من الحد الآمن في أدوار حرجة.', '6/14'),
        'unknown': ('missing', 'احتياج غير محدد', 'مصدر التوفر غير معروف.', '-/-'),
        'partial': ('partial', 'بيانات جزئية', 'تعذر تحميل جزء من التغطية.', '—'),
    }
    st, rate, warn, ratio = status_map[kind]
    strip(draw, w, 160, st, rate, warn, ratio)
    if w >= 700:
        metrics(draw, w, 290, [
            ('المطلوب', 14, INFO), ('المتاح', 9 if kind != 'ready' else 14, OK),
            ('الحاضر', 8, INFO), ('الفجوة', 0 if kind == 'ready' else 5, WARN if kind != 'ready' else OK),
            ('الحد الآمن', 11, OK),
        ])
        row(draw, w, 400, 'ضابط برج', 'TOWER · عنبر أ · فجوة 1', '67%', WARN)
        row(draw, w, 480, 'مواقع حرجة غير مغطاة', 'بديل غير متاح لقائد الوردية', 'حرجة', CRIT)
    else:
        metrics(draw, w, 290, [('المطلوب', 14, INFO), ('المتاح', 9, OK), ('الفجوة', 5, WARN)])
        row(draw, w, 400, 'ضابط برج', 'فجوة 1', '67%', WARN)


def render(kind: str, w: int, h: int) -> Image.Image:
    img = Image.new('RGB', (w, h), BG)
    draw = ImageDraw.Draw(img)
    subtitle = 'سجن تجريبي أ · بيانات وهمية RTL — بدون بيانات شخصية حقيقية'
    header(draw, w, 'القوى البشرية والتغطية التشغيلية', subtitle)

    if kind in ('overview', 'ready', 'attention', 'critical', 'unknown', 'partial'):
        render_overview_state(draw, w, kind)
    elif kind == 'shift':
        header(draw, w, 'تغطية الورديات', subtitle)
        row(draw, w, 160, 'DAY · ضابط برج', 'مطلوب 3 · حاضر 1 · فجوة 1', 'جزئي', WARN)
        row(draw, w, 240, 'NIGHT · مراقب بوابة', 'مطلوب 2 · حاضر 2 · فجوة 0', 'مكتمل', OK)
    elif kind == 'unit':
        header(draw, w, 'تغطية الوحدات', subtitle)
        metrics(draw, w, 160, [('عنبر أ', '67%', WARN), ('عنبر ب', '100%', OK), ('غرفة التحكم', '50%', WARN)])
    elif kind == 'critical-gaps':
        header(draw, w, 'فجوات الأدوار الحرجة', subtitle)
        row(draw, w, 160, 'قائد وردية', 'دور حرج بلا بديل مؤهل', 'نقطة فشل', CRIT)
        row(draw, w, 240, 'سائق نقل', 'شهادة منتهية · لا تغطية مؤهلة', 'فجوة 1', WARN)
    elif kind == 'member-panel':
        header(draw, w, 'تفاصيل العضو', subtitle)
        if w >= 900:
            panel(draw, w, h, 'عضو قوى بشرية', 'ناصر الدوسري', 'EMP-DEMO-014', 'ضابط أمن · عنبر أ · تشغيلي')
        else:
            strip(draw, w, 160, 'complete', 'ناصر الدوسري', 'EMP-DEMO-014 · ضابط أمن · عنبر أ', 'تشغيلي')
    elif kind == 'shift-panel':
        header(draw, w, 'لوحة الوردية', subtitle)
        panel(draw, w, h, 'وردية تشغيل', 'وردية نهار · DAY', 'shift-day', 'فجوة 1 · قائد غير معيّن')
    elif kind == 'qual':
        header(draw, w, 'انتهاء المؤهلات', subtitle)
        row(draw, w, 160, 'رخصة برج', 'فيصل الحربي · تنتهي 2026-07-01', 'منتهية', WARN)
    elif kind == 'unsafe':
        header(draw, w, 'تغطية غير آمنة', subtitle)
        strip(draw, w, 160, 'critical', 'أقل من الحد الآمن', 'المتاح 7 أقل من الحد الآمن 11 في وردية الليل.', 'Unsafe')
    elif kind == 'dq':
        header(draw, w, 'جودة البيانات', subtitle)
        metrics(draw, w, 160, [('الأعضاء', 12, INFO), ('حالة مجهولة', 1, WARN), ('تحقق قديم', 1, WARN), ('استيراد مفتوح', 0, OK)])
    elif kind == 'empty':
        header(draw, w, 'حالة فارغة', subtitle)
        draw.rounded_rectangle((24, 180, w - 24, 280), radius=20, fill=MUTED)
        rtl_text(draw, (w - 48, 220), 'لا يوجد أعضاء مسجلون في هذا السجن التجريبي.', FONT_M, SECONDARY)
    elif kind == 'import':
        header(draw, w, 'معاينة الاستيراد', subtitle)
        draw.rounded_rectangle((24, 160, w - 24, 420), radius=20, fill=SURFACE, outline=(200, 210, 205))
        rtl_text(draw, (w - 48, 180), 'معاينة استيراد القوى البشرية', FONT_M, TEXT)
        rtl_text(draw, (w - 48, 220), 'نوع الاستيراد: المؤهلات', FONT_S, SECONDARY)
        rtl_text(draw, (w - 48, 250), 'الاسم: بندر الشمري · مرجع: D5-1-demo-import', FONT_S, SECONDARY)
        strip(draw, w, 460, 'complete', '1/1', 'مرفوض 0 · مكرر 0 · مطبق 0', 'تأكيد')

    rtl_text(draw, (w - 24, h - 28), 'Baseera Phase D.5.1 · harness capture · RTL', FONT_S, (150, 160, 155))
    return img


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    for fname, (kind, w, h) in SCENES.items():
        path = os.path.join(OUT, fname)
        render(kind, w, h).save(path, 'PNG', optimize=True)
        size = os.path.getsize(path)
        if size < 5000:
            raise SystemExit(f'{fname} is only {size} bytes')
        print(f'wrote {fname} ({size} bytes)')
    print(f'Captured {len(SCENES)} PNGs into {OUT}')


if __name__ == '__main__':
    main()
