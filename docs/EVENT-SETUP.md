# Setting up the photobooth, start to finish

For build **v0.18.0** or newer. Follow it top to bottom the first time; after that
the checklist in section 10 is all you need on the day.

**Allow about two hours the first time**, most of it the camera and the router.
Nothing here needs the internet, and the booth network deliberately has none.

---

## 1. What you are building

```
 Canon R50 --USB--> WINDOWS LAPTOP --cable--> ROUTER (WAN port empty) <--wifi-- guests' phones
 (BR-E1 remote)     the booth app, EOS Utility,        ^
                    all the photos, the operator       |-- iPad      /display  the booth screen
                    console, staff work here           '-- MacBook   /claim    the claiming station
```

| Device | Job | What runs on it |
|---|---|---|
| **Windows laptop** | The whole booth. Stores every photo. Staff sit at it | `Photobooth.Server.exe`, Canon EOS Utility |
| **Canon R50 + BR-E1** | Takes the photos. The app cannot fire it | — |
| **Router** | The only network. No internet on it, on purpose | — |
| **iPad** | The booth screen: countdown, then the guest's strip to look over | A browser |
| **MacBook** | The claiming station: shows the QR guests scan to get their photos | A browser |

The iPad and the MacBook are **only screens**. If one dies, nothing is lost: photos and
sessions are all on the Windows laptop.

### How a guest moves through it

1. **Queue.** A sign shows the join-wifi QR; they join while they wait.
2. **Centre stage.** They frame on the camera's own viewfinder (the iPad has no live view). The iPad counts down 3-2-1; **staff press the BR-E1** for each shot. 3 or 4 shots.
3. **Preview.** The iPad shows their finished strip. Staff at the laptop press **Accept**, or **Retake this one** on a shot they want redone.
4. **Claim.** The iPad says *"Collect your photos at the claiming station"*. The MacBook shows their strip and a QR; they scan it with their phone and download.

---

## 2. What to have

- [ ] Windows laptop, charger, **no "N" edition of Windows** (it lacks the H.264 encoder and the video will not be made)
- [ ] Canon EOS R50, **DR-E18 / LP-E17 dummy battery** (the R50 will not power itself over USB-C), a short good USB-C cable, **BR-E1** remote
- [ ] A travel router and its power, plus a LAN cable to the laptop
- [ ] iPad (iPadOS 16.4 or newer) with a stand, charger
- [ ] MacBook with charger; a USB-C Ethernet adapter if you have one
- [ ] The queue sign (section 8), printed
- [ ] Canon EOS Utility installed on the laptop (free from Canon's support site, using the camera's serial)

---

## 3. The router

Do this first; everything else needs it.

1. Power it on and log in to its admin page.
2. Make sure the **WAN port is empty**. That is what makes the network internet-free.
3. Settings that matter:

   | Setting | Value | Why |
   |---|---|---|
   | Mode | **Router** (not repeater or bridge) | It has to run its own DHCP |
   | Wifi band | **5 GHz** for the guest network | Faster, less crowded |
   | Security | **WPA2** (not WPA2/WPA3 mixed) | Fewest phones fail to join |
   | Wifi name and password | Pick simple ones; **write them down** | You type them into the booth later |
   | "Guest network" | **Off** | On a router, *guest* means isolated from the other devices, which blocks the booth |
   | AP / client isolation | **Off** | Same reason |
   | DHCP reservation | A **fixed address for the Windows laptop** (e.g. `192.168.8.100`) | The photos QR then keeps working at every event |

4. Plug the Windows laptop into a router **LAN** port by cable.
5. Optional but better: reserve addresses for the iPad and MacBook too.

---

## 4. The camera

1. Fit the dummy battery. Connect USB-C to the laptop.
2. On the camera: **Settings → USB Connection App → Photo Import/Remote Control**.
3. Set **manual exposure, manual white balance (not AWB), a fixed ISO, JPEG only (not RAW)**, and **auto power-off disabled**.
4. Pair the **BR-E1**: `Wireless features → Connect to Wireless Remote`.
5. On the laptop, start **EOS Utility**, choose **remote shooting**, and **note the folder it saves to**.
6. Press the remote. A JPEG should land in that folder. Press it ten times over five minutes: it must keep working.

> **Unverified so far:** Canon advises turning Bluetooth off before connecting USB, which is what the
> remote needs. If the remote stops working while tethered, use the camera's own shutter button;
> nothing else changes, but someone stands at the camera.

---

## 5. The Windows laptop

### Install the booth

1. Download **`photobooth-v0.18.0-win-x64.zip`**:
   <https://github.com/blueboxdev01/photobooth-app-ipad/releases/download/v0.18.0/photobooth-v0.18.0-win-x64.zip>
2. Right-click the ZIP → **Properties** → tick **Unblock** if it is there → OK. Then **Extract All** to a folder you will keep, such as `C:\Photobooth` (not Downloads).
3. Keep the `ffmpeg` folder where it is, beside the exe. It makes the guest video.
4. Run **`Photobooth.Server.exe`**. If SmartScreen warns about an unsigned program, choose **More info → Run anyway**.
5. When **Windows Firewall** asks, allow it on **Private networks**. If you missed it, allow `Photobooth.Server` under Windows Defender Firewall.
6. Open <http://localhost:5000/operator>.

**Upgrading from an older booth folder?** Copy its `data\` and `templates\` folders into the new one.

### Windows settings

- [ ] Plugged into mains. **Power plan: never sleep or hibernate**, screen never turns off.
- [ ] The router connection's network profile is **Private**.
- [ ] **Mobile Hotspot is off.** It shares the laptop's internet with guests and caps at 8 devices.
- [ ] Optional: exclude the booth's `data\` folder and the EOS Utility folder from Defender real-time scanning.

---

## 6. Booth settings

On the Windows laptop, **<http://localhost:5000/operator>** → **Settings** (bottom of the left rail). Save each section as you go.

### Folders
- **Watch folder**: paste the folder EOS Utility saves to. Press **Check** (it must say *usable*), then **Save watch folder**.
- **Output folder**: somewhere you can find, e.g. `C:\Users\you\Pictures\PhotoboothSessions`. It must be a **different** folder from the watch folder, with at least 2 GB free.

### Strip layout and Timings
- Pick the output size and **photos per strip** (3 or 4). Check the slots in **Templates**.
- Countdown: the default 3 s. The no-photo timeout (default 20 s) must be longer than the real gap between the countdown and the remote press.

### Guest photos over your wifi
1. Press **Let guests download their photos**.
2. **Wifi network** and **Wifi password**: exactly as set on the router, **case included**. Press **Save wifi details**. A wrong name makes a join QR that scans and does nothing.
3. **Guests reach the booth at**: pick the laptop's **router address** (the adapter on the cable, e.g. `Ethernet — 192.168.8.100`). Do not leave it on *Choose automatically* if the laptop is on two networks.
4. Press **Use a separate claiming station**. This makes the iPad send guests to the MacBook instead of showing the QR itself.

### Guest display on an iPad
- Press **Serve the display to an iPad**, then **restart the booth** (close it and run `Photobooth.Server.exe` again). The ports only open at start-up.
- The page now shows two QR codes and the laptop's address. Keep it open for the next two sections.

### Looping video
- Leave it **on**. If a queue builds and you need seconds back, switch it off.

---

## 7. The two screens

Both need to trust the booth's own certificate **once**. It stays valid across venues and restarts.

### iPad (the booth screen)
Detail is in [IPAD-DISPLAY.md](IPAD-DISPLAY.md). In short:
1. Join the router's wifi.
2. On the laptop's Settings page, scan the **first QR** with the iPad and tap **Install the booth certificate**.
3. iPad **Settings → Profile Downloaded → Install** (passcode, then Install again).
4. **Settings → General → About → Certificate Trust Settings** → switch the booth **on**. *This is the step people miss.*
5. Scan the **second QR**. It should show a **padlock** and *Step in and smile*.
6. **Share → Add to Home Screen**, open it from that icon. Optionally Guided Access, and **Auto-Lock → Never**.
7. Stand it next to the camera so the guest sees it from centre stage.

### MacBook (the claiming station)
1. Join the router's wifi (the Mac will warn there is no internet; stay connected). Use a USB-C Ethernet adapter instead if you have one.
2. In Safari or Chrome open `http://<laptop address>:5001/cert`. Open the downloaded `photobooth-ca.crt`.
3. In **Keychain Access**, double-click **Photobooth booth authority** → expand **Trust** → *When using this certificate*: **Always Trust**. Close the window and enter the Mac's password.
4. **System Settings → Lock Screen**: turn the display off **Never** while on power. Keep it plugged in.
5. Run **`tools/mac-display.command`** (copy that file to the Mac; double-click it). First run asks for the laptop's address. It checks the network, then the certificate, then opens `https://<laptop>:5002/claim` fullscreen and keeps the Mac awake. Quit with **Cmd+Q**.
6. It should read *"Your photos will appear here"*.
7. Place it where guests walk **after** the iPad, out of the camera's line of sight, with room for two people to scan.

---

## 8. The queue sign

Print the join-wifi QR so guests are on the network **before** their turn. Make it from `WIFI:S:<name>;T:WPA;P:<password>;;` with any QR generator (or screenshot the one on the Mac's claiming page). Put the wifi name and password in text beside it, and:

> **Your phone will say "no internet". That's normal. Stay connected.**

Without that line, some Android phones quietly drop to mobile data, taking the page with them.

---

## 9. Running it: a staff run-sheet

All of this is on the Windows laptop at **Operator**.

| Step | Staff action | What the guest sees |
|---|---|---|
| 1 | Press **Start Session** | iPad counts down 3-2-1 |
| 2 | Press the **BR-E1** at "1" (the countdown is advisory; the app cannot fire the shutter) | *Hold it…*, then the next countdown |
| 3 | Repeat for each shot | *Photo 2 of 4*, and so on |
| 4 | After the last shot, wait ~1 s | The iPad shows **their strip** |
| 5 | If they want a photo redone: **Retake this one** on that shot (you can also drag shots to reorder, and use **Back to capture order** to undo) | The strip on the iPad updates to match |
| 6 | Press **Accept** | *Making your strip…*, then *Collect your photos at the claiming station* |
| 7 | Press **Start Session** for the next guest as soon as you like | The MacBook **keeps showing the finished guest** for 5 minutes while the next one poses |

- **No photo arrived?** The session times out after 20 s and says so on the console. Press **Wait** to carry on, or **Abort** to drop the session.
- The MacBook shows up to three guests, newest largest, and each leaves after five minutes.
- "Redo all the shots" is not built. Retake works one shot at a time.

### If a guest has already gone
Every session is in the **output folder**, one folder per guest. Their link only works while they are on the booth wifi, so for anyone who left, send `strip.jpg`, the photos or `all.zip` from there.

---

## 10. Before opening: a ten-minute check

On the day, once everything is plugged in:

- [ ] Router on, laptop on its cable, **Photobooth.Server.exe** running, camera tethered
- [ ] Operator console loads; camera status shows **Ready**
- [ ] iPad shows a padlock and *Step in and smile*
- [ ] MacBook shows *Your photos will appear here*
- [ ] One full test session: remote press → strip preview on the iPad → **Accept** → QR on the MacBook
- [ ] A phone that has just joined the wifi: scan the MacBook QR → strip, video and photos open
- [ ] Tap a photo, long-press, **Add to Photos** (iPhone) or **Download image** (Android). Tap the video, **Save Video**
- [ ] The queue sign is up
- [ ] Free disk space is comfortably above what the event needs (about 30 MB per guest)

---

## 11. When something goes wrong

| What you see | Most likely cause | Fix |
|---|---|---|
| Camera status is not **Ready**, no photos arrive | Watch folder does not match where EOS Utility saves, or EOS Utility is closed | Settings → Folders → **Check**. Check EOS Utility is in remote-shooting mode |
| Remote does nothing | BR-E1 lost its pairing while tethered | Press the camera's own shutter; re-pair after the event |
| iPad or Mac cannot load the page | Different network, or the booth was not restarted after switching the display on, or Windows Firewall | Same router? Restart the booth. Allow it on Private networks |
| Browser warns the connection is not private | Certificate installed but not **trusted** | iPad: Certificate Trust Settings → on. Mac: Keychain → Always Trust |
| iPad sits on *Connecting…* | A booth older than v0.12.2 | Use v0.18.0 |
| The join-wifi QR scans and nothing happens | Wifi name in Settings is wrong, or the router is off | Match the name exactly, case included |
| Joined, but the photos page never loads | Booth advertising the wrong address, or router isolating devices | Settings → *Guests reach the booth at*. Turn off the router's guest mode and client isolation |
| The page loaded once then stopped (Android) | Phone switched back to mobile data | Rejoin the wifi and tell it to stay connected |
| The MacBook shows nobody after Accept | Claiming station not switched on, or the Mac's page is old | Settings → *Use a separate claiming station*; reload the Mac page |
| No video on the guest page | `ffmpeg` folder missing, or a Windows "N" edition | Keep `ffmpeg\` beside the exe. Sessions still work without the video |
| Strip preview never appears on the iPad | The iPad is on an older page | Reload it from the Home Screen icon. The shots show meanwhile |

The **Diagnostics** page (`/diagnostics`) shows what the booth saw and why it rejected anything, and can export a bundle that contains no photographs.

---

## 12. Afterwards

- Everything is in the output folder, one folder per guest: `strip.jpg`, `strip.mp4`, `photo-N.jpg`, `thumb-N.jpg`, and a record. Back it up before clearing anything.
- Guests' links stop working once they leave your wifi. That is by design; nothing was uploaded anywhere.
- The guest wifi password is kept in plain text in `data\settings.json`. It is only the guests' wifi, but do not share the folder carelessly.
