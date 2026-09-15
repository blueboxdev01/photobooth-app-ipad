# Testing guest photos, end to end

Three parts, in order of what they cost you:

- **A — at your desk**, no extra hardware. Proves the app picks and advertises
  the right address.
- **B — with the router**, one phone. Proves a guest can actually get their
  photos, and that they cannot get your internet.
- **C — breaking it on purpose.** Proves the warnings fire, which is the only
  way to know they will fire at an event.

Part C is the one people skip. It is also the one that would have caught every
problem this feature has had so far, so please do not skip it.

---

## Part A — at your desk

No router needed. About ten minutes.

### A1. The automated tests still pass

```bash
dotnet test
```

**Expect:** 284 passed, 0 failed. If anything fails, stop here — the rest of
this plan assumes the build is sound.

### A2. Setup shows every address, named by adapter

1. Start the booth and open **Setup**.
2. Turn on **Guest photos over your wifi** if it is off.
3. Look at **Guests reach the booth at**.

**Expect:** a dropdown listing *Choose automatically* plus one entry per network
this laptop is on, each showing the adapter and the address — for example
`Wi-Fi — 192.168.1.34`.

**Expect NOT to see:** anything starting `127.` or `169.254.`. Those cannot be
reached by a phone, and offering them would only invite a wrong choice.

### A3. The address you pick is the address in the code

1. Pick a specific address from the dropdown.
2. Look at the **Guests reach** line just below it.

**Expect:** the link updates to the address you chose, immediately. No restart.

3. Switch back to **Choose automatically**.

**Expect:** the link goes back to the booth's own guess.

### A4. The QR really carries it

1. With an address chosen, run a session with the mock camera through to the end.
2. On the guest screen, scan the **photos** code with your phone's camera — just
   read the URL it offers, do not open it yet.

**Expect:** the URL contains the address you chose in A3, and the port from the
**Guests reach** line.

This is the step that matters. Everything else is the app agreeing with itself;
this is the app agreeing with a phone.

---

## Part B — with the router

What you need: a travel router, an ethernet cable, one phone.

### B1. Build the network

1. Plug the router in. **Leave the WAN port empty.** Nothing in it at all.
2. Cable the booth laptop to one of the router's **LAN** ports.
3. In the router's admin page, confirm:
   - mode is **Router**, not repeater or bridge
   - **client isolation / AP isolation is OFF**
   - guests will be on the **main** wifi, not the router's "guest network"
4. Give the laptop a **DHCP reservation** so its address stops moving.
5. Join your phone to that wifi.

### B2. Prove there is no internet on it — the point of the exercise

On the **phone**, joined to the router's wifi:

1. Turn **mobile data off**. Without this you will test your phone's cellular
   connection and learn nothing.
2. Open a browser and go to `http://neverssl.com`.

**Expect:** it fails to load.

`neverssl.com` rather than a site you use daily, because a cached page or a
saved HTTPS redirect can make a dead network look alive.

**If a page loads:** something is feeding that router. Check the WAN port is
genuinely empty and that the router is not also joined to a wifi network as a
repeater.

### B3. Prove the laptop is not a back door — your actual worry

This is the test for *"the laptop is on our wifi and the guests' router at the
same time; can they get out through it?"*

1. Leave the laptop cabled to the booth router.
2. **Also** connect the laptop's wifi to your normal internet. Confirm the
   laptop itself has internet — load any page on the laptop.
3. On the phone, still on the router's wifi with mobile data off, retry
   `http://neverssl.com`.

**Expect:** still fails. The laptop has internet; the phone does not get it.

**If the phone now has internet:** something has turned on connection sharing.
Check Mobile Hotspot is off, then confirm:

```bash
powershell -NoProfile -Command "Get-NetIPInterface -AddressFamily IPv4 | Format-Table InterfaceAlias, Forwarding -AutoSize"
```

**Expect:** `Forwarding` is `Disabled` on every line.

### B4. Set the address, now that there are two

The laptop is now on two networks, which is exactly the case that used to break.

1. Open **Setup**.

**Expect:** a warning — *"This booth is on 2 networks and is guessing."*

2. Pick the **Ethernet** address, the one on the router's subnet (it will match
   the reservation you made in B1).

**Expect:** the warning disappears and **Guests reach** shows that address.

### B5. A guest gets their photos

Run a session with the mock camera through to the end, then from the phone:

| Step | Expect |
|---|---|
| Scan the **join** code | The phone offers to join the network, without typing a password |
| Scan the **photos** code | The page loads and shows *that session's* photos |
| Tap one photo | It downloads and opens in the camera roll |
| Tap **Download all as a zip** | A zip downloads, opens, and holds the strip plus every photo |
| Look for `session.json` | It is **not** in the zip and **not** on the page |

### B6. A guest cannot reach anything else

Still on the phone, in the browser address bar. Replace `<booth>` with the
address from B4 and `<port>` with the port from **Guests reach**.

| Try | Expect |
|---|---|
| Change one character of the token in the URL | *"We cannot find those photos"* |
| `http://<booth>:<port>/operator` | Not the operator console |
| `http://<booth>:<port>/api/settings` | Refused |
| Run a **second** session, then reopen the **first** phone link | Still the first session's photos, not the second's |

On `/operator`: the guest port serves an iPad setup page, so **you get a page,
not an error**. Read it. The test is *"is this the operator console"*, not
*"did I get a 404"* — checking the status code alone would pass while the
console was wide open.

### B7. Several phones at once

If you can borrow two or three, join them all and open different sessions at the
same time. **Expect:** each sees only its own. This also sanity-checks that the
router is not hitting a client limit the way the hotspot would.

---

## Part C — breaking it on purpose

Each of these should produce a **visible, specific** warning. A silent failure
here is the bug, even if everything in Part B passed.

### C1. A stale address, as if from the last venue

1. In Setup, pick an address.
2. Unplug the ethernet cable. Wait a few seconds. Reload Setup.

**Expect:** a warning naming the address it can no longer find, saying it has
fallen back, and asking you to pick again.

**Expect NOT:** silence, or a **Guests reach** line still showing the dead
address.

3. Plug the cable back in, reload, pick that address again.

### C2. A wifi name that does not exist

1. In Setup, set the **wifi network** to `NotARealNetwork`. Save.
2. Run a session and scan the join code with the phone.

**Expect:** the phone does nothing, or says it cannot find the network. This is
the documented top failure, and the point is to recognise the symptom — *a code
that scans fine and then nothing happens* means the **name is wrong**, not that
the code is broken.

3. Put the real name back.

### C3. Client isolation, deliberately

1. In the router admin, turn **client isolation ON**.
2. From the phone, reopen a photos link.

**Expect:** the page does not load — the symptom from your home-wifi test.
This is what a venue's network does to you.

3. **Turn it back off.** Confirm the page loads again.

Doing this once means you will recognise it in thirty seconds at an event
instead of spending twenty minutes suspecting the app.

### C4. The Android drift

If you have an Android phone: join it, open the photos page, then leave it for a
few minutes without touching it.

**Expect, honestly:** it may switch itself back to mobile data and lose the page.
That is the phone's behaviour, not a bug — and it is why the sign has to say
*"your phone will say there's no internet; stay connected."* Better to see it
now than to hear about it from a guest.

---

## What "working" means

Everything above passes, and specifically:

- the photos code carries an address you **chose**, not one the app guessed
- a phone on the booth network can reach the photos and **cannot reach the web**
- the laptop having internet does not give the phone internet
- every failure you caused in Part C announced itself on screen

Anything that failed silently is worth reporting even if you found a workaround.
Silent failures are the ones that cost an event.
