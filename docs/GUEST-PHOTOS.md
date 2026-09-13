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

## Setting it up

### 1. A network of your own

You need wifi that the booth and the guests' phones can both be on. **Your own
travel router, or the laptop's hotspot.** Venue wifi usually separates devices
from each other, which blocks this entirely.

The booth does not run the network — it just tells guests how to get onto it.

### 2. Turn it on

**Setup → Guest photos over your wifi → Let guests download their photos.**

Fill in the **wifi network** and **password** — your router's, not the booth's.

> Type the name **exactly as it appears in your phone's wifi list**, case
> included. The booth has no way to check it: it cannot see what networks exist,
> so a name that is wrong, or a network that is switched off, produces a code
> that scans perfectly and then does nothing at all. That is the single most
> likely reason this does not work.

### 3. Check it

Run a session with the mock camera, then from your own phone:

- Scan the join code — the phone should join without typing anything.
- Scan the photos code — your photos, and no one else's.
- Download one, and download the zip. Open both.

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

## The limitation worth knowing

**The link stops working when the guest leaves your wifi.**

This is delivery on the spot. A guest who gets home and realises they did not
save anything cannot open the link again — they have to ask you, and you send
their photos from the output folder.

If links that keep working matter more than independence, that needs the photos
somewhere on the internet, which means an account somewhere. That is the trade
this deliberately takes the other side of.

---

## When it does not work

| What you see | What it means |
|---|---|
| **Scanning the join code does nothing at all** | Almost always the network in Setup is not one that is actually broadcasting -- a placeholder, a typo, or a router that is off. A phone given a code for a network it cannot see has nothing to join and usually says nothing. Check the name matches the network in your phone's own wifi list, exactly, including case |
| The phone offers to join but fails | The password is wrong. Try joining by hand to check it |
| Joined, but the photos page will not load | The network is separating its devices. Use your own router or the laptop's hotspot |
| "We cannot find those photos" | The link is mistyped, or that session has been deleted from the output folder |
| Guests have no internet while joined | Expected on a booth-only router. Phones will grumble; the photos page still works |

---

## Privacy

The photos live on the booth machine and are served only to whoever has the
link — over local wifi, so they never leave the room. Nothing is uploaded
anywhere.

A guest who shares their link with someone else on the same network shares their
photos, exactly as forwarding a cloud link would. Once they leave, the link is
inert.
