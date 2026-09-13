# Letting guests take their own photos

The booth serves each guest their photos over its own wifi. They scan a code,
open a page, and download. **No account, no cloud, no internet.**

This replaced Google Drive after the booth's Google account was suspended and
took every guest's link with it. Nothing here can be suspended.

---

## What a guest does

At the end of a session the guest screen shows two codes:

1. **Join the wifi** — a code their phone camera recognises as a network. Both
   iOS and Android join from the camera app without typing a password.
2. **Scan for your photos** — their own session, and nobody else's.

The page has the strip, each photo, and a **Download all as a zip**, because a
phone taking six files one at a time is something people give up on halfway.

---

## Do guests get our internet?

**No — and that is a property of the network you build, not a setting in the
app.** Build it the way this page describes and there is no internet on it to
give away.

| Setup | Guests get internet |
|---|---|
| **Travel router, WAN port empty** — use this | **No.** No uplink, so there is nowhere to forward their traffic |
| The laptop's Mobile Hotspot | **Yes.** Mobile Hotspot *is* Internet Connection Sharing: it re-shares whatever the laptop is connected to |
| Venue wifi | Yes, and it usually blocks the gallery anyway |

A router with nothing in its WAN port still runs its own DHCP and still switches
traffic between the devices on it. It is a complete local network that simply
has no way out. Guests reach the booth, and nothing else.

**The booth laptop can be on both networks at once** — the guests' router by
cable, your own wifi for you — without becoming a bridge between them. Windows
does not route between adapters unless something turns that on, and the one
thing that turns it on is Mobile Hotspot. So use the router, and leave Mobile
Hotspot alone at events.

---

## Setting it up

### 1. A network of your own

**A travel router with nothing plugged into its WAN port.** Plug the booth
laptop into one of its **LAN** ports by cable, and let guests join its wifi.

Three reasons for the cable, rather than putting the laptop on the router's wifi:

- the booth's connection cannot drop or roam mid-session
- it leaves the wifi radio entirely to the guests
- the router can give the laptop a **fixed address**, so the photos code points
  at the same place at every event

**Not the laptop's Mobile Hotspot.** Besides handing guests your internet, it
**caps at 8 connected devices** — and phones stay joined long after their owner
has walked away, so those 8 fill with people who already have their photos.
Guest nine simply cannot join, with nothing on screen to explain why.

**Not venue wifi.** It nearly always separates its devices from one another,
which blocks the gallery completely.

The booth does not run the network — it just tells guests how to get onto it.

#### Router settings that matter

| Setting | Value | Why |
|---|---|---|
| Mode | **Router** (not repeater or bridge) | It has to run its own DHCP |
| "Guest network" | **Off** — put guests on the main wifi | Guest mode turns on client isolation, which is exactly what blocks the gallery |
| AP / client isolation | **Off** | Same reason |
| Booth laptop | **Fixed address** (DHCP reservation) | So the photos code keeps working |
| WAN port | **Empty** | This is what makes the network internet-free |

The "guest network" one reads backwards and catches people out. On a router,
*guest* means **isolated from the other devices** — and the booth is a device
your guests need to reach.

### 2. Turn it on

**Setup → Guest photos over your wifi → Let guests download their photos.**

Fill in the **wifi network** and **password** — your router's, not the booth's.

> Type the name **exactly as it appears in your phone's wifi list**, case
> included. The booth has no way to check it: it cannot see what networks exist,
> so a name that is wrong, or a network that is switched off, produces a code
> that scans perfectly and then does nothing at all. That is the single most
> likely reason this does not work.

### 3. Tell the booth which address to advertise

**Setup → Guest photos over your wifi → Guests reach the booth at.**

A booth laptop is routinely on more than one network — the guests' router and
your own wifi — and **only one of those addresses is reachable from a guest's
phone**. The photos code can carry only one of them.

Pick the one on the guests' network. The list shows the adapter beside each
address (`Ethernet — 192.168.8.100`), which is what makes them tellable apart.

Leaving it on **Choose automatically** is fine when the booth is on a single
network, and Setup says so plainly when it is not: *"This booth is on 2 networks
and is guessing."* Do not leave it guessing at an event.

If a saved choice is no longer one of this machine's addresses — you set it at
the last venue — Setup says that too, and falls back rather than advertising an
address that cannot answer.

### 4. Check it

Run a session with the mock camera and test it from a real phone. The full
procedure is in [TESTING-GUEST-PHOTOS.md](TESTING-GUEST-PHOTOS.md).

---

## What a link can and cannot reach

Each link carries a **random, unguessable session id** — the same one the booth
has minted per session since the beginning, for exactly this purpose.

| | |
|---|---|
| A guest's own strip and photos | **Yes** |
| Another guest's session | No — a different id, and nothing lists them |
| `session.json` | No. It carries the id, and a guest forwarding their zip should not be forwarding the key to their own gallery |
| The operator console, setup, or anything that can end a session | No. Refused on the guest port, verified by test |

A wrong id and a session that has been deleted return **exactly the same
answer**, so guessing tells you nothing.

---

## The limitations worth knowing

**The link stops working when the guest leaves your wifi.**

This is delivery on the spot. A guest who gets home and realises they did not
save anything cannot open the link again — they have to ask you, and you send
their photos from the output folder.

If links that keep working matter more than independence, that needs the photos
somewhere on the internet, which means an account somewhere. That is the trade
this deliberately takes the other side of.

**Phones complain that the network has no internet.**

That is the cost of an internet-free network, and it is worth knowing before the
event rather than during it. iOS shows a warning and stays put. **Android is the
awkward one**: many phones offer to switch back to mobile data, and some do it
on their own — taking the photos page with them.

So tell guests, out loud and on the sign: *"Your phone will say there's no
internet. That's normal — stay connected."* Without that, a fraction of guests
will quietly lose the page and assume the booth is broken.

---

## When it does not work

| What you see | What it means |
|---|---|
| **Scanning the join code does nothing at all** | Almost always the network in Setup is not one that is actually broadcasting -- a placeholder, a typo, or a router that is off. A phone given a code for a network it cannot see has nothing to join and usually says nothing. Check the name matches the network in your phone's own wifi list, exactly, including case |
| The phone offers to join but fails | The password is wrong. Try joining by hand to check it |
| **Joined, but the photos page never loads** | Two causes, in this order. **One:** the booth is advertising the wrong address -- check that *Guests reach the booth at* names the guests' network. **Two:** the network is separating its devices; turn off the router's guest mode and client isolation |
| The page loaded once and then stopped | An Android phone has switched itself back to mobile data. Rejoin the wifi and tell it to stay |
| "We cannot find those photos" | The link is mistyped, or that session has been deleted from the output folder |
| Guests have no internet while joined | Expected, and deliberate. Phones will grumble; the photos page still works |

---

## Privacy

The photos live on the booth machine and are served only to whoever has the
link — over local wifi, so they never leave the room. Nothing is uploaded
anywhere.

A guest who shares their link with someone else on the same network shares their
photos, exactly as forwarding a cloud link would. Once they leave, the link is
inert.
