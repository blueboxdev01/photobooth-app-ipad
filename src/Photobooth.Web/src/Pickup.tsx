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
 * strip, and clicks it: the strip and its QR go up on the guest-facing monitor
 * (`/pickup/screen`), and the guest scans it to open their page of photos, strip
 * and video. This screen stays the operator's, so the rest of the day's guests
 * are never in front of the one collecting. It only reads the sessions folder
 * the booth computer fills.
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

  // What the monitor is showing, set from here and read back so a reload of
  // this page still knows.
  const show = async (guest: PickupGuest | null) => {
    try {
      const r = guest
        ? await fetch('/api/pickup/screen', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ folder: guest.folder }),
          })
        : await fetch('/api/pickup/screen', { method: 'DELETE' })
      if (!r.ok) throw new Error(`The station answered ${r.status}.`)
      setShown(guest)
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not reach the guest screen.')
    }
  }

  useEffect(() => {
    void fetch('/api/pickup/screen')
      .then((r) => (r.ok ? r.json() : null))
      .then((body: { guest: PickupGuest | null } | null) => setShown(body?.guest ?? null))
      .catch(() => {})
  }, [])

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

  // Escape takes the QR off the monitor, so the operator can go straight back to
  // the number box.
  useEffect(() => {
    if (!shown) return
    const close = (e: KeyboardEvent) => {
      if (e.key === 'Escape') void show(null)
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
                if (e.key === 'Enter' && guests.length === 1) void show(guests[0])
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

        <div className="pickupnow">
          {shown ? (
            <>
              <span>
                On the guest screen: <b>{shown.number !== null ? `No. ${shown.number}` : 'a guest'}</b>
              </span>
              <button className="btn" onClick={() => void show(null)}>Clear the screen (Esc)</button>
            </>
          ) : (
            <span className="muted small">
              The guest screen is waiting. Open <code>/pickup/screen</code> fullscreen on the
              monitor facing the guests.
            </span>
          )}
        </div>
      </Panel>

      {guests.length > 0 && (
        <div className="pickupgrid">
          {guests.map((g) => (
            <GuestTile
              key={g.folder}
              guest={g}
              onScreen={shown?.folder === g.folder}
              onShow={() => void show(g)}
            />
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

    </AppShell>
  )
}

function GuestTile({
  guest, onScreen, onShow,
}: { guest: PickupGuest; onScreen: boolean; onShow: () => void }) {
  const at = new Date(guest.createdUtc)
  const time = at.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })

  return (
    <button
      className={onScreen ? 'pickuptile pickuptile--onscreen' : 'pickuptile'}
      onClick={onShow}
      aria-label={`Show the QR for number ${guest.number ?? 'unnumbered'} on the guest screen`}
    >
      <span className="pickuptile__number">{guest.number ?? '–'}</span>
      <img className="pickuptile__strip" src={guest.stripUrl} alt="" loading="lazy" />
      <span className="pickuptile__when">{time}</span>
      <span className="pickuptile__show">{onScreen ? 'On the guest screen' : 'Show QR'}</span>
    </button>
  )
}
