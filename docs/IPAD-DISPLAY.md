# Setting up the iPad as the guest display

The iPad replaces the external monitor: it shows the countdown, the shots as
they are taken, the review, and the finished strip with the QR code for
downloading it.

**Twenty minutes the first time**, most of it on the iPad. After that, starting a
booth is opening one page.

---

## Before you start

| | |
|---|---|
| **A network you control** | Your own travel router, or the laptop's hotspot. **Not a venue's wifi** — see [If the iPad cannot reach the booth](#if-the-ipad-cannot-reach-the-booth) |
| **iPadOS 16.4 or newer** | Older versions have no wake lock, so the iPad will sleep mid-event |
| **A stand or mount** | Anywhere the guests can read it. It no longer has to sit near the lens, since nothing on it is a camera any more |

Both devices must be on the **same network**. The booth does not need internet —
only Google Drive uploads do.

---

## Why any of this is necessary

The display has to **stay awake**. An iPad left to itself dims and then sleeps
mid-event, which reads as a broken booth rather than a sleeping one — and the
Wake Lock API that prevents it is only handed to a **secure context**: HTTPS,
or localhost. Over plain `http://192.168.1.50:5000/display` there is no wake
lock, and no setting changes that.

No public certificate authority will issue a certificate for a laptop on your
wifi. So **the booth runs its own authority**, and the iPad is told once to trust
it. That is the whole of what the steps below do.

> **This used to be about the posing mirror**, which needed the camera and so
> needed the same secure context. The mirror has gone — the iPad framed the
> shot differently from the R50 standing beside it, so guests posed to a
> picture that was not the one being taken. The wake lock still wants HTTPS,
> which is why the certificate stays.

---

## 1. Switch it on

On the booth laptop, open **Settings → Guest display on an iPad** and press
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

Back on **Settings → Guest display on an iPad**, you will see **two QR codes**.

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
- the attract screen — **Step in and smile**, and the empty photo slots

If you get that, everything is working. Arm a session from the console and the
iPad should count down with it.

### Make it behave like a booth screen

| | |
|---|---|
| **Add to Home Screen** | Share → *Add to Home Screen*, then launch from that icon. Runs fullscreen with no Safari chrome |
| **Guided Access** | Settings → Accessibility → Guided Access. Triple-click the side button to lock the iPad into the display so a guest cannot exit it |
| **Auto-Lock** | Settings → Display & Brightness → Auto-Lock → **Never**, as a belt-and-braces backstop. The app already holds a wake lock |
| **Orientation lock** | So nobody turns the display sideways mid-session |

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
shown under **This machine** on the Settings page — the certificate covers both.

**Safari says the connection is not private.** The certificate is installed but
not trusted. Go back to *Certificate Trust Settings* and switch the booth on.

**The page loads but sits on "Connecting..." for ever.** Getting this far means
the address, the certificate and the network are all correct -- the booth served
the page. Only the live connection is missing.

On **v0.12.1 and older this was a bug in the booth**, not anything you did: the
network port allowed the live-updates channel but not the handshake that opens
it, so the display could never connect and nothing reported an error. Fixed in
**v0.12.2**; upgrade rather than hunting for a cause.

**"All done" appears over an empty white box.** The strip could not be
fetched. On **v0.12.3 and older this was a bug in the booth**: the network
port served the QR but not the strip beside it, and a broken image is all a
browser can show for that. The operator console was unaffected, so the booth
's own screen looked fine. Fixed in **v0.13.0**.

**The iPad dims or sleeps during an event.** The wake lock needs the HTTPS
address and iPadOS 16.4 or newer. Check the padlock is there, then set
**Settings → Display & Brightness → Auto-Lock → Never** as well.

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
| New laptop | New authority, so every iPad repeats step 2. The fingerprint on the Settings page will have changed |
| `data/` deleted | Same as a new laptop |
