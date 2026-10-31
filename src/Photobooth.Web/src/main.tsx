import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

// Self-hosted rather than fetched from a font CDN: a booth at a venue with no
// internet would otherwise fall back to system fonts mid-event.
import '@fontsource-variable/plus-jakarta-sans'
import '@fontsource-variable/jetbrains-mono'

import { Claim } from './Claim'
import { Diagnostics } from './Diagnostics'
import { Display } from './Display'
import { Gallery } from './Gallery'
import { Operator } from './Operator'
import { Pickup } from './Pickup'
import { PickupScreen } from './PickupScreen'
import { Templates } from './Templates'
import './styles.css'

// One bundle, several screens. No router: each is opened directly as its own
// path -- and the guest display as its own window entirely -- so matching on
// the path is enough and costs nothing to load.
const path = window.location.pathname.replace(/\/+$/, '')
const view =
  path === '/display' ? <Display />
  : path === '/claim' ? <Claim />
  : path === '/diagnostics' ? <Diagnostics />
  : path === '/templates' ? <Templates />
  : path === '/gallery' ? <Gallery />
  : path === '/pickup' ? <Pickup />
  : path === '/pickup/screen' ? <PickupScreen />
  : <Operator />

createRoot(document.getElementById('root')!).render(<StrictMode>{view}</StrictMode>)
