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

## A. Windows laptop (do this first, tonight if you can)

**Install**

| | |
|---|---|
| **Canon EOS Utility** | The camera is tethered through it; the booth app never touches the camera |
| **The booth app** | Either of the two below |
| **ffmpeg** | Only if building from source. A release ZIP already carries `ffmpeg\ffmpeg.exe` |

Getting the app, in order of preference:

1. **From source (has this week's changes, never yet compiled).** Install the .NET 10
   SDK and Node 22, then:
   ```
   cd src\Photobooth.Web && npm ci && npm run build && cd ..\..
   dotnet test
   dotnet build
   ```
   **Run `dotnet test` first.** The thumbnail, strip-preview and claiming-station
   changes were written on a Mac with no .NET and have only been type-checked on the web
   side. If anything fails, use option 2. For video, put an ffmpeg build on the PATH.
2. **Last release, v0.17.0.** Download the ZIP from the repo's Releases page, unzip, run
   `Photobooth.Server.exe`. It has no strip preview and no claiming-station page; see
   *If the new build is not ready* below.

Not a Windows "N" edition: it lacks the H.264 encoder and the video will not be made.

**Windows settings**

- Plug the laptop into the router's **LAN** port by cable.
- Windows Firewall: when the app first opens its ports, allow it on **Private networks**.
  Set the router connection's network profile to **Private**.
- Power plan: never sleep on mains, screen off never.
- Do **not** turn on Mobile Hotspot (it shares the laptop's internet with guests).
- Optional: exclude the booth's `data\` and watch folders from Defender real-time scanning.

**Folders.** Two different ones: the **watch folder** (where EOS Utility saves) and
the **output folder** (where sessions are filed). Set both in Settings, press **Check**.

## B. Router

- Mode: **Router**. **WAN port empty.**
- A **5 GHz** SSID, **WPA2**, a simple password. Write both down.
- **Guest network off**, **AP / client isolation off.**
- **DHCP reservation** for the Windows laptop (note its IP, e.g. `192.168.8.100`).
- Optional but better: reserve addresses for the MacBook and iPad too.

## C. Booth app settings (on the Windows laptop, `http://localhost:5000/operator`)

1. **Settings → Guest photos over your wifi**: switch on, enter the **exact** SSID and
   password, and set **Guests reach the booth at** to the laptop's router address.
2. Same section: press **Use a separate claiming station**. This is what makes the iPad
   send guests to the MacBook instead of showing the photos code itself.
3. **Settings → Guest display**: switch on **Serve the display**, then **restart the booth**.
4. Confirm **Looping video** is on and `ffmpeg` is found (diagnostics page).
5. Pick the template and photo count you will actually use.

## D. The iPad (the booth screen)

Follow [IPAD-DISPLAY.md](IPAD-DISPLAY.md): trust the booth certificate, then open
`https://<laptop>.local:5002/display` (or the IP). Add to Home Screen, Guided Access, and
Auto-Lock to Never.

Stand it where the guest can see it **from centre stage**, next to the camera. During the
shots it shows only the countdown; after the last shot it shows the strip.

## E. MacBook Neo (the claiming station)

It has Wi-Fi only, so join the router's wifi. If a USB-C Ethernet adapter turns up, use it
instead and leave the radio to the guests.

1. **Join the router's wifi** (it will warn there is no internet; stay connected).
2. **Trust the booth, once.** In Safari or Chrome open `http://<laptop address>:5001/cert`,
   open the downloaded `photobooth-ca.crt`, then in **Keychain Access** double-click
   **Photobooth booth authority → Trust → When using this certificate: Always Trust.**
3. **Stop it sleeping.** **System Settings → Lock Screen**: turn display off **Never** while
   on power, and keep it plugged in. (It currently turns the display off after 3 minutes.)
   The launcher below also holds it awake while it runs.
4. **Start the station:** double-click `tools/mac-display.command` (first run asks for the
   laptop's address). It checks the network, then the certificate, then opens
   `https://<laptop>:5002/claim` fullscreen in Chrome. Quit with **Cmd+Q**.

It shows *"Your photos will appear here"* until the first guest has been accepted.

Put it where a guest walks **after** the iPad, out of the line of the camera, with room for
two people to stand and scan.

## F. The queue sign

Print the join-wifi QR (the booth shows one on the iPad and the MacBook; or make one from
`WIFI:S:<name>;T:WPA;P:<password>;;` with any QR generator) and put the wifi name and
password in text next to it. Add: *"Your phone will say there's no internet. That's normal
— stay connected."* Android phones are the ones that give up otherwise.

---

## G. The test run

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
