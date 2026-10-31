# Full setup guide: Mac booth, iPad preview, Windows claiming station

For build **v0.20.0** or newer. Follow it top to bottom the first time, ideally at
home the week before. On the day, only **Part 6** (the opening check) and **Part 7**
(the run-sheet) are needed.

A printable copy is [Photobooth-Setup-Guide.pdf](Photobooth-Setup-Guide.pdf). After
editing this page, rebuild it with `python tools/make_setup_guide.py`.

**Allow about two and a half hours the first time.** Most of it is the camera and
the router. Nothing here needs the internet, and the booth network deliberately
has none.

---

## Contents

- **Part 1.** What you are building
- **Part 2.** Before you start: kit, and the details to write down
- **Part 3.** The router
- **Part 4.** The MacBook: camera, booth app, settings, sharing
- **Part 5.** The iPad and the Windows laptop
- **Part 6.** Rehearsal, and the opening check on the day
- **Part 7.** Running the event
- **Part 8.** When something goes wrong
- **Part 9.** Afterwards

---

# Part 1. What you are building

```
 Canon R50 --USB--> MACBOOK ---cable/wifi---> ROUTER (WAN port empty) <--wifi-- guests' phones
 + BR-E1 remote     the booth: EOS Utility,          ^          ^
                    booth app, every photo,          |          |
                    every setting                    |          |
                                                     |          |
                    iPad (wifi) ---------------------'          |
                    guest preview                               |
                                                                |
                    WINDOWS LAPTOP (cable/wifi) ----------------'
                    claiming station, reads the Mac's shared folder
                    [laptop screen: operator]  [external monitor: guest QR]
```

| Device | Its job | What runs on it |
|---|---|---|
| **MacBook** | The main computer. Receives every photo, builds each strip, stores everything, holds **every setting**. Staff press **Accept** here | Canon EOS Utility, the booth app |
| **Canon R50 + BR-E1** | Takes the photos. The app cannot fire the shutter; staff press the remote | |
| **iPad** | The guest preview. A countdown, each photo large as it lands, then the strip, then the guest's number. **View only**, no mirror | Safari |
| **Windows laptop** | The claiming station. Reads the Mac's shared sessions folder and serves guests their downloads | The booth app, in claiming-station mode |
| **External monitor** on the Windows laptop | Faces the guest. Shows one guest's strip and QR at a time | A browser window |
| **Router** | The only network. **No internet**, on purpose | |

If the iPad or the Windows laptop dies, **no photos are lost**: everything is on
the Mac.

### How a guest moves through it

1. **Queue.** A printed sign shows the join-wifi QR. Guests join the booth wifi while they wait.
2. **Centre stage.** They frame using the camera's viewfinder. The iPad counts down 3-2-1, and staff press the **BR-E1** for each shot. Each photo fills the iPad for 2 seconds, with the next count in the corner.
3. **Preview.** After the last shot the iPad shows their **strip**. Staff at the Mac press **Accept**, or **Retake this one** on a shot they want redone.
4. **Number.** The iPad shows a large **number** (1, 2, 3, restarting each day). It is printed on the strip too.
5. **Claim.** At the Windows laptop the guest says their number. The operator types it and clicks their strip. The **external monitor** shows that strip and a QR. The guest scans it and downloads their photos, the strip and a video.

---

# Part 2. Before you start

## 2.1 Kit list

- [ ] **MacBook** (Apple Silicon) and charger
- [ ] **Canon EOS R50**, a **DR-E18 / LP-E17 dummy battery** with its adapter (the R50 will not run on USB power alone), a short good **USB-C cable**, and the **BR-E1** remote with a spare CR2032
- [ ] **iPad** (iPadOS 16.4 or newer), a **stand**, charger and a long cable
- [ ] **Windows laptop** (not an "N" edition of Windows: it lacks the video encoder) and charger
- [ ] **External monitor** for the Windows laptop, its power and an HDMI/USB-C cable
- [ ] **Travel router** and its power. Two **LAN cables** if the router has the ports (one for the Mac, one for Windows)
- [ ] USB-C **Ethernet adapters** for the Mac and Windows laptop, if they have no port
- [ ] **Power strip** and extension lead
- [ ] The **queue sign** (Part 5.4), printed
- [ ] A **USB stick** or external drive for the backup afterwards

## 2.2 Write these down

Fill this in as you go. Every later step refers back to it.

| Detail | Example | Yours |
|---|---|---|
| Booth wifi name | `PhotoBooth` | |
| Booth wifi password | `smile2026` | |
| Router admin address | `192.168.8.1` | |
| Mac address on the router | `192.168.8.101` | |
| Mac's network name | `Juans-MacBook-Neo.local` | |
| Windows laptop address on the router | `192.168.8.100` | |
| Mac user name and password (for the share) | `juan` | |
| Camera watch folder on the Mac | `/Users/juan/Photobooth/Camera` | |
| Sessions (output) folder on the Mac | `/Users/juan/Photobooth/Sessions` | |
| Share name | `Sessions` | |

The Mac's network name is in **System Settings > General > Sharing**, at the bottom
(*Local hostname*).

---

# Part 3. The router (about 20 minutes)

Do this first; everything else needs it.

1. Power the router on. Join its default wifi (or plug in by cable) and open its admin page (the address is on its label, often `192.168.8.1`).
2. Make sure the **WAN / Internet port is empty**. That is what keeps the network offline.
3. Set:

   | Setting | Value | Why |
   |---|---|---|
   | Mode | **Router** (not repeater, bridge or access point) | It must hand out addresses itself |
   | Wifi band | **5 GHz** for the booth network (2.4 GHz on as well is fine) | Faster and less crowded |
   | Security | **WPA2-Personal** (not WPA3 or WPA2/WPA3 mixed) | The most phones can join |
   | Wifi name and password | Your choice. Simple, no unusual symbols. **Write them in 2.2** | Typed into the Mac later |
   | Guest network | **Off** | On a router, "guest" isolates devices from each other, which blocks the booth |
   | AP / client isolation | **Off** | Same reason |

4. Join the Mac and the Windows laptop to it (cable is best). In the router's **DHCP / address reservation** page, give each a **fixed address** and write them in 2.2. Reserve the iPad too if you like.
5. Save, and let the router restart if it asks.

**Check:** on the Mac, System Settings > Wi-Fi (or Network) shows an address like `192.168.8.x`. It will say *No internet connection*; that is correct.

---

# Part 4. The MacBook (about an hour)

## 4.1 Make two folders

In Finder, make a folder for the booth with two folders inside it, for example:

- `~/Photobooth/Camera`, where the camera's photos land (the **watch folder**)
- `~/Photobooth/Sessions`, where each guest's finished folder is saved (the **output folder**)

They **must be separate**, and Sessions must **not** be inside Camera. Write both in 2.2.

## 4.2 The camera

1. Fit the dummy battery and connect its adapter to mains.
2. On the R50, set:
   - **Settings > USB connection app: Photo Import/Remote Control**
   - Shooting mode **M** (manual), a fixed **ISO**, **manual white balance** (not AWB)
   - Image quality **JPEG only** (not RAW)
   - **Auto power off: Disable**, and the screen's power saving as long as it allows
3. Pair the remote: **Wireless features > Connect to Wireless Remote**, then hold the BR-E1's **W** and **T** buttons together until it pairs.
4. Connect the camera to the Mac by USB-C and switch it on.

## 4.3 Canon EOS Utility

1. Download **EOS Utility 3 for macOS** from Canon's support site (search *EOS R50 EOS Utility*, take the macOS download; you may need the camera's serial number). Version **3.19.0 or later** runs natively on Apple Silicon. Check that the download page lists your macOS version.
2. Install it and open it. If **Photos** or **Image Capture** opens when the camera is plugged in, quit them: either can take the camera and leave EOS Utility unable to connect.
3. Choose **Remote Shooting**.
4. In EOS Utility's **Preferences > Destination Folder**, choose your **watch folder** (`~/Photobooth/Camera`). EOS Utility puts each day's shots in a dated subfolder such as `2026_10_31`; the booth reads subfolders, so that is fine.
5. **Test it:** press the BR-E1. A JPEG should appear under the watch folder within a few seconds. Then press it **ten times over five minutes**. Every press must land a file.

> If the remote stops working while tethered (Canon advises Bluetooth off while on USB), press the
> camera's own shutter button instead. Nothing else changes, but someone has to stand at the camera.

## 4.4 Install the booth app

1. Install **Homebrew** if the Mac does not have it (instructions at brew.sh), then install ffmpeg, which makes the guest video:

   ```bash
   brew install ffmpeg
   ```

2. Download **`photobooth-v0.20.0-macos-arm64.zip`** from the releases page:
   <https://github.com/blueboxdev01/photobooth-app-ipad/releases/download/v0.20.0/photobooth-v0.20.0-macos-arm64.zip>
3. Unzip it to a folder you will keep, such as `~/Photobooth/App` (not Downloads).
4. The build is unsigned, so macOS blocks it the first time. In Terminal, run this once (change the path if you unzipped elsewhere):

   ```bash
   xattr -dr com.apple.quarantine ~/Photobooth/App
   ```

5. Start it:

   ```bash
   cd ~/Photobooth/App && ./Photobooth.Server
   ```

   Leave that Terminal window open all event; closing it stops the booth.
6. When macOS asks whether **Photobooth.Server** may *accept incoming network connections*, choose **Allow**.
7. Open **<http://localhost:5000/operator>** in Safari or Chrome. You should see the operator console.

**Upgrading from an older booth folder?** Copy its `data` and `templates` folders into the new one before starting it.

## 4.5 Booth settings

In the operator console, open **Settings** (bottom of the left rail). Save each section as you go.

**Folders**

1. **Watch folder:** paste your watch folder path (e.g. `/Users/juan/Photobooth/Camera`; `~/Photobooth/Camera` also works). Press **Check**; it must say it is usable. Press **Save watch folder**.
2. **Output folder:** paste your output folder (e.g. `/Users/juan/Photobooth/Sessions`). **Check**, then **Save output folder**.
3. Back on the operator console, the camera status should read **Ready**.

**Strip layout**

4. Pick the **output size** (for example *Photo strip 2×6*) and **photos per strip**.
   The shipped `classic-2x6` frame has **three** windows. For four photos you need new frame art made in **Templates**, or the strip is plain.
5. Open **Templates** and check the photo slots sit where you want them.

**Guest display** (the backdrop)

6. Optionally pick a backdrop colour or image to match the event.

**Timings**

7. Countdown: **3 s** is the default.
8. No-photo timeout: **20 s** by default. It must be longer than the real gap between the countdown ending and the remote being pressed.

**Looping video**

9. Leave it **on**. It costs about a second per guest; switch it off only if a queue is building.

**Guest photos over your wifi**

10. Press **Let guests download their photos**.
11. **Wifi network** and **Wifi password:** exactly as set on the router, **capitals included**. Press **Save wifi details**.
    The Windows laptop takes these from the Mac, so **this is the only place you type them**. A join-wifi QR appears here; you will print it in Part 5.4.
12. Press **Hand over by number**. Each guest now gets a number on the iPad and on their strip.
13. Leave **Use a separate claiming station** and **This computer is the claiming station only** both **off** on the Mac.

**Guest display on an iPad**

14. Press **Serve the display to an iPad**.
15. **Restart the booth:** in its Terminal window press **Ctrl+C**, then run `./Photobooth.Server` again. The network port opens only at start-up.
16. Back in Settings, this section now shows a **QR** and an address like `http://Juans-MacBook-Neo.local:5001/display`. You will scan it with the iPad in Part 5.1.

## 4.6 Share the sessions folder with the Windows laptop

1. **System Settings > General > Sharing**, switch **File Sharing** on.
2. Click the **(i)** beside File Sharing.
3. Under **Shared Folders**, press **+** and add your **output folder** (`~/Photobooth/Sessions`).
4. Under **Users** for that folder, set your Mac user to **Read & Write**, and **Everyone** to **No Access**.
5. Press **Options…** and tick **Share files and folders using SMB**. Under **Windows File Sharing**, tick your **user account** and enter your Mac password when asked. (Without this, Windows cannot sign in.)
6. Press **Done**. Write the share name (usually the folder name, `Sessions`) in 2.2.

## 4.7 Keep the Mac awake

1. **System Settings > Lock Screen:** *Turn display off on power adapter when inactive*: **Never**. *Require password after screen saver begins or display is turned off*: **Never** (for the event).
2. **System Settings > Battery > Options:** *Prevent automatic sleeping on power adapter when the display is off*: **on**.
3. Keep it **plugged in** with the **lid open**.
4. Turn on **FileVault** (System Settings > Privacy & Security) if it is not already: guests' photos live on this Mac.

---

# Part 5. The iPad and the Windows laptop (about 40 minutes)

## 5.1 The iPad (the guest preview)

Nothing to install and no certificate.

1. **Settings > Wi-Fi:** join the booth wifi. It will say *No Internet Connection*; stay connected.
2. Open the **Camera** app and scan the QR under **Settings > Guest display on an iPad** on the Mac. Tap the link. Or type the address into Safari: `http://<Mac's network name>:5001/display` (from 2.2), or `http://<Mac address>:5001/display`.
3. It should read **Step in and smile**.
4. Tap **Share > Add to Home Screen > Add**. From now on, open the booth from that icon: it opens fullscreen without Safari's bars.
5. **Settings > Display & Brightness > Auto-Lock: Never.** Over plain http Safari cannot keep the screen on by itself, so this is required.
6. Optional but recommended: **Settings > Accessibility > Guided Access** on, set a passcode. Open the booth icon and triple-click the top (or Home) button to lock the iPad to it.
7. Plug it in and put it on its stand **beside the camera**, facing where guests stand.

## 5.2 The Windows laptop: install the app

1. Join the booth wifi or plug into the router. When Windows asks, choose **Private** network (or later: Settings > Network & internet > the connection > **Private network**).
2. Download **`photobooth-v0.20.0-win-x64.zip`**:
   <https://github.com/blueboxdev01/photobooth-app-ipad/releases/download/v0.20.0/photobooth-v0.20.0-win-x64.zip>
3. Right-click the ZIP > **Properties** > tick **Unblock** if shown > **OK**. Then **Extract All** to `C:\Photobooth` (not Downloads). Keep the `ffmpeg` folder beside the exe.
4. Run **`Photobooth.Server.exe`**. If SmartScreen warns, choose **More info > Run anyway**. Leave its window open all event.
5. When **Windows Firewall** asks, allow it on **Private networks**.
6. Windows power settings: **Settings > System > Power**: screen and sleep **Never** when plugged in. Turn **Mobile hotspot** off.

## 5.3 The Windows laptop: connect it to the Mac

**Map the Mac's share**

1. Open **File Explorer**. In the address bar type `\\<Mac address>\<share name>`, for example `\\192.168.8.101\Sessions`, and press Enter.
2. Sign in with the **Mac's** user name and password. Tick **Remember my credentials**.
3. Right-click **This PC > Map network drive**. Drive **Z:**, folder `\\192.168.8.101\Sessions`, tick **Reconnect at sign-in**, then **Finish**.
4. You should see the Mac's sessions folder (it holds `booth-settings.json` once the Mac's booth app has run).

**Station settings**

Open **<http://localhost:5000/diagnostics>** (Settings) on the Windows laptop.

5. **Output folder:** `Z:\` (or `\\192.168.8.101\Sessions`). Press **Check**, then **Save output folder**.
6. **Guest photos over your wifi:** press **Let guests download their photos**. Leave the wifi name and password empty; they come from the Mac.
7. **Guests reach the booth at:** pick this laptop's **router address** (from 2.2), for example `Ethernet — 192.168.8.100`. Do not leave it on *Choose automatically* if the laptop is on more than one network.
8. Press **This computer is the claiming station only**.
9. **Guest display on an iPad:** press **Serve the display to an iPad**. (No iPad uses it here. This opens the port guests' phones download from.)
10. **Restart the app:** close the `Photobooth.Server.exe` window and run it again.
11. Reopen Settings. Under **Guest photos over your wifi** it should say **Wifi details come from the booth computer: \<your wifi name\>**. If it says they have not been published, check step 5 points at the Mac's shared folder and that the Mac runs v0.20.0.

**Two screens**

12. Connect the external monitor. Press **Win+P** and choose **Extend** (not Duplicate).
13. On the **laptop screen**, open **<http://localhost:5000/pickup>**. This is the operator's screen: every guest of the day.
14. Open a **second browser window** at **<http://localhost:5000/pickup/screen>**, drag it onto the **external monitor**, and press **F11** for fullscreen. It reads **Collect your photos here**.
15. Turn the monitor to face the guests. Keep the laptop screen facing the operator only: it shows every guest's strip.

## 5.4 The queue sign

Guests need to be on the booth wifi **before** they reach the claiming station, and the sign is the only place the join code appears.

1. On the Mac, **Settings > Guest photos over your wifi**: screenshot or print the **join-wifi QR**.
2. Put it on a sign with the wifi name and password in text beside it, and this line:

   > **Your phone will say "no internet". That's normal. Stay connected.**

   Without it, some Android phones quietly switch back to mobile data, and the download page stops loading.
3. Place it where the queue forms.

---

# Part 6. Rehearsal, and the opening check

## 6.1 Full rehearsal (once, before the first event; about 20 minutes)

Do a complete guest, start to finish, with a real phone that is **not** on any other network.

- [ ] **Mac:** operator console shows the camera **Ready**.
- [ ] **iPad:** shows *Step in and smile*, opened from the Home Screen icon.
- [ ] **Mac:** press **Start Session**. The iPad counts down.
- [ ] Press the **BR-E1** for each shot. Each photo fills the iPad for about 2 seconds, then the next countdown runs.
- [ ] After the last shot the iPad shows the **strip**. On the Mac, try **Retake this one** on one shot: the iPad updates.
- [ ] **Mac:** press **Accept**. The iPad shows *Making your strip…*, then a **number**.
- [ ] **Windows:** within about 3 seconds that number appears on `/pickup`.
- [ ] Type the number. Only that guest remains. Click their strip: the **external monitor** shows the strip and a large QR, and the tile says *On the guest screen*.
- [ ] **Phone:** join the booth wifi with the queue-sign QR, then scan the monitor's QR. The page shows the photos, the strip and the video.
- [ ] On the phone, save a photo (iPhone: tap, long-press, **Add to Photos**; Android: **Download image**) and the video (**Save Video**). Try **Download all**.
- [ ] **Windows:** press **Esc**. The monitor goes back to *Collect your photos here*.
- [ ] **Mac:** press **Start Session** for a second guest. They get the next number.
- [ ] Restart the Mac's booth app and do a third guest: the number carries on rather than starting again.
- [ ] Change the wifi password in the Mac's Settings, then back. Within a few seconds the Windows Settings page shows the change.

Then delete the rehearsal guests' folders from the sessions folder if you want the event to start at number 1 (restart the Mac's booth app afterwards).

## 6.2 The opening check, on the day (about 10 minutes)

- [ ] Router on, **WAN port empty**. Mac and Windows laptop connected to it, **plugged into mains**.
- [ ] Camera on, dummy battery on mains, USB to the Mac. **EOS Utility** open in **Remote Shooting**.
- [ ] Mac: booth app running in Terminal. Operator console shows camera **Ready**.
- [ ] One test press of the BR-E1 lands a photo (Diagnostics shows it).
- [ ] iPad on its stand and charger, showing *Step in and smile*.
- [ ] Windows: booth app running. `/pickup` on the laptop screen, `/pickup/screen` fullscreen on the monitor.
- [ ] One full test guest (6.1, the short way): shots, Accept, number, QR on the monitor, a phone downloads.
- [ ] Queue sign up.
- [ ] Free disk space on the Mac comfortably above what the event needs (about **30 MB per guest**; the console warns below 2 GB).

---

# Part 7. Running the event

## 7.1 At the Mac (the booth operator)

| Step | Staff action | The guest sees on the iPad |
|---|---|---|
| 1 | Press **Start Session** | Countdown 3-2-1 |
| 2 | Press the **BR-E1** at "1". The countdown is a cue; the app cannot fire the shutter | The photo, large, for 2 seconds, with the next count in the corner |
| 3 | Repeat for each shot | *Photo 2 of 4*, and so on |
| 4 | After the last shot, wait about a second | **Their strip** |
| 5 | To redo a shot: **Retake this one** on it. Drag shots to reorder; **Back to capture order** undoes that | The strip updates |
| 6 | Press **Accept** | *Making your strip…*, then **their number**. Point them to the claiming station |
| 7 | Press **Start Session** for the next guest whenever ready | The countdown starts again |

- **No photo arrived?** After 20 s the console says so. Press **Wait** to carry on, or **Abort** to drop the session.
- **Retake** in the console's rail redoes the most recent shot; **Retake this one** on a thumbnail redoes that one.
- Only the Mac can Accept, Retake or Abort. Nothing on the iPad or the wifi can change a session.

## 7.2 At the Windows laptop (the claiming operator)

1. Ask the guest's **number** and type it in **Guest number**. Press **Enter** to show them straight away when only one guest matches, or click their strip.
2. The **monitor** shows their strip and QR. Ask them to check it is their strip, then scan it with their phone camera.
3. If the page does not open, their phone is not on the booth wifi: point them to the queue sign, or read the wifi name on the monitor.
4. When they have downloaded, press **Esc** (or **Clear the screen**). It clears itself after 3 minutes anyway.
5. A guest from an earlier day? Change the **Day** picker; numbers restart each day.

---

# Part 8. When something goes wrong

| What you see | Most likely cause | Fix |
|---|---|---|
| Camera status not **Ready**; no photos arrive | Watch folder does not match EOS Utility's destination, or EOS Utility closed | Mac Settings > Folders > **Check**. EOS Utility open in Remote Shooting, destination = watch folder |
| Remote does nothing | BR-E1 lost pairing, or its battery | Press the camera's shutter; re-pair later |
| EOS Utility cannot find the camera | Photos or Image Capture took it | Quit both, unplug and replug the camera |
| iPad cannot load the page | iPad on another network; Mac booth not restarted after turning on the iPad display; macOS blocked incoming connections | Same wifi? Restart the booth app. System Settings > Network > Firewall > allow Photobooth.Server |
| iPad's `.local` address will not load | Name typed wrong, or the router blocks Bonjour names | Use the Mac's address instead: `http://192.168.8.101:5001/display` |
| iPad screen went dark | Auto-Lock | Settings > Display & Brightness > Auto-Lock: Never |
| iPad shows an old screen or *Connecting…* | Page lost its connection | Close and reopen it from the Home Screen icon |
| Number never appears on Windows | Share disconnected, or Output folder not the share | Open `Z:\` in File Explorer (sign in again if asked). Windows Settings > Output folder > **Check** |
| Windows Settings: wifi details not published | Output folder is not the Mac's share, or the Mac runs an older build | Point it at the share; update the Mac to v0.20.0 and restart it |
| Monitor stays on *Collect your photos here* after clicking | The monitor page is in a different browser or was closed | Reopen `http://localhost:5000/pickup/screen` on the monitor, fullscreen |
| Join-wifi QR scans and does nothing | Wifi name or password in the Mac's Settings is wrong | Match the router exactly, capitals included, then reprint the sign |
| Joined, but the photos page never loads | Windows advertising the wrong address, or router isolating devices | Windows Settings > *Guests reach the booth at* (router address). Router: guest mode and client isolation off |
| Page loaded once then stopped (Android) | Phone switched back to mobile data | Rejoin the wifi and choose to stay connected |
| No video on the guest page | ffmpeg missing on the Mac | `brew install ffmpeg`, restart the booth app. Guests still get photos and strip |
| Strip has no frame art | Photos per strip does not match the frame's windows | Set photos per strip to the frame's count, or make new art in Templates |

**Diagnostics** (`http://localhost:5000/diagnostics`, on either computer) shows what the booth saw, what it rejected and why, and can export a bundle with no photographs in it.

### If a guest has already gone

Every guest has a folder in the sessions folder. Their link only works while they are on the booth wifi, so for anyone who left, send `strip.jpg`, the photos or the video from their folder afterwards. The folder's `session.json` records their number.

---

# Part 9. Afterwards

1. Stop the booth apps (Ctrl+C on the Mac, close the window on Windows).
2. **Back up** the Mac's sessions folder to the USB stick or drive before clearing anything. Each guest's folder holds `strip.jpg`, `strip.mp4`, `photo-N.jpg`, thumbnails and `session.json`.
3. On the Windows laptop, the share can stay mapped for the next event.
4. Guests' links stop working once they leave the booth wifi. That is by design; nothing was uploaded anywhere.
5. The guest wifi password is stored in plain text in the Mac's `data/settings.json` and in `booth-settings.json` in the sessions folder. It is only the guests' wifi, but do not share those folders beyond the station.
6. To start the next event at number 1, numbers restart on their own the next day. To restart mid-day, move that day's test folders out of the sessions folder and restart the Mac's booth app.
