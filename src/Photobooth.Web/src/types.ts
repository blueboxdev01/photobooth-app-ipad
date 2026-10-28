export type SessionState =
  | 'Idle'
  | 'Countdown'
  | 'Collecting'
  | 'TimedOut'
  | 'ReviewShots'
  | 'Composing'
  | 'Uploading'
  | 'ShowQr'
  | 'Done'

export interface CapturedPhoto {
  filePath: string
  fileName: string
  sizeBytes: number
  detectedAtUtc: string
}

export interface SessionSnapshot {
  state: SessionState
  shotCount: number
  photos: CapturedPhoto[]
  /**
   * For each entry in `photos`, the 0-based position it was captured in. After
   * dragging the fourth shot to the front of six this is [3, 0, 1, 2, 4, 5], so a
   * thumbnail can still be labelled with the pose it actually is.
   */
  order: number[]
  /** True once the shots have been rearranged, so the console can offer to undo it. */
  isReordered: boolean
  /** Absolute instants, so the browser ticks the countdown locally. */
  countdownEndsUtc: string | null
  /** What the booth’s clock said when this snapshot was built. */
  serverNowUtc: string | null
  timeoutAtUtc: string | null
  startedUtc: string | null
  message: string | null
  capturedCount: number
  currentShot: number
  /**
   * The strip position currently being reshot, 0-based, when one is. Lets both
   * screens name the pose instead of showing a bare countdown.
   */
  retakingSlot: number | null
  /**
   * This session's own unguessable id. It is what a guest's link carries, so the
   * screen can show a code for these photos and no one else's.
   */
  token: string | null
  /** Set once the strip is composed and archived. */
  stripUrl: string | null
  sessionFolder: string | null
  /**
   * The guest's pickup number, once accepted, when the booth hands over by
   * number. What the guest says at the claiming station.
   */
  number: number | null
}

/**
 * The strip as it would print, drawn while the guest decides whether to keep it.
 *
 * `key` names the shots it was drawn from, in strip order. It is only valid for
 * a screen showing exactly those shots; see `previewKey`.
 */
export interface StripPreview {
  key: string
  url: string
}

/** The same string the booth builds, so a screen can tell if a preview is stale. */
export const previewKey = (photos: CapturedPhoto[]) => photos.map((p) => p.fileName).join('|')

/** One finished guest at the claiming station. */
export interface ClaimGuest {
  token: string
  stripUrl: string
  finishedUtc: string
}

export interface ClaimBoard {
  /** Whether the booth is serving guests their photos at all. */
  enabled: boolean
  ssid: string | null
  /** Newest first. */
  guests: ClaimGuest[]
}

/**
 * Delivery, which is deliberately not part of the session snapshot: an upload
 * outlives the session that produced it, so `sessionFolder` says which session
 * this update is about and the screens match it against the one they are showing.
 */
export interface DeliveryUpdate {
  /** Uploading is switched on and a Google client is configured. */
  enabled: boolean
  /** A token is held. False means somebody has to press Re-authorise. */
  authorised: boolean
  pending: number
  failed: number
  lastError: string | null
  sessionFolder: string | null
  state: 'NotAttempted' | 'Pending' | 'Uploaded' | 'Failed' | null
  url: string | null
  qrUrl: string | null
  error: string | null
}

export interface CameraInfo {
  status: 'Disconnected' | 'Ready' | 'Faulted'
  canTrigger: boolean
  watchFolder: string
}

// `width` asks the booth for a reduced copy. The guest display passes one: it
// shows a shot a few hundred pixels across and the original is about 8 MB.
export const photoUrl = (p: CapturedPhoto, width?: number) =>
  `/api/photos/${encodeURIComponent(p.fileName)}${width ? `?w=${width}` : ''}`
