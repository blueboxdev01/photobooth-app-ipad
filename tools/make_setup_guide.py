"""Build the setup guide PDF from docs/SETUP-GUIDE.md.

The other guides in this folder are written out by hand in Python. This one is
read from the Markdown instead, because the Markdown is the guide people edit,
and a printed copy that has quietly drifted from it is worse than none: it is
the one somebody is holding at the venue.

So it understands only what that file uses -- headings, numbered and bulleted
lists, tick boxes, tables, quotes and code blocks -- and renders them in the same
look as the event runbook.

    python tools/make_setup_guide.py

Needs reportlab (pip install reportlab).
"""

import re
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import (
    BaseDocTemplate, CondPageBreak, Frame, KeepTogether, PageBreak, PageTemplate,
    Paragraph, Preformatted, Spacer, Table, TableStyle,
)

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "docs" / "SETUP-GUIDE.md"
OUT = ROOT / "docs" / "Photobooth-Setup-Guide.pdf"

INK = colors.HexColor("#14161A")
SOFT = colors.HexColor("#5B6270")
ACCENT = colors.HexColor("#2563EB")
LINE = colors.HexColor("#D7DAE0")
WASH = colors.HexColor("#F4F6F8")
CODE_WASH = colors.HexColor("#EEF0F3")

PAGE_W, PAGE_H = A4
MARGIN = 16 * mm
CONTENT_W = PAGE_W - 2 * MARGIN
INDENT = 7 * mm


def style(name, **kw):
    base = dict(fontName="Helvetica", fontSize=9.5, leading=13,
                textColor=INK, alignment=TA_LEFT)
    base.update(kw)
    return ParagraphStyle(name, **base)


S_TITLE = style("t", fontName="Helvetica-Bold", fontSize=22, leading=26)
S_PART = style("p", fontName="Helvetica-Bold", fontSize=17, leading=21,
               textColor=ACCENT, spaceAfter=4)
S_H = style("h", fontName="Helvetica-Bold", fontSize=12.5, leading=16,
            textColor=INK, spaceBefore=6, spaceAfter=3)
S_H3 = style("h3", fontName="Helvetica-Bold", fontSize=10.5, leading=14,
             spaceBefore=4, spaceAfter=2)
S_BODY = style("b", spaceAfter=5)
S_STEP = style("s")
S_CELL = style("c", fontSize=8.6, leading=11.6)
S_CELL_H = style("ch", fontName="Helvetica-Bold", fontSize=8.6, leading=11.6)
S_CALL = style("q", fontSize=9.5, leading=13)
S_CODE = style("code", fontName="Courier", fontSize=7.4, leading=9.6)


# --- inline Markdown -----------------------------------------------------------

def inline(text):
    """**bold**, `code`, [links](...) and <https://...> into reportlab markup."""
    text = re.sub(r"<(https?://[^>]+)>", r"\1", text)
    text = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

    # Code first, so nothing inside it is taken for bold.
    codes = []

    def keep_code(m):
        codes.append(m.group(1))
        return f"\x00{len(codes) - 1}\x00"

    text = re.sub(r"`([^`]+)`", keep_code, text)
    text = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text)
    text = re.sub(r"(?<![*\w])\*([^*\n]+?)\*(?![*\w])", r"<i>\1</i>", text)
    text = re.sub(r"\[([^\]]+)\]\((https?://[^)]+)\)",
                  r'<link href="\2" color="#2563EB">\1</link>', text)
    text = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", text)
    text = re.sub(r"(?<![\"=])(https?://[^\s<]+)",
                  r'<link href="\1" color="#2563EB">\1</link>', text)
    text = re.sub(r"\x00(\d+)\x00",
                  lambda m: f'<font face="Courier" size="8.8">{codes[int(m.group(1))]}</font>',
                  text)
    return text


# --- blocks --------------------------------------------------------------------

def _box():
    b = Table([[""]], colWidths=[3.6 * mm], rowHeights=[3.6 * mm])
    b.setStyle(TableStyle([
        ("BOX", (0, 0), (-1, -1), 0.9, SOFT),
        ("TOPPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 0),
    ]))
    return b


def item_list(items, marker, width=CONTENT_W):
    """Numbered, bulleted or tick-box items; each item a list of flowables."""
    rows = []
    for i, (label, body) in enumerate(items):
        if marker == "tick":
            lead = _box()
        elif marker == "bullet":
            lead = Paragraph("&bull;", S_STEP)
        else:
            lead = Paragraph(f"<b>{label}.</b>", style("n", textColor=SOFT))
        rows.append([lead, body])

    t = Table(rows, colWidths=[INDENT, width - INDENT])
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 3),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 0),
        ("TOPPADDING", (0, 0), (0, -1), 4.5 if marker == "tick" else 3),
    ]))
    return t


def callout(paragraphs, width=CONTENT_W):
    inner = []
    for p in paragraphs:
        inner.append(Paragraph(inline(p), S_CALL))
        inner.append(Spacer(1, 3))
    t = Table([[inner[:-1]]], colWidths=[width])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), WASH),
        ("LINEBEFORE", (0, 0), (0, -1), 2.5, ACCENT),
        ("LEFTPADDING", (0, 0), (-1, -1), 9),
        ("RIGHTPADDING", (0, 0), (-1, -1), 9),
        ("TOPPADDING", (0, 0), (-1, -1), 7),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
    ]))
    return t


def code(lines, width=CONTENT_W):
    t = Table([[Preformatted("\n".join(lines), S_CODE)]], colWidths=[width])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), CODE_WASH),
        ("LEFTPADDING", (0, 0), (-1, -1), 8),
        ("RIGHTPADDING", (0, 0), (-1, -1), 8),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ]))
    return t


def table(rows):
    """A Markdown table. Column widths follow how much each column has to say."""
    header, body = rows[0], rows[1:]
    cols = len(header)
    weight = [min(max(max((len(r[c]) for r in rows if c < len(r)), default=0), 8), 60)
              for c in range(cols)]
    total = sum(weight)
    widths = [CONTENT_W * w / total for w in weight]

    data = [[Paragraph(inline(h), S_CELL_H) for h in header]]
    data += [[Paragraph(inline(r[c]) if c < len(r) else "", S_CELL) for c in range(cols)]
             for r in body]

    t = Table(data, colWidths=widths, repeatRows=1)
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("BACKGROUND", (0, 0), (-1, 0), WASH),
        ("LINEBELOW", (0, 0), (-1, 0), 0.9, LINE),
        ("LINEBELOW", (0, 1), (-1, -2), 0.4, LINE),
        ("BOX", (0, 0), (-1, -1), 0.9, LINE),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
    ]))
    return t


# --- the parser ------------------------------------------------------------------

LIST_ITEM = re.compile(r"^(\d+)\.\s+(.*)$|^-\s+\[ \]\s+(.*)$|^-\s+(.*)$")


def list_kind(line):
    m = LIST_ITEM.match(line)
    if not m:
        return None
    if m.group(1):
        return "number", m.group(1), m.group(2)
    if m.group(3) is not None:
        return "tick", "", m.group(3)
    return "bullet", "", m.group(4)


def item_body(lines, width):
    """An item's own text, then anything nested under it: a quote or code block."""
    out, text, i = [], [], 0
    while i < len(lines):
        line = lines[i]
        if line.startswith("```"):
            block = []
            i += 1
            while i < len(lines) and not lines[i].startswith("```"):
                block.append(lines[i])
                i += 1
            out.append(Spacer(1, 3))
            out.append(code(block, width))
            out.append(Spacer(1, 3))
        elif line.startswith(">"):
            quote = []
            while i < len(lines) and lines[i].startswith(">"):
                quote.append(lines[i].lstrip("> ").strip())
                i += 1
            out.append(Spacer(1, 3))
            out.append(callout([" ".join(q for q in quote if q)], width))
            out.append(Spacer(1, 3))
            continue
        elif list_kind(line.strip()) and i > 0:
            # A list nested in a step, such as the camera settings under "set:".
            marker = list_kind(line.strip())[0]
            sub = []
            while i < len(lines) and list_kind(lines[i].strip()) \
                    and list_kind(lines[i].strip())[0] == marker:
                k = list_kind(lines[i].strip())
                sub.append((k[1], [Paragraph(inline(k[2]), S_STEP)]))
                i += 1
            out.append(item_list(sub, marker, width))
            continue
        elif line.strip():
            text.append(line.strip())
        i += 1

    head = [Paragraph(inline(" ".join(text)), S_STEP)] if text else []
    return head + out


def build(markdown):
    lines = markdown.splitlines()
    story = []
    i = 0
    seen_title = False
    seen_part = False

    while i < len(lines):
        line = lines[i]

        if not line.strip() or line.strip() == "---":
            i += 1
            continue

        if line.startswith("# "):
            title = line[2:].strip()
            if not seen_title:
                story.append(Paragraph(inline(title), S_TITLE))
                story.append(Spacer(1, 8))
                seen_title = True
            else:
                # Each part starts a page, so a printed copy can be split
                # between the people setting up each machine.
                if seen_part:
                    story.append(PageBreak())
                seen_part = True
                story.append(Paragraph(inline(title), S_PART))
            i += 1
            continue

        if line.startswith("## ") or line.startswith("### "):
            level = S_H if line.startswith("## ") else S_H3
            # Never a heading alone at the foot of a page.
            story.append(CondPageBreak(40 * mm))
            story.append(Paragraph(inline(line.lstrip("#").strip()), level))
            i += 1
            continue

        if line.startswith("```"):
            block = []
            i += 1
            while i < len(lines) and not lines[i].startswith("```"):
                block.append(lines[i])
                i += 1
            i += 1
            story.append(KeepTogether([code(block), Spacer(1, 6)]))
            continue

        if line.startswith(">"):
            quote, para = [], []
            while i < len(lines) and lines[i].startswith(">"):
                text = lines[i].lstrip(">").strip()
                if text:
                    para.append(text)
                elif para:
                    quote.append(" ".join(para))
                    para = []
                i += 1
            if para:
                quote.append(" ".join(para))
            story.append(callout(quote))
            story.append(Spacer(1, 6))
            continue

        if line.startswith("|"):
            rows = []
            while i < len(lines) and lines[i].startswith("|"):
                cells = [c.strip() for c in lines[i].strip().strip("|").split("|")]
                if not all(re.fullmatch(r":?-+:?", c) for c in cells):
                    rows.append(cells)
                i += 1
            story.append(table(rows))
            story.append(Spacer(1, 6))
            continue

        kind = list_kind(line)
        if kind:
            marker = kind[0]
            items = []
            while i < len(lines):
                k = list_kind(lines[i])
                if not k or k[0] != marker:
                    break
                own = [k[2]]
                i += 1
                # Continuation: indented lines, and blank lines followed by more
                # indented ones (a code block or quote nested in a step).
                while i < len(lines):
                    nxt = lines[i]
                    if nxt.startswith("   ") or nxt.startswith("\t"):
                        own.append(nxt.strip() if not nxt.strip().startswith(("```", ">"))
                                   else nxt.strip())
                        i += 1
                    elif not nxt.strip() and i + 1 < len(lines) and (
                            lines[i + 1].startswith("   ")):
                        i += 1
                    else:
                        break
                items.append((k[1], item_body(own, CONTENT_W - INDENT)))
                # A blank line between items of the same list does not end it.
                if (i < len(lines) and not lines[i].strip() and i + 1 < len(lines)
                        and list_kind(lines[i + 1]) and list_kind(lines[i + 1])[0] == marker):
                    i += 1
            story.append(item_list(items, marker))
            story.append(Spacer(1, 6))
            continue

        para = []
        while (i < len(lines) and lines[i].strip() and not lines[i].startswith(("#", "|", ">", "```"))
               and not list_kind(lines[i]) and lines[i].strip() != "---"):
            para.append(lines[i].strip())
            i += 1
        story.append(Paragraph(inline(" ".join(para)), S_BODY))

    return story


def version(markdown):
    m = re.search(r"build \*\*(v[\d.]+)\*\*", markdown)
    return m.group(1) if m else ""


def main():
    markdown = SOURCE.read_text(encoding="utf-8")
    label = f"Photobooth  |  Setup guide {version(markdown)}".rstrip()

    def furniture(canvas, doc):
        canvas.saveState()
        canvas.setFont("Helvetica", 7.5)
        canvas.setFillColor(SOFT)
        canvas.drawString(MARGIN, 10 * mm, label)
        canvas.drawRightString(PAGE_W - MARGIN, 10 * mm, f"Page {doc.page}")
        canvas.setStrokeColor(LINE)
        canvas.setLineWidth(0.5)
        canvas.line(MARGIN, 13 * mm, PAGE_W - MARGIN, 13 * mm)
        canvas.restoreState()

    doc = BaseDocTemplate(
        str(OUT), pagesize=A4,
        leftMargin=MARGIN, rightMargin=MARGIN, topMargin=MARGIN, bottomMargin=18 * mm,
        title="Photobooth setup guide", author="Photobooth")
    frame = Frame(MARGIN, 18 * mm, CONTENT_W, PAGE_H - MARGIN - 18 * mm, id="body",
                  leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    doc.addPageTemplates([PageTemplate(id="page", frames=[frame], onPage=furniture)])
    doc.build(build(markdown))
    print(f"Wrote {OUT}")


if __name__ == "__main__":
    main()
