import { useEffect, useMemo, useState } from 'react'
import { AppShell, Panel } from './AppShell'

interface PickupGuest {
  number: number | null
  token: string
  folder: string
  stripUrl: string
  photoCount: number
  hasVideo: boolean
  createdUtc: string
}

interface PickupResponse {
  day: string | null
  days: string[]
  ssid: string | null
  guests: PickupGuest[]
}

/**
 * The claiming station: every guest for the day, by the number they were given.
 *
 * A guest comes over and says their number. The operator types it, finds the
 * strip, and clicks it to show the QR, which opens that guest's page of
 * photos, strip and video on their phone. Nothing here changes anything: it only
 * reads the sessions folder the booth computer fills.
 *
 * Numbers restart each event day, so the day is part of the question. It
 * defaults to today, or the latest day there is.
 */
export function Pickup() {
  const [data, setData] = useState<PickupResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [day, setDay] = useState<string | null>(null)
  const [query, setQuery] = useState('')
  const [shown, setShown] = useState<PickupGuest | null>(null)

  useEffect(() => {
    let cancelled = false
    const load = async () => {
      try {
        const r = await fetch(day ? `/api/pickup?day=${encodeURIComponent(day)}` : '/api/pickup')
        if (!r.ok) {
          if (!cancelled) setError(`The booth answered ${r.status}.`)
          return
        }
        const body: PickupResponse = await r.json()
        if (!cancelled) {
          setData(body)
          setError(null)
        }
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : 'Could not reach the booth.')
      }
    }

    void load()
    // Quick: a guest is standing there when their number is first asked for.
    const id = setInterval(load, 3000)
    return () => {
      cancelled = true
      clearInterval(id)
    }
  }, [day])

  const typed = query.trim()
  const guests = useMemo(() => {
    const all = data?.guests ?? []
    return typed === '' ? all : all.filter((g) => g.number !== null && String(g.number).includes(typed))
  }, [data, typed])

  // Escape closes the QR, so the operator can go straight back to the number box.
  useEffect(() => {
    if (!shown) return
    const close = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setShown(null)
    }
    window.addEventListener('keydown', close)
    return () => window.removeEventListener('keydown', close)
  }, [shown])

  return (
    <AppShell page="/pickup">
      <Panel title="Claiming station">
        <div className="pickupbar">
          <label className="pickupbar__number">
            Guest number
            <input
              className="control pickupbar__input"
              inputMode="numeric"
              autoFocus
              value={query}
              placeholder="e.g. 12"
              onChange={(e) => setQuery(e.target.value.replace(/\D/g, ''))}
              onKeyDown={(e) => {
                // Enter opens the code straight away when only one guest matches.
                if (e.key === 'Enter' && guests.length === 1) setShown(guests[0])
              }}
            />
          </label>

          {data && data.days.length > 0 && (
            <label>
              Day
              <select
                className="control"
                value={data.day ?? ''}
                onChange={(e) => setDay(e.target.value)}
              >
                {data.days.map((d) => (
                  <option key={d} value={d}>{d}</option>
                ))}
              </select>
            </label>
          )}

          {typed !== '' && (
            <button className="btn" onClick={() => setQuery('')}>Show everyone</button>
          )}
        </div>

        <p className="muted small">
          {data === null
            ? 'Loading…'
            : data.guests.length === 0
              ? 'No guests yet for this day.'
              : typed === ''
                ? `${data.guests.length} guest${data.guests.length === 1 ? '' : 's'} on ${data.day}.`
                : `${guests.length} match${guests.length === 1 ? '' : 'es'} for ${typed}.`}
        </p>

        {error && <p className="banner">{error}</p>}
      </Panel>

      {guests.length > 0 && (
        <div className="pickupgrid">
          {guests.map((g) => (
            <GuestTile key={g.folder} guest={g} onShow={() => setShown(g)} />
          ))}
        </div>
      )}

      {data && data.guests.length > 0 && guests.length === 0 && (
        <Panel>
          <p className="muted small">
            Nobody has number {typed} on {data.day}. Check the number with the guest, or pick another day.
          </p>
        </Panel>
      )}

      {shown && <QrDialog guest={shown} ssid={data?.ssid ?? null} onClose={() => setShown(null)} />}
    </AppShell>
  )
}

function GuestTile({ guest, onShow }: { guest: PickupGuest; onShow: () => void }) {
  const at = new Date(guest.createdUtc)
  const time = at.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })

  return (
    <button className="pickuptile" onClick={onShow} aria-label={`Show the QR for number ${guest.number ?? 'unnumbered'}`}>
      <span className="pickuptile__number">{guest.number ?? '–'}</span>
      <img className="pickuptile__strip" src={guest.stripUrl} alt="" loading="lazy" />
      <span className="pickuptile__when">{time}</span>
      <span className="pickuptile__show">Show QR</span>
    </button>
  )
}

function QrDialog({
  guest, ssid, onClose,
}: { guest: PickupGuest; ssid: string | null; onClose: () => void }) {
  return (
    <div className="pickupdialog" role="dialog" aria-modal="true" onClick={onClose}>
      <div className="pickupdialog__card" onClick={(e) => e.stopPropagation()}>
        <p className="pickupdialog__number">
          {guest.number !== null ? `Number ${guest.number}` : 'Guest'}
        </p>

        <img
          className="pickupdialog__qr"
          src={`/api/guest-gallery/qr?target=photos&token=${encodeURIComponent(guest.token)}`}
          alt="QR code for this guest's photos"
        />

        <p className="pickupdialog__what">
          Scan to download {guest.photoCount} photo{guest.photoCount === 1 ? '' : 's'}, the strip
          {guest.hasVideo ? ' and the video' : ''}
        </p>

        {ssid && (
          <p className="pickupdialog__wifi">
            Your phone must be on the wifi <span className="handout__ssid">{ssid}</span>
          </p>
        )}

        <button className="btn btn--primary" onClick={onClose} autoFocus>Close</button>
      </div>
    </div>
  )
}
