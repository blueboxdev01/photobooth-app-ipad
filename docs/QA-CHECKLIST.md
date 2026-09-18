# QA checklist — before an event

Work top to bottom. Tick what passes, write down what does not.

**Budget half a day**, and do it on the machine and screen you will actually use,
with the camera you will actually use. Most of what has bitten this project so
far was environment, not code.

---

## What this build cannot do

Not gaps to test — features that do not exist. Do not plan an event around them.

| | |
|---|---|
| **Printing** | Not built. There is no print path at all |
| **Self-serve / kiosk** | Not built. A person must press the shutter |
| **App-controlled shutter** | Impossible on this path. EOS Utility owns the camera; the app only watches a folder |
| **Editing a strip after Accept** | Once accepted, the strip is composed. Re-run the guest to change it |

---

## Section 0 — The three that could change your plans

Do these first. If any fails, the rest of the day is worth reorganising around it.

### 0.1 The BR-E1 remote while USB-tethered

Canon's own advice is to disable Bluetooth before connecting USB, which is the
opposite of what the remote needs. **This has never been verified.**

1. Pair the BR-E1.
2. Connect USB, EOS Utility in remote-shooting mode saving to the watch folder.
3. Press the remote.

- [ ] A JPEG lands in the watch folder
- [ ] It still works on the tenth press, five minutes later

**If it fails:** fall back to the camera's own shutter button. Nothing else in
the design changes, but you are standing at the booth all night.

Result: ______________________________________________

### 0.2 Power

The R50 will **not** power itself over USB-C while tethered.

- [ ] DR-E18 / LP-E17 dummy battery fitted and powering the camera
- [ ] Camera still alive after **one hour** tethered and idle
- [ ] Laptop on mains, sleep and hibernate disabled

### 0.3 A photo actually becomes a strip

- [ ] Full session end to end, from remote press to a finished strip on disk

If this works, everything else on this list is refinement.

---

## Section 1 — Setup

**Settings → Folders**

- [ ] Watch folder matches EOS Utility's save location **exactly**
- [ ] Press **Check** — reports usable, not just "exists"
- [ ] Output folder is somewhere you can find (`Pictures\PhotoboothSessions`, not inside the app folder)
- [ ] The two folders are **different**
- [ ] Free disk space is at least 2 GB, ideally 10× your expected sessions × 30 MB

**Settings → Strip layout**

- [ ] Output size chosen, and it is the size you are actually delivering
- [ ] Photos per strip set; min and max bound what the event allows
- [ ] Slots look evenly placed in **Templates**

**Settings → Timings**

- [ ] Countdown set to what suits your guests (default 3s)
- [ ] No-photo timeout longer than the real press-to-file latency you measured in 0.1

**Settings → Guest display**

- [ ] Backdrop colour or image set for the event

---

## Section 2 — Ingest, and the ways it goes wrong

Every one of these has a deliberate behaviour. None should crash, and none should
put a wrong photo on a strip.

| Do this | Expected | ✓ |
|---|---|---|
| Press the remote twice fast | Two distinct photos, no duplicate | ☐ |
| Unplug USB mid-session | Visible error, no crash | ☐ |
| Close EOS Utility mid-session | Session times out with an explanation | ☐ |
| Press nothing for 30s | "No photo after Ns…", then **Keep waiting** works | ☐ |
| Copy an old JPEG into the watch folder | Ignored as stale | ☐ |
| Drop a huge file in slowly | Waits for it to finish, never reads a half-written file | ☐ |
| Fill the watch folder with 200 old files, start a session | Only new ones count | ☐ |

- [ ] **Settings → Ingest decisions** names a reason for every rejection

---

## Section 3 — A normal session

- [ ] Both screens stay in step throughout
- [ ] Countdown is readable across the booth
- [ ] Every photo appears, right way round, not duplicated
- [ ] Shot counter is correct ("photo 2 of 4")
- [ ] Accept produces a strip within a few seconds

---

## Section 4 — Retake

**Retake one shot** *(review, per thumbnail)*

- [ ] Retake shot 2 of 4 → the booth waits for that pose again
- [ ] Both screens say **photo 2**, not "4 of 4"
- [ ] The replacement lands in **slot 2**
- [ ] Shots 1, 3 and 4 keep their photos **and** their positions
- [ ] Retake the same slot twice in a row
- [ ] Retake the first and the last slot

**Retake last** *(the rail button)*

- [ ] Drops the shot taken **most recently**, whatever position it sits in
- [ ] Its replacement returns to the same slot

**The two together**

- [ ] Reorder the strip, then retake a shot — the arrangement survives

---

## Section 5 — Reordering

- [ ] Drag a thumbnail; the order changes
- [ ] Arrows `‹ ›` move a shot (this is the touchscreen path — drag does nothing on touch)
- [ ] Each thumbnail still says which shot it is ("shot 4") after moving
- [ ] **Back to capture order** restores it
- [ ] The finished strip is in the order you arranged
- [ ] In the output folder, `photo-1.jpg` is the shot you put first

---

## Section 6 — Templates and art

- [ ] Upload a **backdrop** (opaque) — photos appear **on top** of it
- [ ] Upload a **frame** (PNG, transparent windows) — drawn **over** the photos
- [ ] The editor's detection matches what you intended; override works if not
- [ ] Upload art at the **wrong** size — centre-cropped, not distorted
- [ ] Drag a slot, type exact sizes, **Make every slot this size**
- [ ] **Render preview** matches what a real session produces
- [ ] Changing photo count or output size re-lays slots and warns that art detaches

---

## Section 7 — The strip

- [ ] Correct pixel size and **300 DPI** for the chosen output
- [ ] Photos not stretched — cropped, and faces are not cut badly
- [ ] Look at one **at full size** on screen, not a thumbnail

**In the output folder, per session:**

- [ ] Its own folder, named by date and time
- [ ] `strip.jpg`, one `photo-N.jpg` per shot, `session.json`
- [ ] `qr.png` *(only if Drive is on)*
- [ ] The originals are **still in the watch folder**, untouched

---

## Section 8 — Guest display

On the actual monitor, at its actual resolution, fullscreen.

- [ ] Attract screen reads **Step in and smile**, with the empty slots below
- [ ] Countdown legible from **two metres** — it should fill the screen
- [ ] Each shot appears in its slot as it is taken
- [ ] Review shows the shots large enough to judge from where a guest stands
- [ ] Finished strip and its QR are both readable from a normal distance
- [ ] Nothing important is cut off by overscan
- [ ] Backdrop colour or image appears

**There is no live preview.** The screen shows only what the booth knows: the
count, the shots already taken, and the strip. A guest cannot check their
framing before the shutter fires, so the thing to judge here is whether the
booth is marked well enough that they stand in the right place to begin with.

- [ ] Guests end up framed without being told where to stand, **or** the floor
      is marked

---

## Section 8b — The iPad as the guest display

Skip if you are using a monitor. Settings is **[IPAD-DISPLAY.md](IPAD-DISPLAY.md)**.

- [ ] Certificate installed **and trusted** under Certificate Trust Settings
- [ ] Display opens with a **padlock**, and the attract screen appears
- [ ] Added to the Home Screen and launched from there — fullscreen, no Safari bars
- [ ] Guided Access on, so a guest cannot exit the display
- [ ] Left idle **15 minutes** — the screen does not dim or sleep

**What must not be reachable from the iPad.** Try each; all should fail:

| On the iPad, open | Expected | ✓ |
|---|---|---|
| `https://<booth>:5002/operator` | Not found | ☐ |
| `https://<booth>:5002/diagnostics` | Not found | ☐ |
| `https://<booth>:5002/api/settings` | Not found | ☐ |
| `https://<booth>:5002/api/session/abort` | Not found | ☐ |

**Surviving the real world:**

- [ ] Restart the booth — the iPad reconnects with **no re-install**
- [ ] Join a different network — still works, nothing to redo
- [ ] Turn the wifi off mid-session — the guest screen freezes but the session
      completes, photos intact
- [ ] You know the fallback: plug in a monitor, open `/display` on the laptop

---

## Section 8c — Guests downloading their own photos

Skip if you are handing photos over by hand. Settings is
**[GUEST-PHOTOS.md](GUEST-PHOTOS.md)**.

From **your own phone**, on the booth's wifi:

- [ ] The join code actually joins the network, without typing a password
- [ ] The photos code opens **your** session
- [ ] The strip and every photo are there
- [ ] Download one — it opens, and is named for the session
- [ ] **Download all as a zip** — it opens, and holds every file
- [ ] Run a second session: its code shows different photos

**What a guest must not reach.** Try each from the phone; all should refuse:

| On the phone, open | Expected | ✓ |
|---|---|---|
| The link with one character changed | "We cannot find those photos" | ☐ |
| `.../session.json` on your own link | Not found | ☐ |
| `http://<booth>:<port>/operator` | The iPad setup page, **not** the console | ☐ |
| `http://<booth>:<port>/api/session/abort` | Same, and the session keeps running | ☐ |

- [ ] Leave the wifi, then open the link again — it should stop working, and you
      should be comfortable explaining that to a guest

---

## Section 9 — Delivery and the QR

Skip if you are running without Google Drive.

**Settings → Guest delivery**

- [ ] Account shown is the **booth** account, not a personal one
- [ ] Consent screen is **In production**, not Testing — otherwise the sign-in dies after 7 days, mid-event
- [ ] Uploading: On

**A session**

- [ ] QR appears on the guest screen; time it — seconds, not tens of seconds
- [ ] **Scan it from a phone on mobile data, not the venue wifi**
- [ ] The folder holds the strip, the raws, and `qr.png`
- [ ] A second session gives a **different** link showing different photos
- [ ] In Drive, sessions sit inside the one `Photobooth` folder
- [ ] `qr.png` in the folder scans to that same session

**When it breaks**

| Do this | Expected | ✓ |
|---|---|---|
| Turn wifi off mid-upload | Session completes, marked waiting, drains on reconnect | ☐ |
| Run a session fully offline | Completes; photos intact; publishes later from Setup | ☐ |
| Close the app mid-upload, reopen | Picks up where it left off, no duplicate files in Drive | ☐ |
| Sign out, run a session | Loud banner; photos safe; **Try again** works after signing in | ☐ |

---

## Section 10 — Endurance

The one that finds what single sessions never do.

- [ ] **20 sessions back to back** without restarting the app
- [ ] Memory does not climb session over session
- [ ] Camera does not overheat or drop off USB
- [ ] Every session produced a complete folder
- [ ] All uploads drained to zero waiting
- [ ] Disk space still healthy

---

## Section 11 — Data and privacy

- [ ] Guest photos exist in **two** places — this laptop and the Drive account. You are content with both
- [ ] Drive folders are "anyone with the link" — a forwarded link works for whoever holds it
- [ ] No guest names in folder names
- [ ] You have a retention period in mind, covering the laptop, Drive, and any backup
- [ ] **Settings → Download diagnostics bundle** — confirm it contains **no photographs** and no credentials

---

## Section 12 — Recovery

Things that will happen at an event.

| Do this | Expected | ✓ |
|---|---|---|
| Kill the app mid-session, restart | Comes back clean; earlier sessions intact | ☐ |
| Restart with a session waiting to upload | It uploads without being asked | ☐ |
| Close the guest display browser window, reopen | Reconnects to the running session | ☐ |
| Change the watch folder mid-event | Applies immediately, no restart | ☐ |

- [ ] You know how to restart everything from cold in under two minutes

---

## Section 13 — Upgrading

Only if you are moving off an older build.

- [ ] Unzipped **alongside** the old folder, not over it
- [ ] `data/` copied across — past sessions, settings, Drive sign-in
- [ ] `templates/` copied across
- [ ] Old sessions still listed, old Drive links still open
- [ ] Version at the foot of the rail is the one you meant to run

---

## Go / no-go

| | |
|---|---|
| Section 0 all passed | ☐ |
| A full session works end to end | ☐ |
| 20 back-to-back sessions survived | ☐ |
| Photos land somewhere you can find them | ☐ |
| You can restart from cold under pressure | ☐ |

**Anything unresolved:**

______________________________________________________________

______________________________________________________________

**Version tested:** ____________ *(foot of the left-hand rail)*

**Date / tester:** ____________________________________________
