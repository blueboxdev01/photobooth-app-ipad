"""Build the event-day runbook PDF.

Different job from the test guide. That one is a walkthrough you follow in order
at a desk; this is a reference somebody scans in thirty seconds while a queue
watches. So: no prose, symptom-first tables, and the fallback for each thing
stated before it is needed rather than worked out on the night.

Print it and keep it at the booth. The one failure it cannot help with is the
laptop being the thing that died.
"""

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import (
    BaseDocTemplate, Frame, PageBreak, PageTemplate, Paragraph, Spacer,
    Table, TableStyle,
)

OUT = r"C:\Users\edwar\Projects\photobooth-app-ipad\docs\Photobooth-Event-Runbook.pdf"

INK = colors.HexColor("#14161A")
SOFT = colors.HexColor("#5B6270")
ACCENT = colors.HexColor("#2563EB")
LINE = colors.HexColor("#D7DAE0")
WASH = colors.HexColor("#F4F6F8")
WARN_WASH = colors.HexColor("#FEF3E2")
WARN_LINE = colors.HexColor("#E0A155")
STOP_WASH = colors.HexColor("#FDECEC")
STOP_LINE = colors.HexColor("#D96C6C")

PAGE_W, PAGE_H = A4
MARGIN = 16 * mm
CONTENT_W = PAGE_W - 2 * MARGIN


def style(name, **kw):
    base = dict(fontName="Helvetica", fontSize=9.5, leading=13,
                textColor=INK, alignment=TA_LEFT)
    base.update(kw)
    return ParagraphStyle(name, **base)


S_TITLE = style("t", fontName="Helvetica-Bold", fontSize=22, leading=26)
S_SUB = style("sub", fontSize=11, leading=15, textColor=SOFT)
S_H = style("h", fontName="Helvetica-Bold", fontSize=13.5, leading=17,
            textColor=ACCENT, spaceBefore=2, spaceAfter=1)
S_HNOTE = style("hn", fontSize=9, leading=12.5, textColor=SOFT)
S_BODY = style("b", spaceAfter=4)
S_STEP = style("s")
S_SYM = style("sym", fontName="Helvetica-Bold", fontSize=8.8, leading=11.8)
S_ACT = style("act", fontSize=8.8, leading=11.8)
S_CALL_H = style("ch", fontName="Helvetica-Bold", fontSize=10.5, leading=14)
S_CALL = style("c", fontSize=9.5, leading=13)
S_BIG = style("big", fontName="Helvetica-Bold", fontSize=11.5, leading=15.5)


def heading(title, note=None):
    out = [Paragraph(title, S_H)]
    if note:
        out.append(Paragraph(note, S_HNOTE))
    out.append(Spacer(1, 5))
    return out


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


def ticks(items):
    rows = [[_box(), Paragraph(t, S_STEP)] for t in items]
    t = Table(rows, colWidths=[7 * mm, CONTENT_W - 7 * mm])
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("TOPPADDING", (0, 0), (0, -1), 5.5),
    ]))
    return t


def numbered(items):
    rows = [[f"{i + 1}.", Paragraph(t, S_STEP)] for i, t in enumerate(items)]
    t = Table(rows, colWidths=[7 * mm, CONTENT_W - 7 * mm])
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("FONTNAME", (0, 0), (0, -1), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (0, -1), 9.5),
        ("TEXTCOLOR", (0, 0), (0, -1), SOFT),
    ]))
    return t


def callout(heading_text, body, kind="info"):
    wash, edge = {
        "info": (WASH, LINE),
        "warn": (WARN_WASH, WARN_LINE),
        "stop": (STOP_WASH, STOP_LINE),
    }[kind]
    inner = [Paragraph(heading_text, S_CALL_H), Spacer(1, 3), Paragraph(body, S_CALL)]
    t = Table([[inner]], colWidths=[CONTENT_W])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), wash),
        ("BOX", (0, 0), (-1, -1), 0.9, edge),
        ("LEFTPADDING", (0, 0), (-1, -1), 9),
        ("RIGHTPADDING", (0, 0), (-1, -1), 9),
        ("TOPPADDING", (0, 0), (-1, -1), 8),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
    ]))
    return t


def fixes(rows):
    """Symptom on the left, what to do on the right."""
    data = [[Paragraph("What you see", S_SYM), Paragraph("What to do", S_SYM)]]
    data += [[Paragraph(s, S_SYM), Paragraph(a, S_ACT)] for s, a in rows]

    sym_w = 52 * mm
    t = Table(data, colWidths=[sym_w, CONTENT_W - sym_w], repeatRows=1)
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("BACKGROUND", (0, 0), (-1, 0), WASH),
        ("LINEBELOW", (0, 0), (-1, 0), 0.9, LINE),
        ("LINEBELOW", (0, 1), (-1, -2), 0.4, LINE),
        ("BOX", (0, 0), (-1, -1), 0.9, LINE),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
    ]))
    return t


def blanks(labels):
    rows = [[Paragraph(l, S_ACT), ""] for l in labels]
    t = Table(rows, colWidths=[46 * mm, CONTENT_W - 46 * mm],
              rowHeights=[9 * mm] * len(rows))
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "BOTTOM"),
        ("LINEBELOW", (1, 0), (1, -1), 0.6, LINE),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
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
    canvas.drawString(MARGIN, 10 * mm, "Photobooth  |  Event day runbook")
    canvas.drawRightString(PAGE_W - MARGIN, 10 * mm, f"Page {doc.page}")
    canvas.setStrokeColor(LINE)
    canvas.setLineWidth(0.5)
    canvas.line(MARGIN, 13 * mm, PAGE_W - MARGIN, 13 * mm)
    canvas.restoreState()


story = []
A = story.append
E = story.extend

# ------------------------------------------------------------------- page 1

A(Paragraph("Event day runbook", S_TITLE))
A(Spacer(1, 4))
A(Paragraph(
    "What to do when something goes wrong, decided in advance rather than in "
    "front of a queue.", S_SUB))
A(Spacer(1, 12))

A(callout(
    "The one rule: taking photos and handing them out are separate",
    "If guests cannot download their photos, <b>keep shooting</b>. Every finished "
    "session is already saved on the laptop and can be sent afterwards. "
    "<br/><br/>"
    "Delivery problems are the most likely thing to go wrong and the least "
    "damaging. <b>Never stop the booth to fix one.</b> Take names on paper and "
    "carry on &mdash; the photos are not at risk.", "stop"))

A(Spacer(1, 12))

E(heading("Sixty-second triage",
          "Almost everything is downstream of these four. Check them in order."))
A(numbered([
    "<b>Is the app running?</b> The black console window should still be open. "
    "If not, run <b>Photobooth.Server.exe</b> again.",
    "<b>Does the console load?</b> <b>http://localhost:5000/operator</b> on the "
    "laptop. If not, the app is not running &mdash; see 1.",
    "<b>Is the camera ready?</b> The rail should say <b>Camera Ready</b>. "
    "Anything else is a camera or cable problem, not an app problem.",
    "<b>Is the router powered?</b> Lights on. This one only affects delivery, "
    "never capture.",
]))

A(Spacer(1, 10))

E(heading("Before doors open", "Thirty minutes. Prevents most of what follows."))
A(ticks([
    "Router powered, <b>WAN port empty</b>, laptop cabled into a LAN port.",
    "<b>Start the booth after the cable is in.</b> The HTTPS certificate only "
    "covers addresses the laptop had at startup &mdash; joining a network "
    "afterwards means the iPad will refuse to connect.",
    "<b>Settings &rsaquo; Guests reach the booth at</b> &mdash; pick the "
    "<b>Ethernet</b> address, the one on the router's range.",
    "<b>Settings &rsaquo; wifi network and password</b> match the router exactly, "
    "including capitals.",
    "Output folder set, and <b>/diagnostics</b> shows plenty of free disk. "
    "Budget about <b>25 MB per session</b>.",
    "Camera: dummy battery in, manual exposure and white balance, fixed ISO, "
    "<b>JPEG only</b>, auto power-off disabled.",
    "Fire the remote once and confirm a photo lands in the watch folder.",
    "Run one full session end to end with the real camera.",
    "From your own phone: join the wifi, scan both codes, download a photo "
    "<b>and</b> the zip, and open them.",
    "iPad: display loads and shows the attract screen, <b>Auto-Lock Never</b>, "
    "Guided Access on, and <b>plugged in</b>.",
    "Write the booth's details into the box on the last page.",
]))

A(PageBreak())

# ------------------------------------------------------------------- power

E(heading("Power"))
A(fixes([
    ("Router has no lights",
     "Check the socket and both ends of the barrel plug; try another socket. "
     "<b>If it is dead:</b> switch to the laptop's Mobile Hotspot, then in Settings "
     "pick the <b>192.168.137</b> address. Restart the booth afterwards. Expect "
     "a limit of <b>8 phones at once</b> and no way to disconnect anyone."),
    ("Laptop switched to battery",
     "Find a socket. Capture continues on battery for an hour or two &mdash; "
     "drop the screen brightness and carry on. Nothing is lost if it does die, "
     "but the session in progress is."),
    ("Camera dead",
     "Check the dummy battery lead at both ends first; it pulls out easily. "
     "A real battery gets you under an hour as a stopgap."),
    ("Whole circuit tripped",
     "Restart in order: <b>router first</b>, wait for its lights, then the "
     "laptop, then the booth app. Starting the app before the network is up is "
     "what breaks the iPad."),
]))

A(Spacer(1, 10))

E(heading("Camera and capture", "The half that actually matters. Protect this."))
A(fixes([
    ("Pressing the remote does nothing",
     "Press the <b>camera's own shutter</b>. If that produces a photo, the "
     "remote is at fault &mdash; replace its battery or re-pair it, and shoot "
     "from the camera meanwhile. If neither works, it is the USB cable or EOS "
     "Utility."),
    ("&quot;No photo arrived&quot; / the session times out",
     "Usually the camera has gone to sleep, the USB has been knocked, or EOS "
     "Utility has closed. Wake the camera and check EOS Utility is still in "
     "remote shooting mode. Press <b>Abort</b> and start the guest again."),
    ("Photos are not being picked up",
     "The watch folder is wrong. <b>Settings &rsaquo; Watch folder</b> must be "
     "where EOS Utility actually saves. <b>/diagnostics</b> lists the files it "
     "can see &mdash; if that list is empty, the folder is wrong."),
    ("Photos appear but are ignored",
     "The camera is shooting RAW. Set it to <b>JPEG only</b>; RAW files are "
     "skipped deliberately."),
    ("Camera card full",
     "Set the camera to save to the computer only, or swap the card."),
]))

A(Spacer(1, 10))

E(heading("The app"))
A(fixes([
    ("A session is stuck",
     "Press <b>Abort</b>. That returns it to <b>Idle</b> and clears the guest's "
     "photos and link. Start the next guest normally."),
    ("The app crashed / console window gone",
     "Run <b>Photobooth.Server.exe</b> again. <b>Every finished session is "
     "already on disk and safe</b> &mdash; only the one in progress is lost. "
     "Re-shoot that guest."),
    ("The console page will not load",
     "The app is not running. Restart it. If it will not start, check the disk "
     "is not full."),
    ("Disk nearly full",
     "About 25 MB per session. Copy older session folders to a USB drive and "
     "delete them from the output folder &mdash; but <b>not</b> ones whose "
     "guests have not downloaded yet, or their link stops working."),
    ("The strip looks wrong",
     "Check <b>Templates</b>. Avoid changing the layout mid-event; it affects "
     "every session after it, not the ones already taken."),
]))

A(PageBreak())

# ---------------------------------------------------------------- delivery

E(heading("Guest photo delivery",
          "The most likely thing to fail, and the least serious. See the rule on page 1."))
A(fixes([
    ("Guests cannot find the wifi network",
     "The name in Settings is <b>only text for the QR code</b> &mdash; it does not "
     "create anything. Check the router is broadcasting that exact name, "
     "capitals included."),
    ("Phone joins, but the photos page never loads",
     "<b>First:</b> Settings &rsaquo; <i>Guests reach the booth at</i> &mdash; is "
     "it the router's address? <b>Second:</b> the router's guest-network or "
     "client-isolation setting must be <b>off</b>. Those two cover almost every "
     "case."),
    ("Settings warns it is &quot;guessing&quot;",
     "The laptop is on more than one network. Pick the address on the guests' "
     "network. Takes effect immediately, no restart."),
    ("Settings names an address it cannot find",
     "Saved at a different venue. Pick again from the list."),
    ("&quot;We cannot find those photos&quot;",
     "The link is mistyped, or that session's folder has been deleted or moved "
     "out of the output folder."),
    ("An Android guest loses the page",
     "Their phone switched itself back to mobile data because our network has "
     "no internet. Ask them to rejoin and stay connected. Put this on the sign."),
    ("Phones complain there is no internet",
     "Expected and correct. The photos page still works."),
    ("Delivery is broken and you cannot fix it",
     "<b>Stop troubleshooting.</b> Take names and emails on paper, keep "
     "shooting, and send the photos afterwards from the output folder. The "
     "event is not damaged by this."),
]))

A(Spacer(1, 10))

E(heading("The iPad display"))
A(fixes([
    ("Stuck on &quot;Connecting...&quot;",
     "Check the iPad is on the <b>same network as the laptop</b> &mdash; not "
     "the venue wifi, not mobile data. If it is, restart the booth app. "
     "<i>On v0.12.1 and older this was a bug with no workaround; upgrade.</i>"),
    ("Safari says the connection is not private",
     "Certificate Trust Settings &rsaquo; switch the booth on. <b>If it started "
     "after a network change, restart the booth</b> &mdash; the certificate only "
     "covers addresses present when the app started."),
    ("The iPad screen keeps sleeping",
     "Auto-Lock &rsaquo; <b>Never</b>, and keep it on a charger."),
    ("The iPad shows nothing but Connecting",
     "It cannot reach the booth. Check both are on the <b>same wifi</b>, and "
     "that the address still matches the one on Settings."),
    ("The iPad is dead or unusable",
     "<b>Plug a monitor into the laptop</b> and open "
     "<b>http://localhost:5000/display</b>. A complete fallback that needs no "
     "network at all. This is the one worth rehearsing."),
]))

A(PageBreak())

E(heading("Guests and the queue"))
A(fixes([
    ("A guest left without downloading",
     "Take their name and email. Their folder is on the laptop and you can send "
     "it afterwards."),
    ("The queue is building",
     "Reduce the photo count for a session, or move guests through review "
     "faster. Do not change the template."),
    ("A guest cannot manage the QR",
     "Offer to scan it on your own phone and show them, or just take their "
     "email."),
    ("A guest wants their photos removed",
     "Delete that session's folder from the output folder after the event. "
     "Their link dies with it."),
]))

A(Spacer(1, 12))

# ------------------------------------------------------------------ worst

E(heading("If the laptop dies completely"))
A(Paragraph(
    "This is the only failure that stops the event, because the laptop is the "
    "booth: it watches for photos, builds the strips, stores the sessions and "
    "serves the gallery.", S_BODY))
A(Spacer(1, 4))
A(numbered([
    "<b>The photos already taken are not lost.</b> They are on that laptop's "
    "disk and on the camera's card if it is set to save both.",
    "<b>Keep shooting on the camera alone.</b> Set it to save to its card, and "
    "photograph guests normally. No strips and no instant delivery, but the "
    "event still has photos.",
    "<b>Take names and emails.</b> Compose and send afterwards.",
    "Tell guests plainly that photos will follow by email. People mind far less "
    "than you would expect when told.",
]))

A(Spacer(1, 10))
A(callout(
    "Worth doing at the start of the night",
    "Set the camera to save to <b>both</b> the card and the computer, if it "
    "offers it. It costs nothing and turns a dead laptop from a disaster into "
    "an inconvenience.", "warn"))

E(rule())

E(heading("After the event"))
A(ticks([
    "<b>Copy the whole output folder somewhere else before anything else.</b> "
    "One copy is not a backup.",
    "Send photos to anyone who asked, and to anyone whose session you know "
    "failed to deliver.",
    "Delete any session a guest asked you to remove.",
    "Write down anything that went wrong while it is fresh &mdash; that is what "
    "improves the next one.",
]))

A(Spacer(1, 10))

E(heading("Spares kit"))
A(ticks([
    "Spare battery for the remote (CR2032 for the BR-E1)",
    "Spare USB-C cable, and a spare ethernet cable",
    "Extension lead and a multi-way adaptor",
    "USB drive, for offloading sessions if the disk fills",
    "Power bank, if the router is USB-powered",
    "<b>Paper and a pen</b> &mdash; the fallback for every delivery failure",
    "This runbook, printed",
]))

A(Spacer(1, 12))

E(heading("Fill this in before doors open",
          "So anyone standing at the booth can answer a question without you."))
A(blanks([
    "Wifi network name",
    "Wifi password",
    "Booth address (from Settings)",
    "Output folder",
    "App version",
    "Who to call",
]))

doc = BaseDocTemplate(
    OUT, pagesize=A4,
    leftMargin=MARGIN, rightMargin=MARGIN,
    topMargin=MARGIN, bottomMargin=20 * mm,
    title="Photobooth - Event day runbook",
    author="Photobooth",
    subject="Contingencies and fallbacks for running the booth at an event",
)
frame = Frame(MARGIN, 20 * mm, CONTENT_W, PAGE_H - MARGIN - 20 * mm, id="body")
doc.addPageTemplates([PageTemplate(id="main", frames=[frame], onPage=furniture)])
doc.build(story)

print("wrote", OUT)
