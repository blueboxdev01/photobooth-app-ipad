#!/bin/bash
# Opens one of the booth's guest screens fullscreen on this Mac and keeps the Mac
# awake. By default that is the CLAIMING STATION (/claim): the screen a guest
# scans after they have accepted their strip.
#
# Double-click it in Finder, or run it from Terminal. The first run asks for the
# booth laptop's address and remembers it; to change it later, delete
# ~/.photobooth-display or pass the address as the first argument.
#
#   ./mac-display.command 192.168.8.100            claiming station
#   ./mac-display.command 192.168.8.100 --display  the booth screen instead
#
# The address is the one on the Windows booth's Settings page, under "This
# machine" -- the same one entered under "Guests reach the booth at".

set -euo pipefail

SAVED="$HOME/.photobooth-display"
PROFILE="$HOME/.photobooth-display-profile"
CHROME="/Applications/Google Chrome.app"

host=""
page="claim"
for arg in "$@"; do
  case "$arg" in
    --display) page="display" ;;
    --claim)   page="claim" ;;
    *)         host="$arg" ;;
  esac
done
if [[ -z "$host" && -f "$SAVED" ]]; then
  host="$(cat "$SAVED")"
fi
if [[ -z "$host" ]]; then
  read -r -p "Booth laptop address (e.g. 192.168.8.100): " host
fi
host="${host#https://}"; host="${host#http://}"; host="${host%%/*}"; host="${host%%:*}"
[[ -n "$host" ]] || { echo "No address given."; exit 1; }
echo "$host" > "$SAVED"

# Step 1 of 2: can this Mac see the booth at all? Plain HTTP, no certificate
# involved, so a failure here is the network and not the trust step.
if ! curl -fsS --max-time 5 -o /dev/null "http://$host:5001/"; then
  echo
  echo "Cannot reach http://$host:5001/ from this Mac."
  echo "Check: same router as the booth laptop, the booth is running, Settings ->"
  echo "'Serve the display' is on and the booth was restarted after switching it on,"
  echo "and Windows Firewall allows Photobooth.Server on Private networks."
  exit 1
fi

# Step 2 of 2: is the booth's certificate trusted? If not, Chrome would show a
# warning page on the guest screen; better to say so here.
if ! curl -fsS --max-time 5 -o /dev/null "https://$host:5002/$page"; then
  echo
  echo "The booth answers, but its certificate is not trusted on this Mac yet."
  echo "Open http://$host:5001/cert, open the downloaded file, then in Keychain"
  echo "Access double-click 'Photobooth booth authority' -> Trust -> Always Trust."
  exit 1
fi

# Keep the display, and the Mac, awake for as long as this window stays open.
caffeinate -dis &
CAFFEINATE=$!
trap 'kill $CAFFEINATE 2>/dev/null || true' EXIT

echo "Opening /$page. Quit with Cmd+Q; closing this window also stops keep-awake."

if [[ -d "$CHROME" ]]; then
  # Its own profile, so it opens as a separate kiosk window even when Chrome is
  # already running for something else.
  open -n -W -a "$CHROME" --args \
    --user-data-dir="$PROFILE" --kiosk --no-first-run \
    --disable-session-crashed-bubble --noerrdialogs \
    "https://$host:5002/$page"
else
  open -W -a Safari "https://$host:5002/$page"
fi
