# Wednesday test: queue, shoot, preview, claim

Three machines and a tablet, one rule: **everything that matters runs on the
Windows laptop.** The iPad and the MacBook are only screens, and the router only
connects them to the guests' phones. None of this needs the internet.

## How a guest moves through it

| | Where | What happens |
|---|---|---|
| 1 | The queue | A sign with the **join-wifi QR**. They join the booth wifi while they wait |
| 2 | Centre stage | They frame on the **camera's own viewfinder** (the iPad has no live view) and take 3-4 shots |
| 3 | The iPad | It shows **their finished strip**. Staff at the Windows laptop press **Accept**, or **Retake this one** on a single shot |
| 4 | The claiming station | After Accept the iPad says *"Collect your photos at the claiming station"*. The **MacBook** shows their strip and a **QR**; they scan it with their phone and download |

The MacBook keeps a guest on screen for five minutes after they finish, even while the
next guest is already posing, and shows up to three at a time, newest largest.

```
 Canon R50 --USB--> Windows laptop --cable--> Router (WAN empty) <--wifi-- guests' phones
                    booth app, EOS Utility,       ^
                    all the files                 |-- iPad       /display  (the booth screen)
                    operator console              '-- MacBook    /claim    (the claiming station)
```

---

## Set everything up first

Follow **[EVENT-SETUP.md](EVENT-SETUP.md)** end to end: the router, the camera, the Windows
laptop (from the v0.18.0 ZIP), the booth settings, the iPad, the MacBook and the queue sign.
Its section 10 is the ten-minute check to pass before you start the test below.

The ZIP is here:
<https://github.com/blueboxdev01/photobooth-app-ipad/releases/download/v0.18.0/photobooth-v0.18.0-win-x64.zip>

---

## The test run

Do these in order. Stop at the first failure and note it.

1. **Camera**: Section 0 of [QA-CHECKLIST.md](QA-CHECKLIST.md) (remote works while tethered,
   camera stays powered, a press becomes a strip).
2. **iPad**: loads with a padlock, shows *Step in and smile*, counts down with the operator console.
3. **One full session** with the real camera. After the last shot:
   - the iPad shows the strip within about a second (it first shows the shots and
     *"Putting your strip together…"*);
   - **reorder** two shots and **Retake this one** on a shot: the strip on the iPad must
     change to match, and must never show the old order;
   - press **Accept**: the iPad says to collect at the claiming station; the MacBook shows the
     strip and a QR.
4. **Two guests back to back** — the important one. Press **Start session** for guest 2
   *while guest 1 is still on the MacBook*. The MacBook must **keep guest 1**, the iPad must
   show guest 2's countdown, and guest 1's photos must open guest 1's pictures.
5. **Claim on two phones, one iPhone and one Android**, joined to the wifi in the queue:
   - **Time it**: Accept → QR on the MacBook; scan → strip visible; scan → all photos visible.
   - Tap a photo, long-press, **Add to Photos / Download image**. Tap the video, **Save Video**.
   - Post the saved video to Instagram and Facebook.
6. **Several phones at once** (5+), all claiming together. Note when it slows.
7. **Sizes**: in `data\sessions\<session>\` note the size of `photo-1.jpg`, `thumb-1.jpg`,
   `strip.jpg`, `strip.mp4`.
8. **Security check** from a phone on the guest wifi: `https://<laptop>:5002/api/sessions`
   and `https://<laptop>:5002/api/session/accept` must both show *not found*.
9. **Android hopping back to mobile data**: leave a phone connected for 5 minutes.

## If the new build is not ready

v0.17.0 works with the MacBook simply opening `/display` (run `mac-display.command
<address> --display`). After Accept it shows the strip and the QR, and **keeps them until
staff press Start for the next guest** — so staff wait for the guest to scan before pressing
Start. There is no strip preview on the iPad in that build; it shows the separate shots.

## What to bring back

- Pass/fail for each step above, and the times from step 5.
- The diagnostics bundle from the Windows laptop (`/diagnostics`) and the version at the foot
  of the operator rail.
- Whether the video posted on both Instagram and Facebook.
- Whether the retake flow felt right to staff — "redo all the shots" is not built yet.
