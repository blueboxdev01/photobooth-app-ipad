"""Build the field guide PDF for the v0.12.2 guest-photo test.

Laid out for someone working through it at a laptop with the page beside them:
every action is a numbered step with a box to tick, and every stage ends with
something they can check before moving on.
"""

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import (
    BaseDocTemplate, Frame, PageBreak, PageTemplate, Paragraph,
    Spacer, Table, TableStyle, KeepTogether,
)

OUT = r"C:\Users\edwar\Projects\photobooth-app-ipad\docs\Photobooth-v0.12.2-Test-Guide.pdf"

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

ss = getSampleStyleSheet()


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
S_BODY = style("b", spaceAfter=4)
S_STEP = style("s")
S_SMALL = style("sm", fontSize=8.5, leading=12, textColor=SOFT)
S_CELL = style("c", fontSize=8.5, leading=11.5)
S_CELL_B = style("cb", fontSize=8.5, leading=11.5, fontName="Helvetica-Bold")
S_WARN = style("w", fontSize=9.5, leading=13.5)
S_WARN_H = style("wh", fontName="Helvetica-Bold", fontSize=10, leading=14)


def stage(number, title, note=None):
    """A stage heading. Kept with whatever follows it."""
    bits = [Paragraph(f"Stage {number} &nbsp;&middot;&nbsp; {title}", S_STAGE)]
    if note:
        bits.append(Paragraph(note, S_STAGE_NOTE))
    bits.append(Spacer(1, 5))
    return bits


def _box():
    """A small square, drawn as its own table so it keeps its size whatever the
    step text does. A cell border would stretch to the full row height."""
    b = Table([[""]], colWidths=[3.6 * mm], rowHeights=[3.6 * mm])
    b.setStyle(TableStyle([
        ("BOX", (0, 0), (-1, -1), 0.9, SOFT),
        ("TOPPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 0),
    ]))
    return b


def steps(items, start):
    """Numbered steps, each with a tick box. Returns one table."""
    rows = [[_box(), str(start + i) + ".", Paragraph(text, S_STEP)]
            for i, text in enumerate(items)]

    t = Table(rows, colWidths=[7 * mm, 7 * mm, CONTENT_W - 14 * mm])
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        # nudges the square down onto the first text line rather than its cap
        ("TOPPADDING", (0, 0), (0, -1), 6.5),
        ("FONTNAME", (1, 0), (1, -1), "Helvetica-Bold"),
        ("FONTSIZE", (1, 0), (1, -1), 9.5),
        ("TEXTCOLOR", (1, 0), (1, -1), SOFT),
    ]))
    return t


def tickbox_rows(items):
    """Tick boxes without numbers, for a results checklist."""
    rows = [[_box(), Paragraph(t, S_STEP)] for t in items]
    t = Table(rows, colWidths=[7 * mm, CONTENT_W - 7 * mm])
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("TOPPADDING", (0, 0), (0, -1), 6.5),
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


def table(header, rows, widths):
    data = [[Paragraph(h, S_CELL_B) for h in header]]
    data += [[Paragraph(c, S_CELL) for c in r] for r in rows]
    t = Table(data, colWidths=widths, repeatRows=1)
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("BACKGROUND", (0, 0), (-1, 0), WASH),
        ("LINEBELOW", (0, 0), (-1, 0), 0.8, LINE),
        ("LINEBELOW", (0, 1), (-1, -2), 0.4, LINE),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("BOX", (0, 0), (-1, -1), 0.8, LINE),
    ]))
    return t


def rule(space_before=10, space_after=8):
    t = Table([[""]], colWidths=[CONTENT_W], rowHeights=[0.1])
    t.setStyle(TableStyle([
        ("LINEABOVE", (0, 0), (-1, 0), 0.8, LINE),
        ("TOPPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
    ]))
    return [Spacer(1, space_before), t, Spacer(1, space_after)]


def furniture(canvas, doc):
    canvas.saveState()
    canvas.setFont("Helvetica", 7.5)
    canvas.setFillColor(SOFT)
    canvas.drawString(MARGIN, 11 * mm,
                      "Photobooth v0.12.2-ipad  |  Guest photo delivery test")
    canvas.drawRightString(PAGE_W - MARGIN, 11 * mm, f"Page {doc.page}")
    canvas.setStrokeColor(LINE)
    canvas.setLineWidth(0.5)
    canvas.line(MARGIN, 14 * mm, PAGE_W - MARGIN, 14 * mm)
    canvas.restoreState()


story = []
A = story.append
E = story.extend

# ---------------------------------------------------------------- cover block

A(Paragraph("Testing guest photo delivery", S_TITLE))
A(Spacer(1, 4))
A(Paragraph(
    "Photobooth <b>v0.12.2-ipad</b> &nbsp;&middot;&nbsp; step by step, from "
    "unzipping the build to a guest holding their photos.", S_SUB))
A(Spacer(1, 12))

A(table(
    ["", ""],
    [
        ["What this proves",
         "That a guest can join your wifi, scan a code, and download their own "
         "photos from the booth &mdash; with no Google account, no cloud and no internet."],
        ["How long", "About 45 minutes, including reading."],
        ["What you need",
         "This laptop, <b>one phone with a camera</b>, and the release ZIP. "
         "The Canon camera is optional &mdash; there is a mock camera built in."],
        ["Version to quote",
         "<b>0.12.2-ipad</b>, shown at the foot of the left-hand rail. Please "
         "include it with any result so it cannot be confused with the v0.10.0 build."],
    ],
    [30 * mm, CONTENT_W - 30 * mm]))

A(Spacer(1, 10))

A(callout(
    "About the setup you are using",
    "You have no travel router, so this guide uses <b>the laptop's own Mobile "
    "Hotspot</b> as the guest network. That is fine for a test and is <b>not</b> "
    "how a real event should run &mdash; a hotspot shares the laptop's internet "
    "with guests and stops at 8 devices. None of that matters with one phone in "
    "a room.<br/><br/>"
    "It does mean the laptop ends up on <b>two networks at once</b>, which is "
    "exactly the situation the new address setting exists for. "
    "<b>Stage 6 is the most important stage in this guide.</b>"))

E(rule())

# ------------------------------------------------------------------- stage 1

E(stage(1, "Install and open the booth"))
A(steps([
    "Download <b>photobooth-v0.12.2-win-x64.zip</b> from the release page and "
    "unzip it to somewhere easy, such as your Desktop. Unzip it properly &mdash; "
    "running from inside the zip preview will fail in confusing ways.",

    "Run <b>Photobooth.Server.exe</b>. Windows SmartScreen will warn that it is "
    "unsigned: choose <b>More info</b> then <b>Run anyway</b>. A black console "
    "window stays open; leave it.",

    "Open <b>http://localhost:5000/operator</b> in a browser on this laptop.",

    "Look at the foot of the left-hand rail. It should read <b>0.12.2-ipad</b>. "
    "Write it down now: ________________________",
], 1))

A(Spacer(1, 8))
A(callout(
    "Check before moving on",
    "You can see the operator console, with <b>Session</b>, <b>Templates</b> and "
    "<b>Setup</b> in the rail on the left."))

E(rule())

# ------------------------------------------------------------------- stage 2

E(stage(2, "Switch the booth onto the network",
        "The step that catches everyone. Do not skip it."))

A(callout(
    "Read this first",
    "Guest photos are served on a <b>separate network port</b>, and that port is "
    "only opened <b>when the app starts</b>. Until you do the next two steps, "
    "turning on guest photos will look completely correct in Setup and do "
    "nothing at all &mdash; the codes will point at a door that was never opened."
    "<br/><br/>The setting is filed under <b>Guest display on an iPad</b>, which "
    "sounds like it only concerns iPads. It does not. It is required for guest "
    "photos too, whether or not an iPad is involved.", warn=True))

A(Spacer(1, 8))
A(steps([
    "Go to <b>Setup</b>, find <b>Guest display on an iPad</b>, and press "
    "<b>Serve the display to the network</b>.",

    "<b>Close the app completely</b> &mdash; close the black console window &mdash; "
    "and start <b>Photobooth.Server.exe</b> again. Reopen "
    "<b>http://localhost:5000/operator</b>.",

    "Back in <b>Setup</b>, confirm that section now shows <b>addresses</b> and a "
    "<b>certificate fingerprint</b>, and no longer says a restart is needed."
    "<br/><i>If it still asks for a restart, you are on v0.12.0 or older. That "
    "banner had a bug and could never clear &mdash; guest photos were switched "
    "on regardless. Carry on to Stage 3.</i>",
], 5))

E(rule())

# ------------------------------------------------------------------- stage 3

E(stage(3, "Say where finished photos are saved"))
A(steps([
    "In <b>Setup</b>, set the <b>Output folder</b> to somewhere findable, such "
    "as <b>C:\\Users\\you\\Pictures\\PhotoboothSessions</b>. Press <b>Check</b>, "
    "then save. Each finished session becomes one folder in here.",

    "If you are using the <b>Canon camera</b>: start EOS Utility in remote "
    "shooting mode, note the folder it saves to, and paste that into "
    "<b>Watch folder</b>. It must be a different folder from the output one."
    "<br/><i>Skipping the camera? Leave the watch folder alone and use the mock "
    "camera in Stage 8.</i>",
], 8))

E(rule())

A(PageBreak())

# ------------------------------------------------------------------- stage 4

E(stage(4, "Start the laptop's hotspot",
        "This becomes the network your guests join."))
A(steps([
    "Open Windows <b>Settings</b> &gt; <b>Network &amp; internet</b> &gt; "
    "<b>Mobile hotspot</b>.",

    "Under <b>Properties</b>, press <b>Edit</b> and read off the "
    "<b>Network name</b> and <b>Network password</b> exactly as written, "
    "including capital letters. Write them here:"
    "<br/><br/>Network name: ______________________________"
    "<br/><br/>Password: __________________________________",

    "Turn <b>Mobile hotspot</b> on.",
], 10))

A(Spacer(1, 8))
A(callout(
    "Why exactly as written",
    "The booth cannot see what wifi networks exist, so it cannot check what you "
    "type. A name with one wrong letter produces a join code that scans "
    "perfectly and then does nothing, with no error anywhere. That is the single "
    "most common reason this feature appears broken."))

E(rule())

# ------------------------------------------------------------------- stage 5

E(stage(5, "Tell the booth about that wifi"))
A(steps([
    "In <b>Setup</b>, find <b>Guest photos over your wifi</b> and press "
    "<b>Let guests download their photos</b>.",

    "Type the <b>Wifi network</b> and <b>Wifi password</b> from step 11, exactly "
    "as you wrote them down.",

    "Press <b>Save wifi details</b>.",
], 13))

E(rule())

# ------------------------------------------------------------------- stage 6

E(stage(6, "Tell the booth which address to advertise",
        "The new setting, and the one this whole test is really about."))

A(callout(
    "Why this matters",
    "With the hotspot running, this laptop now has <b>two</b> addresses: one on "
    "your normal wifi, and one on the hotspot. <b>Your phone can only reach the "
    "hotspot one.</b> The photos code can carry only a single address, so if the "
    "booth picks the other one, the code will scan, open a browser, and hang on "
    "a page that never loads &mdash; looking exactly like a broken app."
    "<br/><br/>Older builds guessed. This build lets you choose."))

A(Spacer(1, 8))
A(steps([
    "Still under <b>Guest photos over your wifi</b>, find "
    "<b>Guests reach the booth at</b>. You should see a warning that the booth "
    "is on 2 networks and is guessing. <b>That warning is correct</b> &mdash; it "
    "is the feature working.",

    "Open the dropdown. Pick the address that <b>starts with 192.168.137</b> "
    "&mdash; that is always the hotspot. Write it here: ____________________",

    "Check the <b>Guests reach</b> line just below now shows that address, and "
    "the warning has gone. No restart needed.",
], 16))

A(Spacer(1, 8))
A(callout(
    "If you do not see two addresses",
    "The hotspot is not running. Go back to step 12. If you see an address "
    "starting <b>169.254</b> anywhere, tell us &mdash; those are supposed to be "
    "filtered out and never offered."))

E(rule())

# ------------------------------------------------------------------- stage 7

E(stage(7, "Join your phone to the hotspot"))
A(steps([
    "On the phone, turn <b>mobile data off</b>. This matters: with it on, you "
    "will not be able to tell what is reaching the booth and what is going out "
    "over cellular.",

    "Join the hotspot network from the phone's wifi settings, using the name and "
    "password from step 11.",

    "Expect the phone to complain about the network having no internet, or to "
    "offer to switch back to mobile data. <b>Tell it to stay connected.</b> "
    "This is normal and expected.",
], 19))

E(rule())
A(PageBreak())

# ------------------------------------------------------------------- stage 8

E(stage(8, "Take a session"))
A(steps([
    "Go to <b>Session</b> in the rail and press <b>Start session</b>.",

    "<b>With the Canon:</b> press the BR-E1 remote on the &quot;1&quot; of each "
    "countdown, once per photo."
    "<br/><b>Without a camera:</b> use <b>Mock camera</b> in the rail and press "
    "<b>Simulate press</b> once per photo. It stands in for the remote.",

    "Review the shots when prompted, retake any you want, and let the session "
    "finish. It is done when the screen shows the finished strip.",

    "Open your output folder and confirm a new folder appeared, holding the "
    "strip and the individual photos.",
], 22))

E(rule())

# ------------------------------------------------------------------- stage 9

E(stage(9, "Delivery: the guest gets their photos",
        "The part that matters. Everything above was setup."))
A(steps([
    "Look at the <b>guest screen</b>. Open <b>http://localhost:5000/display</b> "
    "in a second browser window if you do not have a second monitor. At the end "
    "of a session it shows <b>two codes</b>.",

    "Point the phone's camera at the <b>first code</b> (join the wifi). You are "
    "already joined, so this should simply offer the network again &mdash; that "
    "is enough to prove the code is valid.",

    "Point the phone's camera at the <b>second code</b> (your photos) and open "
    "the link it offers.",

    "<b>The page should load and show that session's photos.</b> Record what "
    "happened: ______________________________________________",

    "Tap one photo. It should download and appear in the phone's camera roll.",

    "Tap <b>Download all as a zip</b>. Open the zip on the phone and confirm it "
    "holds the strip and every photo, and that they open.",
], 26))

A(Spacer(1, 8))
A(callout(
    "If the page does not load",
    "Almost always the address in Stage 6. Go back to step 17 and confirm you "
    "picked the <b>192.168.137</b> one. If you did, tell us &mdash; that is a "
    "real finding and the most valuable result you can send back."))

E(rule())

# ------------------------------------------------------------------ stage 10

E(stage(10, "Check a guest cannot reach anything else",
        "Type these into the phone's browser."))
A(steps([
    "Take the photos link and <b>change one character in the long code at the "
    "end</b>. It must say it cannot find those photos.",

    "Run a <b>second session</b>, then reopen the <b>first</b> link on the "
    "phone. It must still show the first session's photos, not the second's.",

    "Replace everything after the port number with <b>/operator</b>. You will "
    "get a page &mdash; that is expected, it is the iPad setup page. "
    "<b>Read it.</b> The test is whether it is the operator console with your "
    "session controls on it. It must not be.",
], 32))

E(rule())

# ------------------------------------------------------------------ stage 11

E(stage(11, "Break it on purpose",
        "Please do not skip this. It is the most useful part of the test."))

A(Paragraph(
    "Every problem this feature has had was something that <b>looked fine and "
    "was not</b>. These three cause the failures deliberately, so you learn to "
    "recognise them in thirty seconds instead of twenty minutes.", S_BODY))
A(Spacer(1, 4))

A(steps([
    "<b>The disappearing address.</b> With an address picked in Stage 6, turn "
    "the Mobile hotspot <b>off</b>. Reload <b>Setup</b>."
    "<br/>Expected: a warning naming the address it can no longer find, and the "
    "<b>Guests reach</b> line falling back to something else rather than still "
    "showing the dead one."
    "<br/>What happened: ______________________________________",

    "<b>The wrong network name.</b> Turn the hotspot back on. In Setup, change "
    "the <b>Wifi network</b> to <b>NotARealNetwork</b> and save. Run a session "
    "and scan the join code."
    "<br/>Expected: the phone does nothing, or says it cannot find the network. "
    "<b>This is the symptom to memorise</b> &mdash; a code that scans fine and "
    "then nothing happens means the name is wrong."
    "<br/>What happened: ______________________________________",

    "<b>Put it back.</b> Set the real network name again, save, and confirm a "
    "fresh session's join code works.",
], 35))

E(rule())
A(PageBreak())

# -------------------------------------------------------------- troubleshoot

A(Paragraph("If something does not work", S_STAGE))
A(Spacer(1, 6))

A(table(
    ["What you see", "What it usually means"],
    [
        ["Setup shows no <b>Guests reach the booth at</b> at all",
         "Guest photos are switched off. Stage 5, step 13."],

        ["Only one address in the dropdown",
         "The hotspot is not running. Stage 4, step 12."],

        ["Scanning the join code does nothing whatsoever",
         "The network name in Setup does not match a network that is actually "
         "broadcasting. Check it character by character against the phone's own "
         "wifi list."],

        ["The phone offers to join but fails",
         "The password is wrong. Try joining by hand to check it."],

        ["Joined, but the photos page never loads",
         "<b>First</b> check the address in Stage 6 is the 192.168.137 one. "
         "<b>Then</b> check the phone is actually on the hotspot and not back on "
         "mobile data."],

        ["The page worked and then stopped",
         "An Android phone has switched itself back to mobile data. Rejoin and "
         "tell it to stay."],

        ["&quot;We cannot find those photos&quot;",
         "The code is mistyped, or that session's folder has been deleted."],

        ["The phone says there is no internet",
         "Expected. The photos page still works. At a real event guests need to "
         "be told this or they assume the booth is broken."],
    ],
    [58 * mm, CONTENT_W - 58 * mm]))

E(rule(space_before=14))

# ------------------------------------------------------------------- results

A(Paragraph("What to send back", S_STAGE))
A(Spacer(1, 6))
A(Paragraph(
    "A &quot;that did not work&quot; is a more useful result than a pass. If "
    "something failed <b>silently</b> &mdash; looked correct and did nothing "
    "&mdash; say so even if you found a way around it. Those are the ones that "
    "cost a real event.", S_BODY))
A(Spacer(1, 8))

A(tickbox_rows([
    "The version string from step 4",
    "Stage 6: did the &quot;on 2 networks and is guessing&quot; warning appear?",
    "Stage 9: did the photos page load on the phone, and did the zip open?",
    "Stage 10: was anything reachable that should not have been?",
    "Stage 11: did each of the three failures announce itself on screen?",
    "Anything that looked correct and did nothing",
    "Anywhere the wording on screen sent you in the wrong direction",
]))

A(Spacer(1, 12))
A(Paragraph("Notes", S_CELL_B))
A(Spacer(1, 4))

# six lines, which is what fits without spilling a near-empty page 8
_notes = Table([[""] for _ in range(6)], colWidths=[CONTENT_W], rowHeights=[9 * mm] * 6)
_notes.setStyle(TableStyle([
    ("LINEBELOW", (0, 0), (-1, -1), 0.5, LINE),
    ("TOPPADDING", (0, 0), (-1, -1), 0),
    ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
]))
A(_notes)

doc = BaseDocTemplate(
    OUT, pagesize=A4,
    leftMargin=MARGIN, rightMargin=MARGIN,
    topMargin=MARGIN, bottomMargin=22 * mm,
    title="Photobooth v0.12.2 - Guest photo delivery test guide",
    author="Photobooth",
    subject="Step-by-step test guide, setup through delivery",
)
frame = Frame(MARGIN, 22 * mm, CONTENT_W, PAGE_H - MARGIN - 22 * mm, id="body")
doc.addPageTemplates([PageTemplate(id="main", frames=[frame], onPage=furniture)])
doc.build(story)

print("wrote", OUT)
