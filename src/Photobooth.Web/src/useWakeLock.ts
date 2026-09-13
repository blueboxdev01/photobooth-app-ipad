import { useEffect } from 'react'

/**
 * Keeps the guest screen awake for as long as this page is showing.
 *
 * A booth stands idle between guests, and a display that has dimmed itself reads
 * as a broken booth rather than a sleeping one. Wanted most on an iPad, which is
 * far more eager to sleep than a monitor.
 *
 * The lock is dropped whenever the tab is hidden, so it has to be retaken when
 * the tab comes back -- otherwise the screen stays awake exactly once, until the
 * first time somebody switches away from it.
 */
export function useWakeLock() {
  useEffect(() => {
    // Safari before 16.4, and any browser over plain HTTP, has no wake lock. The
    // display must still work there, just without this.
    if (!('wakeLock' in navigator)) return

    let lock: WakeLockSentinel | null = null
    let cancelled = false

    const take = async () => {
      if (cancelled || document.visibilityState !== 'visible') return
      try {
        lock = await navigator.wakeLock.request('screen')
      } catch {
        // Denied, or the browser decided not to -- never worth breaking the
        // guest screen over.
      }
    }

    const onVisibility = () => {
      if (document.visibilityState === 'visible') void take()
    }

    void take()
    document.addEventListener('visibilitychange', onVisibility)

    return () => {
      cancelled = true
      document.removeEventListener('visibilitychange', onVisibility)
      void lock?.release().catch(() => {})
    }
  }, [])
}
