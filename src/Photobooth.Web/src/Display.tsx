import { photoUrl } from './types'
import type { DeliveryUpdate, SessionSnapshot, SessionState } from './types'
import { useCountdown, useSession } from './useSession'
import { backdropStyle, useDisplayTheme } from './useDisplayTheme'
import { useWakeLock } from './useWakeLock'

/**
 * The states before there is anything to look at: waiting for a guest, counting
 * them down, and waiting for the shutter.
 *
 * These used to show a live posing mirror. It was a second camera -- on an iPad,
 * the iPad's own -- and it framed the shot differently from the R50 standing
 * next to it, so guests posed to a picture that was not the one being taken.
 * A preview that disagrees with the photograph is worse than no preview, so the
 * screen now shows only what the booth actually knows: the count, and the shots
 * already taken.
 */
const POSING_STATES: SessionState[] = ['Idle', 'Countdown', 'Collecting', 'TimedOut']

/** The guest-facing screen. Fullscreen on the external monitor. */
export function Display() {
  const { snapshot, delivery, gallery } = useSession()

  // A booth sits idle between guests, and an iPad that has dimmed itself looks
  // broken. Safari has supported this since 16.4; anything older simply carries
  // on without it.
  useWakeLock()
  const backdrop = backdropStyle(useDisplayTheme())

  if (!snapshot) {
    return (
      <div className="stage" style={backdrop}>
        <p className="muted">Connecting…</p>
      </div>
    )
  }

  const { state } = snapshot

  // Reviewing photos is the one time the mirror is not wanted -- guests are
  // looking at what they took, not at themselves.
  if (state === 'ReviewShots') {
    return (
      <div className="stage" style={backdrop}>
        <h1>How do these look?</h1>
        <Filmstrip snapshot={snapshot} large />
      </div>
    )
  }

  if (state === 'Composing') {
    return (
      <div className="stage" style={backdrop}>
        <h1>Making your strip…</h1>
        <Filmstrip snapshot={snapshot} />
      </div>
    )
  }

  if (!POSING_STATES.includes(state)) {
    return (
      <div className="stage stage--done" style={backdrop}>
        <h1>All done</h1>
        <div className="handover">
          {snapshot.stripUrl ? (
            <img className="strip" src={snapshot.stripUrl} alt="Your photo strip" />
          ) : (
            <Filmstrip snapshot={snapshot} />
          )}
          <Handover snapshot={snapshot} delivery={delivery} gallery={gallery} />
        </div>
      </div>
    )
  }

  return (
    <div className="stage stage--posing" style={backdrop}>
      <Overlay snapshot={snapshot} />
      <Caption snapshot={snapshot} />
      <Filmstrip snapshot={snapshot} />
    </div>
  )
}

/**
 * How the guest takes their photos home.
 *
 * The delivery update names the session it belongs to, so this only ever shows a
 * QR for the strip beside it -- an upload still draining from an earlier guest
 * must never put someone else's code on the screen.
 *
 * When there is no link yet the strip is still shown, with an honest line about
 * why. A booth with no signal has not failed the guest: their photos exist, and
 * the operator can send the link on afterwards.
 */
function Handover({
  snapshot,
  delivery,
  gallery,
}: {
  snapshot: SessionSnapshot
  delivery: DeliveryUpdate | null
  gallery: { enabled: boolean; ssid: string | null }
}) {
  // Photos served by the booth itself. Two codes, because there are two steps
  // and the first is the one that loses people: a phone cannot open a page on
  // the booth until it is on the booth's network.
  if (gallery.enabled && snapshot.token) {
    return (
      <div className="handout">
        {gallery.ssid && (
          <figure className="handout__step">
            <img src="/api/guest-gallery/qr?target=join" alt="" />
            <figcaption>
              <b>1.</b> Join the wifi
              <br />
              <span className="handout__ssid">{gallery.ssid}</span>
            </figcaption>
          </figure>
        )}
        <figure className="handout__step">
          <img
            src={`/api/guest-gallery/qr?target=photos&token=${encodeURIComponent(snapshot.token)}`}
            alt=""
          />
          <figcaption>
            <b>{gallery.ssid ? '2.' : ''}</b> Scan for your photos
          </figcaption>
        </figure>
      </div>
    )
  }

  const mine =
    delivery && snapshot.sessionFolder && delivery.sessionFolder === snapshot.sessionFolder
      ? delivery
      : null

  if (!mine?.enabled) {
    return <p className="handover__note">Ask us for your photos.</p>
  }

  if (mine.qrUrl) {
    return (
      <div className="qr">
        <img src={mine.qrUrl} alt="QR code linking to your photos" />
        <p className="qr__caption">Scan to keep your photos</p>
      </div>
    )
  }

  if (mine.state === 'Failed') {
    return <p className="handover__note">Ask us for your photos — we have them safe.</p>
  }

  return <p className="handover__note">Getting your link ready…</p>
}

/**
 * The one thing worth reading from across the booth.
 *
 * It used to sit on top of the mirror; with the mirror gone it is the screen,
 * which is why the number is sized against the viewport rather than set in
 * points.
 */
function Overlay({ snapshot }: { snapshot: SessionSnapshot }) {
  const counting = snapshot.state === 'Countdown'
  const remaining = useCountdown(counting ? snapshot.countdownEndsUtc : null, snapshot.serverNowUtc)

  if (counting && remaining !== null) {
    return (
      <div className="countdown" key={Math.ceil(remaining)}>
        {Math.max(1, Math.ceil(remaining))}
      </div>
    )
  }

  if (snapshot.state === 'Collecting') {
    return (
      <div className="hold">
        {snapshot.retakingSlot !== null
          ? `One more of photo ${snapshot.retakingSlot + 1}`
          : 'Hold it…'}
      </div>
    )
  }

  if (snapshot.state === 'Idle') {
    return <div className="attract">Step in and smile</div>
  }

  return <div className="hold">Just a moment…</div>
}

function Caption({ snapshot }: { snapshot: SessionSnapshot }) {
  if (snapshot.state === 'Idle') {
    return (
      <p className="shotcount">
        {snapshot.shotCount} photos, then your QR code
      </p>
    )
  }

  if (snapshot.retakingSlot !== null) {
    return (
      <p className="shotcount">
        Taking photo {snapshot.retakingSlot + 1} again
      </p>
    )
  }

  return (
    <p className="shotcount">
      Photo {snapshot.currentShot} of {snapshot.shotCount}
    </p>
  )
}

/**
 * Shots so far, with empty slots for the ones still to come, so guests can see
 * how far through the session they are.
 */
function Filmstrip({
  snapshot,
  large = false,
}: {
  snapshot: SessionSnapshot
  large?: boolean
}) {
  const slots = Array.from({ length: snapshot.shotCount })

  return (
    <div className={large ? 'filmstrip filmstrip--large' : 'filmstrip'}>
      {slots.map((_, i) => {
        const photo = snapshot.photos[i]
        return (
          <figure key={i} className={photo ? 'shot' : 'shot shot--empty'}>
            {photo ? <img src={photoUrl(photo)} alt={`Photo ${i + 1}`} /> : <span>{i + 1}</span>}
          </figure>
        )
      })}
    </div>
  )
}
