"""Build the test guide PDF: Mac booth, preview monitor, Windows claiming station.

This is docs/WEDNESDAY-TEST.md as a printable sheet.

Laid out for someone working through it beside the booth: every action has a tick
box, and the test run ends with a results table to fill in by hand.

    python3 tools/make_mac_test_guide.py [output.pdf]

Needs reportlab (pip install reportlab). Keep the text in step with
docs/WEDNESDAY-TEST.md, which is the source of truth. Helvetica is a Latin-1
font, so no arrows or other symbols: they print as black boxes.
"""

import os
import sys

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import (
    BaseDocTemplate, Frame, PageBreak, PageTemplate, Paragraph, Preformatted,
    Spacer, Table, TableStyle,
)

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    HERE, "..", "docs", "Photobooth-Mac-Booth-Test-Guide.pdf")

INK = colors.HexColor("#14161A")
SOFT = colors.HexColor("#5B6270")
ACCENT = colors.HexColor("#2563EB")
LINE = colors.HexColor("#D7DAE0")
WASH = colors.HexColor("#F4F6F8")
WARN_WASH = colors.HexColor("#FEF3E2")
WARN_LINE = colors.HexColor("#E0A155")

PAGE_W, PAGE_H = A4
MARGIN = 18 * mm
CONTENT_W = PAGE_W - 2 * MARGIN


def style(name, **kw):
    base = dict(fontName="Helvetica", fontSize=9.5, leading=13.5,
                textColor=INK, alignment=TA_LEFT)
    base.update(kw)
    return ParagraphStyle(name, **base)


S_TITLE = style("t", fontName="Helvetica-Bold", fontSize=21, leading=25)
S_SUB = style("sub", fontSize=11, leading=15, textColor=SOFT)
S_STAGE = style("stage", fontName="Helvetica-Bold", fontSize=13.5,
                leading=17, textColor=ACCENT, spaceBefore=2, spaceAfter=1)
S_STAGE_NOTE = style("sn", fontSize=9, leading=12.5, textColor=SOFT)
S_PART = style("part", fontName="Helvetica-Bold", fontSize=17, leading=21)
S_BODY = style("b", spaceAfter=4)
S_STEP = style("s")
S_CELL = style("c", fontSize=8.5, leading=11.5)
S_CELL_B = style("cb", fontSize=8.5, leading=11.5, fontName="Helvetica-Bold")
S_WARN = style("w", fontSize=9.5, leading=13.5)
S_WARN_H = style("wh", fontName="Helvetica-Bold", fontSize=10, leading=14)
S_CODE = style("code", fontName="Courier", fontSize=8.5, leading=12,
               textColor=INK, backColor=WASH, borderPadding=(5, 6, 5, 6),
               leftIndent=6, rightIndent=6, spaceBefore=4, spaceAfter=8)
S_DIAGRAM = style("diag", fontName="Courier", fontSize=7.6, leading=10.5,
                  textColor=INK, backColor=WASH, borderPadding=(6, 6, 6, 6),
                  leftIndent=6, rightIndent=6)


def stage(title, note=None):
    bits = [Paragraph(title, S_STAGE)]
    if note:
        bits.append(Paragraph(note, S_STAGE_NOTE))
    bits.append(Spacer(1, 5))
    return bits


def _box():
    """Drawn as its own table so it keeps its size whatever the text does."""
    b = Table([[""]], colWidths=[3.6 * mm], rowHeights=[3.6 * mm])
    b.setStyle(TableStyle([
        ("BOX", (0, 0), (-1, -1), 0.9, SOFT),
        ("TOPPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 0),
    ]))
    return b


def steps(items, start=1):
    """Numbered steps, each with a tick box."""
    rows = [[_box(), f"{start + i}.", Paragraph(text, S_STEP)]
            for i, text in enumerate(items)]
    t = Table(rows, colWidths=[7 * mm, 8 * mm, CONTENT_W - 15 * mm])
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (0, -1), 6.5),
        ("FONTNAME", (1, 0), (1, -1), "Helvetica-Bold"),
        ("FONTSIZE", (1, 0), (1, -1), 9.5),
        ("TEXTCOLOR", (1, 0), (1, -1), SOFT),
    ]))
    return t


def ticks(items):
    """Tick boxes without numbers."""
    rows = [[_box(), Paragraph(t, S_STEP)] for t in items]
    t = Table(rows, colWidths=[7 * mm, CONTENT_W - 7 * mm])
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 4.5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4.5),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("TOPPADDING", (0, 0), (0, -1), 6),
    ]))
    return t


def callout(heading, body, warn=False):
    inner = [Paragraph(heading, S_WARN_H), Spacer(1, 3), Paragraph(body, S_WARN)]
    t = Table([[inner]], colWidths=[CONTENT_W])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), WARN_WASH if warn else WASH),
        ("BOX", (0, 0), (-1, -1), 0.8, WARN_LINE if warn else LINE),
        ("LEFTPADDING", (0, 0), (-1, -1), 9),
        ("RIGHTPADDING", (0, 0), (-1, -1), 9),
        ("TOPPADDING", (0, 0), (-1, -1), 8),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
    ]))
    return t


def table(header, rows, widths, cell=S_CELL):
    """A table with a header row, or with none when <header> is empty."""
    data = [[Paragraph(h, S_CELL_B) for h in header]] if header else []
    data += [[Paragraph(c, cell) for c in r] for r in rows]
    t = Table(data, colWidths=widths, repeatRows=1 if header else 0)
    first = 1 if header else 0
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("BACKGROUND", (0, 0), (-1, 0), WASH if header else colors.white),
        ("LINEBELOW", (0, 0), (-1, 0), 0.8 if header else 0.4, LINE),
        ("LINEBELOW", (0, first), (-1, -2), 0.4, LINE),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("BOX", (0, 0), (-1, -1), 0.8, LINE),
    ]))
    return t


def rule(before=10, after=8):
    t = Table([[""]], colWidths=[CONTENT_W], rowHeights=[0.1])
    t.setStyle(TableStyle([
        ("LINEABOVE", (0, 0), (-1, 0), 0.8, LINE),
        ("TOPPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
    ]))
    return [Spacer(1, before), t, Spacer(1, after)]


def furniture(canvas, doc):
    canvas.saveState()
    canvas.setFont("Helvetica", 7.5)
    canvas.setFillColor(SOFT)
    canvas.drawString(MARGIN, 11 * mm,
                      "Photobooth  |  Mac booth + Windows claiming station: setup and test flow")
    canvas.drawRightString(PAGE_W - MARGIN, 11 * mm, f"Page {doc.page}")
    canvas.setStrokeColor(LINE)
    canvas.setLineWidth(0.5)
    canvas.line(MARGIN, 14 * mm, PAGE_W - MARGIN, 14 * mm)
    canvas.restoreState()


story = []
A = story.append
E = story.extend

# ----------------------------------------------------------------------- cover

A(Paragraph("Pickup by number: setup and test flow", S_TITLE))
A(Spacer(1, 4))
A(Paragraph(
    "Build <b>v0.19.1</b> &nbsp;&middot;&nbsp; MacBook booth with a preview monitor, and the Windows "
    "laptop as the claiming station. From the queue to a phone holding four photos, a strip and a "
    "video.", S_SUB))
A(Spacer(1, 12))

A(table(
    None,
    [
        ["The setup",
         "The <b>MacBook is the booth</b>: it takes the photos, builds every strip and stores "
         "everything. An <b>external monitor on the Mac</b> is the guest screen. The <b>Windows "
         "laptop is the claiming station</b>: it reads the Mac's shared folder and lists each guest "
         "by number. No iPad. No internet needed."],
        ["What a guest gets",
         "<b>4 photos, 1 strip and 1 video</b> (an MP4, which Instagram and Facebook accept), by "
         "scanning a QR the operator shows on the Windows laptop."],
        ["What you need",
         "MacBook and external monitor, Windows laptop, Canon R50 with USB-C cable and BR-E1, a travel "
         "router (WAN port empty), a printed join-wifi sign, and two phones (one iPhone, one Android)."],
        ["Open risks",
         "EOS Utility for Mac lists macOS 26, 15 and 14: a newer macOS is <b>unverified</b> (Test 1). "
         "The Mac-to-Windows folder share is untested on real hardware (Tests 4, 13, 14). With 4 "
         "photos the frame art is dropped until a 4-window frame exists."],
    ],
    [30 * mm, CONTENT_W - 30 * mm]))

A(Spacer(1, 12))
A(Paragraph("How a guest moves through it", S_STAGE))
A(Spacer(1, 4))
A(table(
    ["", "Where", "What happens"],
    [
        ["1", "The queue", "A sign with the <b>join-wifi QR</b>. They join the booth wifi while they wait."],
        ["2", "Centre stage",
         "They frame on the camera's own viewfinder and take <b>4 shots</b>. The monitor counts down; "
         "staff press the BR-E1."],
        ["3", "The monitor",
         "It shows <b>their finished strip</b>. Staff at the Mac press <b>Accept</b>, or <b>Retake this one</b>."],
        ["4", "The monitor",
         "A large <b>number</b> (1, 2, 3..., restarting each day) and <i>Tell this number at the "
         "claiming station</i>. The number is also stamped on the strip. <b>No QR here.</b>"],
        ["5", "Claiming station",
         "The guest says their number. The operator types it, <b>hovers</b> the strip, clicks "
         "<b>Show QR</b>, and the guest scans it to download everything."],
    ],
    [8 * mm, 32 * mm, CONTENT_W - 40 * mm]))

A(Spacer(1, 8))
A(Preformatted(
    "Canon R50 --USB--> Booth MacBook --cable--> Router (WAN empty) <--wifi-- guests' phones\n"
    "                   booth app, EOS Utility,       ^\n"
    "[external monitor] all sessions, shared (SMB)    '-- Windows laptop  /pickup  claiming station\n"
    " = guest screen",
    S_DIAGRAM))

A(PageBreak())

# ---------------------------------------------------------------------- part 1

A(Paragraph("Part 1 &nbsp;&middot;&nbsp; Set up", S_PART))
A(Spacer(1, 3))
A(Paragraph("Tick these in order. Full detail: EVENT-SETUP.md section 1b.", S_STAGE_NOTE))
A(Spacer(1, 8))

E(stage("A. The Mac"))
A(ticks([
    "Mains power; display off <b>Never</b>; <b>FileVault on</b>.",
    "<font face='Courier'>brew install ffmpeg</font> done, and <font face='Courier'>ffmpeg -version</font> "
    "prints a version. Without it there is no video.",
    "Canon <b>EOS Utility 3 for macOS</b> installed (R50 needs 3.16.10+; 3.19.0+ is native on Apple "
    "Silicon). This Mac runs macOS: ______________  (Canon lists 26, 15, 14; if newer it is unverified).",
    "Camera tethered: <b>Photo Import/Remote Control</b>, manual exposure and white balance, <b>JPEG "
    "only</b>, auto power-off off, BR-E1 paired. <b>Photos / Image Capture closed.</b> EOS Utility "
    "destination folder = the booth's watch folder.",
    "Booth app running: <font face='Courier'>photobooth-v0.19.1-macos-arm64.zip</font> (unsigned: "
    "<font face='Courier'>xattr -dr com.apple.quarantine &lt;folder&gt;</font> once), or "
    "<font face='Courier'>dotnet run --project src/Photobooth.Server</font>. Allow incoming connections when asked. <font face='Courier'>http://localhost:5000/operator</font> opens.",
]))
E(rule(6, 6))

E(stage("B. Router"))
A(ticks([
    "Travel router, <b>WAN port empty</b>, mode Router, WPA2, <b>client isolation and guest mode off</b>.",
    "The Mac and the Windows laptop are both on it (cable preferred, with a fixed address).",
]))
E(rule(6, 6))

E(stage("C. Mac settings", "The Settings page is at the foot of the operator rail."))
A(ticks([
    "<b>Watch folder</b> = EOS Utility's destination. <b>Output folder</b> is a different folder. "
    "<b>Check</b>, then <b>Save</b>.",
    "<b>Photos per strip: 4.</b> (The shipped frame has three windows; at 4 the frame art is dropped "
    "and you get a plain 4-photo strip.)",
    "<b>Let guests download their photos</b>: on. Wifi name and password <b>exactly</b> as on the router. "
    "<b>Save wifi details</b>. <b>Print the join-wifi QR</b> that appears, for the queue sign.",
    "<b>Hand over by number</b>: on. <b>Use a separate claiming station</b>: off.",
    "<b>Share the Output folder</b>: System Settings &gt; General &gt; Sharing &gt; File Sharing. Give the "
    "Windows laptop's user Read &amp; Write. Mac's address: ________________",
]))

A(PageBreak())

E(stage("D. The monitor"))
A(ticks([
    "External monitor connected and set as the <b>main display</b> (System Settings &gt; Displays).",
    "Run <font face='Courier'>tools/mac-display.command --local</font>. It shows <i>Step in and smile</i> "
    "fullscreen. No iPad, certificate or HTTPS is needed.",
]))
E(rule(6, 6))

E(stage("E. The Windows laptop (claiming station)"))
A(ticks([
    "<font face='Courier'>photobooth-v0.19.1-win-x64.zip</font> unzipped and run. Allow <font face='Courier'>Photobooth.Server</font> on "
    "<b>Private networks</b> when asked.",
    "Connect to the Mac's share: File Explorer &gt; <font face='Courier'>\\\\&lt;Mac address&gt;\\&lt;share&gt;"
    "</font>. Sign in as the Mac user, tick <b>Remember my credentials</b>, and map it to a drive letter "
    "set to <b>Reconnect at sign-in</b>. Drive: ______",
    "Settings (<font face='Courier'>http://localhost:5000/diagnostics</font>): <b>Output folder</b> = the "
    "shared folder (<b>Check</b>, <b>Save</b>).",
    "<b>Let guests download their photos</b> on, the <b>same wifi name and password</b>, and <b>Guests "
    "reach the booth at</b> = this laptop's address on the router.",
    "<b>Serve the display to the network</b>: on. <b>This computer is the claiming station only</b>: on. "
    "<b>Restart the app.</b>",
    "<font face='Courier'>http://localhost:5000/pickup</font> opens and shows <i>Claiming station</i> in "
    "place of the camera status.",
]))

A(PageBreak())

# ---------------------------------------------------------------------- part 2

A(Paragraph("Part 2 &nbsp;&middot;&nbsp; The test run", S_PART))
A(Spacer(1, 3))
A(Paragraph("Do these in order. Stop at the first failure and note it.", S_STAGE_NOTE))
A(Spacer(1, 8))

A(callout(
    "Test 1 can end the Mac plan",
    "If EOS Utility will not run, or will not see the R50, on this macOS, <b>stop</b>: use the Windows "
    "laptop as the booth for the event and report the macOS version.", warn=True))
A(Spacer(1, 8))

A(steps([
    "<b>Camera and EOS Utility on this Mac.</b> QA-CHECKLIST.md section 0: ten BR-E1 presses over five "
    "minutes, each landing a JPEG in the watch folder, camera <b>Ready</b>.",

    "<b>The monitor</b> counts down with the operator console and shows each shot.",

    "<b>One full session, 4 shots.</b> <b>Reorder</b> two shots and <b>Retake this one</b>: the strip must "
    "change to match. Press <b>Accept</b>. The monitor shows a <b>large number and no QR</b>. Open "
    "<font face='Courier'>strip.jpg</font> in the session folder: <b>No. N</b> at the foot, the same N.",

    "<b>The claiming station sees it.</b> On the Windows laptop, <font face='Courier'>/pickup</font> shows "
    "that guest with the same number within about 3 seconds. Time: ____ s.",

    "<b>Find a guest.</b> Type the number: only that guest remains. <b>Hover</b> the strip: <b>Show QR</b> "
    "appears. Click: a large QR, with the wifi name. <b>Close</b> returns to the grid. Clear the box: "
    "everyone is back.",

    "<b>Numbers.</b> Take three more guests: consecutive numbers. <b>Restart the Mac's booth app</b>, take "
    "one more: the number carries on, with no repeat.",

    "<b>Two guests back to back.</b> Start guest 2 <i>while guest 1 is still on the claiming station</i>. "
    "Both appear, correctly numbered; guest 1's QR opens guest 1's photos.",

    "<b>Download on two phones</b> (iPhone and Android) joined from the queue sign. Show QR to strip "
    "visible ____ s; to all photos ____ s. The page offers <b>4 photos, the strip and the video</b>. Save "
    "a photo and the video; <b>Download all</b> gives a zip. Post the video to Instagram and Facebook. "
    "A phone <b>not</b> on the wifi: the QR fails to open.",

    "<b>The queue sign:</b> from a phone not yet on the wifi, scan it. It joins without a password.",

    "<b>Several phones at once</b> (5+), all downloading. Note when it slows: ____________________",

    "<b>Sizes</b>: photo-1.jpg ______ &nbsp; thumb-1.jpg ______ &nbsp; strip.jpg ______ &nbsp; "
    "strip.mp4 ______",

    "<b>Security.</b> From a phone on the guest wifi, <font face='Courier'>https://&lt;Windows laptop&gt;:5002"
    "/api/pickup</font> and <font face='Courier'>.../api/sessions</font> must both show <i>not found</i>.",

    "<b>Shared folder under load.</b> Take guests quickly on the Mac while watching the Windows grid: every "
    "guest appears, none half-loaded (a strip with no photo behind it).",

    "<b>Mac off.</b> Quit the booth app on the Mac for a minute: the Windows grid keeps the guests already "
    "taken, and their downloads still open while the Mac is on and sharing.",

    "<b>Restart the Windows app</b> and reload <font face='Courier'>/pickup</font>: the same numbers return.",
], 1))

A(Spacer(1, 10))
A(callout(
    "If the Mac cannot be the booth",
    "The Windows laptop can be the booth as before (EVENT-SETUP.md section 5) with <b>Hand over by "
    "number</b> on. It is then both booth and claiming station: its own <font face='Courier'>/pickup</font> "
    "lists its own guests, and no folder share is needed."))

A(PageBreak())

# ----------------------------------------------------------------- bring back

A(Paragraph("What to bring back", S_PART))
A(Spacer(1, 8))
A(ticks([
    "Pass/fail for each step, the times from steps 4 and 8, and the <b>macOS version</b>.",
    "Whether EOS Utility ran and tethered the R50 on that macOS, and whether the BR-E1 worked while tethered.",
    "How the Windows grid kept up with the Mac over the share: any guest slow to appear, appearing with a "
    "missing photo, or any dropped connection.",
    "The diagnostics bundle from the Mac (<font face='Courier'>/diagnostics</font>) and the version at the "
    "foot of the operator rail.",
    "Whether the video posted on Instagram and Facebook.",
    "Whether the number flow made sense: was the number easy to remember and say, and how long did the "
    "operator take to find a guest.",
    "Whether the 4-photo strip without the frame is acceptable, or a 4-window frame is needed.",
]))

A(Spacer(1, 12))
A(Paragraph("Results", S_STAGE))
A(Spacer(1, 4))
names = [
    "1  Camera + EOS Utility", "2  Monitor", "3  Session, number on strip", "4  Windows sees guest",
    "5  Find + Show QR", "6  Numbers + restart", "7  Back to back", "8  Phones download",
    "9  Queue sign", "10 Several phones", "11 Sizes", "12 Security", "13 Share under load",
    "14 Mac off", "15 Windows restart",
]
rows = [[n, "", ""] for n in names]
res = table(["Step", "Pass / fail", "Notes"], rows,
            [52 * mm, 24 * mm, CONTENT_W - 76 * mm])
res.setStyle(TableStyle([
    ("TOPPADDING", (0, 1), (-1, -1), 7),
    ("BOTTOMPADDING", (0, 1), (-1, -1), 7),
    ("LINEBELOW", (0, 1), (-1, -1), 0.4, LINE),
    ("LINEAFTER", (0, 0), (1, -1), 0.4, LINE),
]))
A(res)

doc = BaseDocTemplate(
    OUT, pagesize=A4,
    leftMargin=MARGIN, rightMargin=MARGIN,
    topMargin=MARGIN, bottomMargin=22 * mm,
    title="Photobooth - pickup by number: setup and test flow",
    author="Photobooth",
    subject="Mac booth, preview monitor and Windows claiming station",
)
frame = Frame(MARGIN, 22 * mm, CONTENT_W, PAGE_H - MARGIN - 22 * mm, id="body")
doc.addPageTemplates([PageTemplate(id="main", frames=[frame], onPage=furniture)])
doc.build(story)

print("wrote", os.path.normpath(OUT))
