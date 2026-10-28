import { useEffect, useState } from 'react'
import type { ClaimBoard, ClaimGuest } from './types'
import { useWakeLock } from './useWakeLock'

/**
 * The claiming station: the screen a guest walks up to after they have accepted
 * their strip, and scans with their own phone.
 *
 * It is deliberately not the booth screen. That one belongs to whoever is in
 * front of the camera and clears the previous guest the moment the next one is
 * armed; this one belongs to whoever has just finished, and keeps them on screen
 * while the next guest is posing.
 *
 * Polled rather than pushed. The list also changes when time passes (a guest
 * leaves the board after a few minutes), which no server event announces, and a
 * station that is a couple of seconds behind costs nothing: the guest is
 * walking over.
 */
export function Claim() {
  useWakeLock()

  const [board, setBoard] = useState<ClaimBoard | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let cancelled = false

    const load = async () => {
      try {
        const r = await fetch('/api/claim')
        if (!r.ok) throw new Error(String(r.status))
        const body = (await r.json()) as ClaimBoard
        if (!cancelled) {
          setBoard(body)
          setFailed(false)
        }
      } catch {
        if (!cancelled) setFailed(true)
      }
    }

    void load()
    const id = setInterval(load, 2000)
    return () => {
      cancelled = true
      clearInterval(id)
    }
  }, [])

  if (!board) {
    return (
      <div className="stage">
        <p className="muted">{failed ? 'Cannot reach the booth…' : 'Connecting…'}</p>
      </div>
    )
  }

  if (!board.enabled) {
    return (
      <div className="stage">
        <h1>Photo pickup is switched off</h1>
        <p className="muted">Turn on “Let guests download their photos” in the booth’s Settings.</p>
      </div>
    )
  }

  const [newest, ...earlier] = board.guests

  return (
    <div className="stage stage--claim">
      {failed && <p className="claim__warn">Lost touch with the booth — retrying…</p>}

      {newest ? (
        <>
          <h1>Scan to get your photos</h1>
          <div className="claim__main">
            <img className="strip" src={newest.stripUrl} alt="Your photo strip" />
            <figure className="qr">
              <img src={photosQr(newest)} alt="QR code for these photos" />
              <figcaption className="qr__caption">
                Point your phone’s camera at this
              </figcaption>
            </figure>
          </div>

          {earlier.length > 0 && (
            <div className="claim__earlier">
              <p className="muted">Still waiting to collect</p>
              <div className="claim__row">
                {earlier.map((g) => (
                  <div className="claim__small" key={g.token}>
                    <img className="strip" src={g.stripUrl} alt="Earlier photo strip" />
                    <img className="claim__qr" src={photosQr(g)} alt="QR code for these photos" />
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      ) : (
        <h1>Your photos will appear here</h1>
      )}

      {board.ssid && (
        <p className="claim__join">
          Not on the wifi yet? Join <span className="handout__ssid">{board.ssid}</span>
          {' '}— the code is on the sign by the queue
        </p>
      )}
    </div>
  )
}

const photosQr = (g: ClaimGuest) =>
  `/api/guest-gallery/qr?target=photos&token=${encodeURIComponent(g.token)}`
