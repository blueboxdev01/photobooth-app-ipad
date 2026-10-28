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

1. **Queue.** A sign shows the join-wifi QR; they join while they wait. This is the only place that code appears: the screens at the end show just the photos QR.
2. **Centre stage.** They frame on the camera's own viewfinder (the iPad has no live view). The iPad counts down 3-2-1; **staff press the BR-E1** for each shot. 3 or 4 shots.
3. **Preview.** The iPad shows their finished strip. Staff at the laptop press **Accept**, or **Retake this one** on a shot they want redone.
4. **Claim.** The iPad says *"Collect your photos at the claiming station"*. The MacBook shows their strip and **one** QR, their photos, with the wifi name in words beneath it; they scan it with their phone and download.

---

## 1b. The current setup: Mac booth, preview monitor, Windows claiming station

This is how the booth is run now. The rest of this guide still applies for the router, the
camera and the Mac, but **where it says iPad, read the monitor on the Mac, and where it says the
Windows laptop is the booth, read the MacBook.**

```
 Canon R50 --USB--> MACBOOK --cable/wifi--> ROUTER (WAN empty) <--wifi-- guests' phones
 (BR-E1 remote)     booth app, EOS Utility,        ^
 [external monitor] all the sessions, shared       |-- Windows laptop  /pickup  the claiming station
  = guest screen    over SMB                       '-- (reads the Mac's shared sessions folder)
```

| Device | Job |
|---|---|
| **MacBook** | The whole booth. Takes the photos, builds each guest's strip, stores everything, and **shares its sessions folder**. Staff press Accept here |
| **External monitor on the Mac** | The guest screen. Shows the countdown, the strip, then the guest's **number** |
| **Windows laptop** | The **claiming station**. Reads the Mac's shared folder and shows a grid of every guest by number. Serves the download page to phones |
| **Router** | The only network, no internet on it |

### How a guest moves through it

1. **Queue.** A sign shows the join-wifi QR; they join the booth wifi while they wait.
2. **Centre stage.** They frame on the camera's viewfinder; staff press the BR-E1 for each shot (4 shots). The monitor counts down.
3. **Preview and Accept.** The monitor shows their strip. Staff press **Accept** on the Mac.
4. **A number.** The monitor shows a large **number** (1, 2, 3, restarting each day). The same number is stamped on the strip.
5. **Claim.** The guest goes to the Windows laptop and says their number. The operator types it in the **Claiming Station** page, hovers the guest's strip, clicks **Show QR**, and the guest scans it to download **4 photos, the strip and the video**.

### Set up the Mac (the booth)

1. Everything in the Mac section below (EOS Utility, `brew install ffmpeg`, the app).
2. **Settings** on the Mac:
   - **Guest photos over your wifi**: switch on **Let guests download their photos**, enter the wifi name and password, and press **Save wifi details** (this prints the queue-sign QR too).
   - Press **Hand over by number**. (This is what gives each guest a number and puts it on the strip and the monitor.)
   - Leave **Use a separate claiming station** off. The Windows laptop does not use it.
   - **Photos per strip: 4.** See the note below about the frame art.
3. **Share the sessions folder.** System Settings > General > Sharing > **File Sharing**: add the booth's **Output folder** (shown on Settings, usually `.../data/sessions` beside the app) as a shared folder, and give the Windows laptop's user **Read & Write** (the app only reads, but Settings' *Check* button tests by writing a file). Note the Mac's address, for example `smb://192.168.8.101`.
4. **The monitor.** Run `tools/mac-display.command --local` (or open `http://localhost:5000/display`) on the external monitor, fullscreen. No iPad, certificate or HTTPS is needed any more.
5. The Mac must **never sleep**: System Settings > Lock Screen > display off **Never**, power connected, lid open (or use the external display with the lid closed and keep the Mac awake).

### Set up the Windows laptop (the claiming station)

1. Install the Windows build as in section 5.
2. **Connect to the Mac's share**: File Explorer > address bar `\\<Mac address>\<share name>`, sign in as the Mac user, tick **Remember my credentials**. Map it to a drive letter (right-click > Map network drive, **Reconnect at sign-in**) so it is still there after a restart.
3. Open `http://localhost:5000/diagnostics` (Settings) on the Windows laptop and set:
   - **Output folder**: the shared folder (e.g. `Z:\` or `\\192.168.8.101\Photobooth`). Press **Check**, then **Save**.
   - **Guest photos over your wifi**: **Let guests download their photos**, the **same wifi name and password**, and **Guests reach the booth at**: this laptop's address **on the router** (not another network).
   - **Serve the display to the network**: on. This is what opens the port phones use to download. (The iPad certificate is not used.)
   - **This computer is the claiming station only**: on. **Restart the app** afterwards.
4. Allow `Photobooth.Server` on **Private networks** when Windows Firewall asks.
5. Open `http://localhost:5000/pickup`. After a guest is accepted on the Mac, their number appears here within a few seconds.

### Check it

- [ ] A test guest on the Mac gets a number; the monitor shows it large with no QR; `strip.jpg` has *No. N* at the foot.
- [ ] That number appears on the Windows laptop's **Claiming Station** within about 3 seconds.
- [ ] Type the number: only that guest remains. Hover their strip: **Show QR** appears. Click: a large QR.
- [ ] A phone on the booth wifi scans it and opens a page with **4 photos, the strip and the video**, and **Download all** works.
- [ ] The next guest gets the next number; restart the Mac's booth app, and the one after that carries on (it does not repeat a number).

### Things to know

- **Numbers restart each day.** The claiming station has a **Day** picker (it defaults to today). To start again from 1 in the middle of a day, delete the test sessions' folders from the Output folder and restart the Mac's booth app.
- **The Mac must be on** for the Windows laptop to see new guests. Download pages are served by the Windows laptop, from the shared folder, so a guest can only download while **both** are on and on the wifi.
- **4 photos and the frame.** The shipped `classic-2x6` frame has three windows. Setting 4 photos per strip removes that frame art and gives a plain 4-photo strip with the number at the foot. A branded 4-window frame needs new art made for it (Templates).
- **No video on the download page?** The Mac needs ffmpeg (`brew install ffmpeg`).
- **The video is an MP4, not a GIF**, which is what Instagram and Facebook accept.

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

## 5. The booth computer: a Windows laptop or a MacBook

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

### Or: a MacBook as the booth computer

The booth runs on a Mac as well as on Windows, and everything else in this guide
is the same. The differences:

**Canon's app.** Install **EOS Utility 3 for macOS** from Canon's support site
(search *EOS Utility 3* for your camera, take the macOS `.dmg.zip`). The R50 needs
3.16.10 or later, and 3.19.0 or later runs natively on Apple Silicon. Canon's
latest build lists macOS 26, 15 and 14 as supported; **check the download page
for your macOS version, and run the ten-press test in section 4 on the actual Mac
before the event.** Then:

- In EOS Utility's **Preferences → Destination Folder**, choose the folder the booth watches.
- If macOS opens **Photos** or **Image Capture** when the camera is plugged in, close it. Either can claim the camera and leave EOS Utility unable to connect.

**Install the booth.** Install ffmpeg once, for the guest video, with
`brew install ffmpeg`. The booth finds it in `/opt/homebrew/bin` or `/usr/local/bin` and uses the
Mac's own H.264 encoder.

Then either download **`photobooth-<version>-macos-arm64.zip`** from the releases
page, or build it on the Mac (needs the .NET 10 SDK and Node 22):

```bash
cd src/Photobooth.Web && npm ci && npm run build && cd ../..
dotnet run --project src/Photobooth.Server
```

A downloaded ZIP is unsigned, so macOS blocks it the first time. Run
`xattr -dr com.apple.quarantine <the folder>` once, then `./Photobooth.Server`.
Building it yourself avoids this.

When macOS asks whether `Photobooth.Server` may **accept incoming network
connections**, choose **Allow**. Then open <http://localhost:5000/operator>.

**Settings differences.**
- Folders are Mac paths such as `/Users/you/Pictures/Tethered`; `~/Pictures/Tethered` works too.
- The iPad reaches the booth at `https://<Mac name>.local:5002/display`; a Mac advertises that name itself. Spaces in the Mac's name become hyphens (`Juans-MacBook-Pro.local`).
- The booth's certificate key is stored in `data/` with owner-only permissions rather than Windows encryption, so **turn on FileVault** (System Settings → Privacy & Security).

**Power.** System Settings → Lock Screen: display off **Never** on power, and keep it plugged in. Closing the lid sleeps a Mac, so keep it open.

**The claiming screen on the same Mac.** The page is just the browser: open
<http://localhost:5000/claim> on the booth Mac for a claiming screen with no
certificate step. A second screen still follows the MacBook steps in section 7.

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
- Once the wifi name is saved, a third code appears under the wifi fields: that is the join-wifi QR for the queue sign (section 8).

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

Print the join-wifi QR so guests are on the network **before** their turn. The guest screens no longer show it, so this sign is the only way guests get it. Print or screenshot the code on **Settings → Guest photos over your wifi** (or make one from `WIFI:S:<name>;T:WPA;P:<password>;;` with any QR generator). Put the wifi name and password in text beside it, and:

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
| No video on the guest page | `ffmpeg` folder missing, or a Windows "N" edition; on a Mac, ffmpeg not installed | Keep `ffmpeg\` beside the exe, or on a Mac run `brew install ffmpeg`. Sessions still work without the video |
| Strip preview never appears on the iPad | The iPad is on an older page | Reload it from the Home Screen icon. The shots show meanwhile |

The **Diagnostics** page (`/diagnostics`) shows what the booth saw and why it rejected anything, and can export a bundle that contains no photographs.

---

## 12. Afterwards

- Everything is in the output folder, one folder per guest: `strip.jpg`, `strip.mp4`, `photo-N.jpg`, `thumb-N.jpg`, and a record. Back it up before clearing anything.
- Guests' links stop working once they leave your wifi. That is by design; nothing was uploaded anywhere.
- The guest wifi password is kept in plain text in `data\settings.json`. It is only the guests' wifi, but do not share the folder carelessly.
