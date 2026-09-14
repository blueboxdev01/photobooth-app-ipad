# Setting up the iPad as the guest display

The iPad replaces both the external monitor **and** the USB webcam: it shows the
countdown, the shots and the QR, and its front camera is the posing mirror.

**Twenty minutes the first time**, most of it on the iPad. After that, starting a
booth is opening one page.

---

## Before you start

| | |
|---|---|
| **A network you control** | Your own travel router, or the laptop's hotspot. **Not a venue's wifi** — see [If the iPad cannot reach the booth](#if-the-ipad-cannot-reach-the-booth) |
| **iPadOS 16.4 or newer** | Older versions have no wake lock, so the iPad will sleep mid-event |
| **A stand or mount** | The iPad's front camera is now the mirror, so it has to sit where the webcam did: facing the guests, near the R50 |

Both devices must be on the **same network**. The booth does not need internet —
only Google Drive uploads do.

---

## Why any of this is necessary

A browser will not give a web page the camera unless the page is a **secure
context** — HTTPS, or localhost. The posing mirror is a camera, so an iPad
opening `http://192.168.1.50:5000/display` gets no mirror at all, and no setting
changes that.

No public certificate authority will issue a certificate for a laptop on your
wifi. So **the booth runs its own authority**, and the iPad is told once to trust
it. That is the whole of what the steps below do.

---

## 1. Switch it on

On the booth laptop, open **Setup → Guest display on an iPad** and press
**Serve the display to an iPad**.

Then **restart the booth**. The app opens its network ports once, when it starts,
so the switch does nothing until it does. The page tells you this in a banner.

Two things happen on that first start:

- The booth creates its **authority** — a root certificate in `data/`, good for
  ten years. This is the thing the iPad trusts, and it stays put.
- It issues itself a **server certificate**, which is thrown away and reissued
  every time the app starts. That is deliberate: it is what lets you take the
  booth to a venue on a different network, get a different IP, and have
  everything keep working with nothing to redo on the iPad.

---

## 2. Trust the booth on the iPad

Back on **Setup → Guest display on an iPad**, you will see **two QR codes**.

### Scan the first one

It opens a small page served by the booth. Tap **Install the booth certificate**
and allow the download when Safari asks.

### Install the profile

**Settings** → a **Profile Downloaded** item near the top → **Install**. Enter the
iPad's passcode, then **Install** again.

### Then trust it — this is the step people miss

**Settings → General → About → Certificate Trust Settings**, and switch the
booth **on**.

> **Do not skip this.** Installing the profile is not the same as trusting it.
> Without this switch the display will refuse to load, and the error Safari
> shows does not mention certificates at all.

You only ever do this once per iPad. Restarting the booth, changing venue or
changing IP does not undo it.

---

## 3. Open the display

Scan the **second** QR code. Safari opens the guest display and should show:

- a **padlock** in the address bar
- the **mirror**, with your own face in it

If you get the mirror, everything is working.

### Make it behave like a booth screen

| | |
|---|---|
| **Add to Home Screen** | Share → *Add to Home Screen*, then launch from that icon. Runs fullscreen with no Safari chrome |
| **Guided Access** | Settings → Accessibility → Guided Access. Triple-click the side button to lock the iPad into the display so a guest cannot exit it |
| **Auto-Lock** | Settings → Display & Brightness → Auto-Lock → **Never**, as a belt-and-braces backstop. The app already holds a wake lock |
| **Orientation lock** | So nobody turns the mirror sideways mid-session |

---

## What is and is not served to the network

Only the guest display. The operator console, the template editor, this setup
page and every control that can start or abort a session stay on the laptop and
are **refused** on the network ports — so a stranger on the same wifi cannot
reach them.

| Port | Serves |
|---|---|
| 5000 | Operator, templates, setup — **this machine only** |
| 5001 | The certificate page, and nothing else |
| 5002 | The guest display over HTTPS |

Change the ports under `GuestDisplay` in `appsettings.Local.json` if something
else on the machine wants them.

---

## Recalibrate the framing guide

The guide on the guest screen shows what survives onto the strip. It was never
calibrated against the R50 to begin with, and the iPad's lens is a different
field of view from the webcam's — so whatever you previously learned about it no
longer applies.

1. Stand so you exactly fill the corner brackets.
2. Take a shot.
3. Compare the strip against what the guide promised.
4. Write down how far out it is.

---

## If the iPad cannot reach the booth

Work down this list; the first item is by far the most likely.

**The network is separating its devices.** Most venue and guest wifi enables
client isolation, which blocks device-to-device traffic completely. No setting in
this app can defeat it. Use **your own travel router or the laptop's hotspot**,
and treat venue wifi as unusable.

**Windows Firewall is blocking the ports.** The first time the app opens them,
Windows asks — say yes, and make sure **Private networks** is ticked. If you
dismissed it, allow `Photobooth.Server` under Windows Defender Firewall.

**The `.local` name will not resolve.** The URLs use your machine's mDNS name,
which is what survives a change of address. If it does not resolve, use the IP
shown under **This machine** on the Setup page — the certificate covers both.

**Safari says the connection is not private.** The certificate is installed but
not trusted. Go back to *Certificate Trust Settings* and switch the booth on.

**The page loads but sits on "Connecting..." for ever.** Getting this far means
the address, the certificate and the network are all correct -- the booth served
the page. Only the live connection is missing.

On **v0.12.1 and older this was a bug in the booth**, not anything you did: the
network port allowed the live-updates channel but not the handshake that opens
it, so the display could never connect and nothing reported an error. Fixed in
**v0.12.2**; upgrade rather than hunting for a cause.

**The mirror is missing but the page loads.** Safari was denied the camera:
**Settings → Apps → Safari → Camera → Allow**, then reload. If the page is on
`http://` rather than `https://`, no permission will help — open the HTTPS URL.

**It shows the rear camera.** Reload the page. The app asks for the front camera
explicitly, so this means the request was made before the iPad was ready.

---

## If wifi drops mid-event

The guest screen freezes. **Nothing else is affected** — capture, review,
compositing, the local archive and Drive uploads all run on the laptop and carry
on.

The fallback costs nothing: plug in a monitor and open
<http://localhost:5000/display> on the laptop, exactly as before.

---

## When the booth changes

| | What to do |
|---|---|
| New IP, new venue, new router | **Nothing.** A new certificate is issued at startup and the iPad already trusts the authority that signed it |
| Booth restarted | Nothing |
| A second iPad | Steps 2 and 3 on that iPad |
| New laptop | New authority, so every iPad repeats step 2. The fingerprint on the Setup page will have changed |
| `data/` deleted | Same as a new laptop |
