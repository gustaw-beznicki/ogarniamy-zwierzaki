import type { JSX } from 'react';

export type IconName = 'search' | 'plus' | 'paw' | 'document' | 'camera' | 'photos' | 'pdf' | 'ocr' | 'shieldCheck' | 'clock' | 'check' | 'alertCircle' | 'refresh' | 'calendar' | 'external' | 'download' | 'pencil' | 'archive' | 'arrowUp' | 'arrowDown' | 'close' | 'mail' | 'lock' | 'logout' | 'chevronLeft' | 'chevronRight' | 'chevronDown' | 'arrowRight' | 'upload' | 'info';

// Stroked 24 px glyphs copied from the design's icon set (design/Main.dc.html) plus the six extras used only in the
// screen files. Stroke, caps and joins come from the `.icon` class in tokens.css.
const glyphs: Record<IconName, JSX.Element> = {
  search: <><circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" /></>,
  plus: <path d="M12 5v14M5 12h14" />,
  paw: <><circle cx="5.6" cy="10.2" r="2" /><circle cx="9.2" cy="5.6" r="2" /><circle cx="14.8" cy="5.6" r="2" /><circle cx="18.4" cy="10.2" r="2" /><path d="M12 12c-2.8 0-5.5 3.2-5.5 5.6 0 1.6 1.2 2.4 2.6 2.4 1.2 0 1.9-.6 2.9-.6s1.7.6 2.9.6c1.4 0 2.6-.8 2.6-2.4 0-2.4-2.7-5.6-5.5-5.6z" /></>,
  document: <><path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" /><path d="M14 3v5h5M9 13h6M9 17h4" /></>,
  camera: <><path d="M4 8.5a2 2 0 0 1 2-2h1.6L9.2 4h5.6l1.6 2.5H18a2 2 0 0 1 2 2V18a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2z" /><circle cx="12" cy="13" r="3.5" /></>,
  photos: <><rect x="7" y="3" width="14" height="14" rx="2.5" /><path d="M3 7.5V19a2 2 0 0 0 2 2h11.5" /><circle cx="12" cy="8" r="1.4" /><path d="m21 13.5-3.6-3.6L10.5 17" /></>,
  pdf: <><path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" /><path d="M14 3v5h5" /><rect x="7.5" y="12" width="9" height="5" rx="1.2" /></>,
  ocr: <><path d="M4 8V6a2 2 0 0 1 2-2h2M16 4h2a2 2 0 0 1 2 2v2M20 16v2a2 2 0 0 1-2 2h-2M8 20H6a2 2 0 0 1-2-2v-2" /><path d="M8 10h8M8 14h5" /></>,
  shieldCheck: <><path d="M12 3 5 6v5.5c0 4.3 3 7.7 7 9.5 4-1.8 7-5.2 7-9.5V6z" /><path d="m9 12 2.2 2.2L15.5 10" /></>,
  clock: <><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></>,
  check: <path d="m5 12.5 4.5 4.5L19 7.5" />,
  alertCircle: <><circle cx="12" cy="12" r="9" /><path d="M12 7.5v5.5M12 16.5h.01" /></>,
  refresh: <><path d="M20 11a8 8 0 0 0-14.6-4.5L4 8.5" /><path d="M4 3.5v5h5" /><path d="M4 13a8 8 0 0 0 14.6 4.5l1.4-2" /><path d="M20 20.5v-5h-5" /></>,
  calendar: <><rect x="3.5" y="5" width="17" height="15" rx="2.5" /><path d="M3.5 10h17M8 3v4M16 3v4" /></>,
  external: <><path d="M14 4h6v6M20 4l-8.5 8.5" /><path d="M18 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4" /></>,
  download: <path d="M12 4v11M7 10l5 5 5-5M5 20h14" />,
  pencil: <><path d="M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16z" /><path d="m13.5 6.5 4 4" /></>,
  archive: <><rect x="3" y="4" width="18" height="5" rx="1.5" /><path d="M5 9v9a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V9M10 13h4" /></>,
  arrowUp: <path d="M12 19V5M6 11l6-6 6 6" />,
  arrowDown: <path d="M12 5v14M6 13l6 6 6-6" />,
  close: <path d="M6 6l12 12M18 6 6 18" />,
  mail: <><rect x="3" y="5" width="18" height="14" rx="2.5" /><path d="m4 7 8 6 8-6" /></>,
  lock: <><rect x="5" y="10.5" width="14" height="10" rx="2.5" /><path d="M8 10.5v-3a4 4 0 0 1 8 0v3" /></>,
  logout: <><path d="M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3" /><path d="M10 17l-5-5 5-5M5 12h11" /></>,
  chevronLeft: <path d="m15 6-6 6 6 6" />,
  chevronRight: <path d="m9 6 6 6-6 6" />,
  chevronDown: <path d="m6 9 6 6 6-6" />,
  arrowRight: <path d="M5 12h14M13 6l6 6-6 6" />,
  upload: <path d="M12 20V9M7 14l5-5 5 5M5 4h14" />,
  info: <><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 8h.01" /></>,
};

// A decorative interface icon. Icons never carry meaning on their own: the surrounding control provides the text or
// accessible name, so the SVG is always hidden from assistive technology.
export function Icon({ name, size = 24, className }: { name: IconName; size?: number; className?: string }): JSX.Element {
  return <svg
    className={className ? `icon ${className}` : 'icon'}
    viewBox="0 0 24 24"
    width={size}
    height={size}
    aria-hidden="true"
    focusable="false"
  >{glyphs[name]}</svg>;
}

// The filled paw used in the logo tile (design/Main.dc.html:51). Colour comes from `currentColor`.
export function BrandMark({ size = 32, className }: { size?: number; className?: string }): JSX.Element {
  return <svg
    className={className ? `brand-mark ${className}` : 'brand-mark'}
    viewBox="0 0 24 24"
    width={size}
    height={size}
    aria-hidden="true"
    focusable="false"
  >
    <circle cx="5.6" cy="10.2" r="2.1" /><circle cx="9.2" cy="5.6" r="2.1" /><circle cx="14.8" cy="5.6" r="2.1" /><circle cx="18.4" cy="10.2" r="2.1" />
    <path d="M12 11.6c-2.9 0-5.7 3.3-5.7 5.8 0 1.7 1.2 2.6 2.7 2.6 1.3 0 2-.7 3-.7s1.7.7 3 .7c1.5 0 2.7-.9 2.7-2.6 0-2.5-2.8-5.8-5.7-5.8z" />
  </svg>;
}
