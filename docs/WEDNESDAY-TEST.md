# Test flow: Mac booth, preview monitor, Windows claiming station

Two computers and a monitor. **The MacBook is the booth**: it takes the photos, builds every
strip and stores everything. An **external monitor on the Mac** is the guest screen. The
**Windows laptop is the claiming station**: it reads the Mac's shared folder and shows each
guest's strip by number. The router connects them and the guests' phones. None of this
needs the internet.

## How a guest moves through it

| | Where | What happens |
|---|---|---|
| 1 | The queue | A sign with the **join-wifi QR**. They join the booth wifi while they wait |
| 2 | Centre stage | They frame on the **camera's own viewfinder** and take **4 shots**. The monitor counts down; staff press the BR-E1 |
| 3 | The monitor | It shows **their finished strip**. Staff at the Mac press **Accept**, or **Retake this one** |
| 4 | The monitor | After Accept it shows a large **number** (1, 2, 3..., restarting each day) and *"Tell this number at the claiming station"*. The number is also on the strip. **No QR here** |
| 5 | The claiming station | The guest says their number. The operator types it on the **Claiming Station** page, hovers the guest's strip, clicks **Show QR**, and the guest scans it to download **4 photos, the strip and the video** |

```
 Canon R50 --USB--> Booth MacBook --cable--> Router (WAN empty) <--wifi-- guests' phones
                    booth app, EOS Utility,       ^
 [external monitor] all sessions, shared (SMB)    '-- Windows laptop  /pickup  claiming station
  = guest screen
```

---

## Part 1: Set up

The detail is in [EVENT-SETUP.md](EVENT-SETUP.md) section 1b and the Mac section 5. This is the
short path. Tick in order.

### A. The Mac
- [ ] Mains power; display off **Never**; **FileVault on**; `brew install ffmpeg` done and `ffmpeg -version` prints a version (without it there is no video).
- [ ] Canon **EOS Utility 3 for macOS**, camera tethered, **Photos / Image Capture closed**, destination folder = the booth's watch folder. **Check the macOS version against Canon's list** (latest build lists macOS 26, 15 and 14); Test 1 proves it.
- [ ] Booth app running (`dotnet run --project src/Photobooth.Server`, or the Mac ZIP). Allow incoming connections when asked. `http://localhost:5000/operator` opens.

### B. Router
- [ ] Travel router, **WAN empty**, mode Router, WPA2, client isolation and guest mode **off**. Mac and Windows laptop both on it (cable preferred).

### C. Mac settings (Settings page)
- [ ] **Watch folder** = EOS Utility's destination; **Output folder** is a different folder. **Check**, then **Save**.
- [ ] **Photos per strip: 4.** (The shipped frame has three windows; at 4 the frame art is dropped and you get a plain 4-photo strip. A branded 4-window frame needs new art.)
- [ ] **Let guests download their photos**: on. Wifi name and password **exactly** as on the router. **Save wifi details.** Print the join-wifi QR that appears for the queue sign.
- [ ] **Hand over by number**: on. **Use a separate claiming station**: off.
- [ ] **Share the Output folder**: System Settings > General > Sharing > File Sharing, add the folder, give the Windows laptop's user Read & Write. Note the Mac's address.

### D. The monitor
- [ ] External monitor connected and made the **main display**. Run `tools/mac-display.command --local`. It shows *Step in and smile* fullscreen. (No iPad, certificate or HTTPS.)

### E. The Windows laptop
- [ ] Windows build installed and run. Allow it on **Private networks**.
- [ ] Connect to the Mac's share (`\\<Mac address>\<share>`), remember the credentials, map it to a drive letter that reconnects at sign-in.
- [ ] Settings on the Windows laptop (`http://localhost:5000/diagnostics`): **Output folder** = the shared folder (**Check**, **Save**); **Let guests download their photos** on with the **same wifi name and password**; **Guests reach the booth at** = this laptop's address on the router; **Serve the display to the network** on; **This computer is the claiming station only** on. **Restart the app.**
- [ ] `http://localhost:5000/pickup` opens and shows *Claiming station* where the camera status was.

---

## Part 2: The test run

Do these in order. Stop at the first failure and note it.

1. **Camera and EOS Utility on this Mac.** Section 0 of [QA-CHECKLIST.md](QA-CHECKLIST.md): ten BR-E1 presses over five minutes, each landing a JPEG in the watch folder, camera **Ready**. **If EOS Utility will not run or see the R50 on this macOS, stop and use the Windows laptop as the booth for the event**, and report the macOS version.
2. **The monitor** counts down with the operator console and shows each shot.
3. **One full session, 4 shots.** After the last shot the monitor shows the strip. **Reorder** two shots and **Retake this one**: the strip must change to match. Press **Accept**. The monitor shows a **large number and no QR**. Open `strip.jpg` in the session folder: **No. N** at the foot, the same N.
4. **The claiming station sees it.** On the Windows laptop, `/pickup` shows that guest, labelled with the same number, **within about 3 seconds**. Time it: ____ s.
5. **Find a guest.** Type the number: only that guest remains. Hover the strip: **Show QR** appears. Click: a large QR with *"Scan to download 4 photos, the strip and the video"* and the wifi name. **Close** returns to the grid. Clear the box: everyone is back.
6. **Numbers.** Take three more guests: they are numbered consecutively. **Restart the Mac's booth app**, take one more: the next number carries on, with no repeat.
7. **Two guests back to back.** Start guest 2 *while guest 1 is still on the claiming station*. Both appear, correctly numbered; guest 1's QR opens guest 1's photos.
8. **Download on two phones, one iPhone and one Android**, joined to the wifi from the queue sign:
   - **Time it**: Show QR to strip visible ____ s; to all photos ____ s.
   - The page offers **4 photos, the strip and the video**. Tap a photo, long-press, **Add to Photos / Download image**. Tap the video, **Save Video**. **Download all** gives a zip with all of them.
   - Post the saved video to Instagram and Facebook.
   - A phone **not** on the wifi: the QR fails to open, and the claiming station shows the wifi name.
9. **The queue sign**: from a phone not yet on the wifi, scan it. It joins without asking for a password.
10. **Several phones at once** (5+), all downloading. Note when it slows: ______.
11. **Sizes** of one session's `photo-1.jpg`, `thumb-1.jpg`, `strip.jpg`, `strip.mp4`.
12. **Security.** From a phone on the guest wifi, `https://<Windows laptop address>:5002/api/pickup` and `.../api/sessions` must both show *not found*: the list of guests is only on the Windows laptop itself.
13. **Shared folder under load.** Take guests quickly on the Mac while watching the Windows grid: every guest appears, none half-loaded (a strip with no photo behind it).
14. **Mac off.** Quit the booth app on the Mac and wait a minute: the Windows grid keeps showing guests already taken, and their downloads still open (as long as the Mac is on and sharing).
15. **Restart the Windows app** and reload `/pickup`: the grid comes back with the same numbers.

## If the Mac cannot be the booth

The Windows laptop can be the booth as before (Windows steps in [EVENT-SETUP.md](EVENT-SETUP.md)
section 5), with **Hand over by number** on. Then it is both booth and claiming station: its own
`/pickup` page lists its own guests, and no share is needed.

## What to bring back

- Pass/fail for each step, the times from steps 4 and 8, and the **macOS version**.
- Whether EOS Utility ran and tethered the R50 on that macOS, and whether the BR-E1 worked while tethered.
- Whether the Windows grid kept up with the Mac over the share: any guest slow to appear, appearing with a missing photo, or any dropped connection.
- The diagnostics bundle from the Mac (`/diagnostics`) and the version at the foot of the operator rail.
- Whether the video posted on Instagram and Facebook.
- Whether the number flow made sense to guests and operator: was the number easy to remember and say, how long did the operator take to find a guest.
- Whether the 4-photo strip, without the frame, is acceptable, or a 4-window frame is needed.
