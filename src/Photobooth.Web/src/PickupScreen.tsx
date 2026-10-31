import { useEffect, useState } from 'react'
import { useWakeLock } from './useWakeLock'

interface ScreenGuest {
  number: number | null
  token: string
  stripUrl: string
  photoCount: number
  hasVideo: boolean
}

interface ScreenResponse {
  guest: ScreenGuest | null
  ssid: string | null
}

/**
 * The claiming station's guest-facing monitor: one guest's strip and the QR
 * that opens their photos, put here by the operator from `/pickup`.
 *
 * It shows nobody else. The operator's screen has the whole day on it; this one
 * is turned towards the guest, so it waits until the operator has found them.
 *
 * Polled rather than pushed: the station already polls the shared folder, and a
 * code that takes a second to appear costs nothing while the guest is getting
 * their phone out.
 */
export function PickupScreen() {
  useWakeLock()

  const [data, setData] = useState<ScreenResponse | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let cancelled = false

    const load = async () => {
      try {
        const r = await fetch('/api/pickup/screen')
        if (!r.ok) throw new Error(String(r.status))
        const body = (await r.json()) as ScreenResponse
        if (!cancelled) {
          setData(body)
          setFailed(false)
        }
      } catch {
        if (!cancelled) setFailed(true)
      }
    }

    void load()
    const id = setInterval(load, 1000)
    return () => {
      cancelled = true
      clearInterval(id)
    }
  }, [])

  if (!data) {
    return (
      <div className="stage">
        <p className="muted">{failed ? 'Cannot reach the claiming station…' : 'Connecting…'}</p>
      </div>
    )
  }

  const { guest, ssid } = data

  return (
    <div className="stage stage--claim">
      {failed && <p className="claim__warn">Lost touch with the claiming station — retrying…</p>}

      {guest ? (
        <>
          <h1>{guest.number !== null ? `Number ${guest.number}` : 'Your photos'}</h1>
          <div className="claim__main">
            <img className="strip" src={guest.stripUrl} alt="Your photo strip" />
            <figure className="qr">
              <img
                src={`/api/guest-gallery/qr?target=photos&token=${encodeURIComponent(guest.token)}`}
                alt="QR code for these photos"
              />
              <figcaption className="qr__caption">
                Scan to download {guest.photoCount} photo{guest.photoCount === 1 ? '' : 's'}, the strip
                {guest.hasVideo ? ' and the video' : ''}
              </figcaption>
            </figure>
          </div>
        </>
      ) : (
        <>
          <h1>Collect your photos here</h1>
          <p className="muted">Tell us the number from the booth screen.</p>
        </>
      )}

      {ssid && (
        <p className="claim__join">
          Your phone must be on the wifi <span className="handout__ssid">{ssid}</span>
          {' '}— the code is on the sign by the queue
        </p>
      )}
    </div>
  )
}
