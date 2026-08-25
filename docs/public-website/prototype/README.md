# Local public-site prototype

This dependency-free prototype exercises the proposed information architecture,
responsive reading layout, and in-artifact search. It is deliberately not a
production site or public-claim source.

## Run locally

Open `index.html` directly, or serve only on loopback:

```powershell
python -m http.server 4173 --bind 127.0.0.1 --directory docs\public-website
```

Then visit `http://127.0.0.1:4173/prototype/`. Serving the packet root keeps the
prototype's local governance links usable. Stop the process when finished. Do
not bind to a non-loopback interface or publish the directory.

The theme follows the operating-system preference. Append
`?theme=light` or `?theme=dark` to exercise a specific theme.

## Design approach

The prototype is an evidence field guide: ruled content sheets, numbered route
codes, visible planning status, and a contents rail make boundaries inspectable
without using a promise-first marketing layout. Deep rose is the only accent;
neutral surfaces and rules carry the reading hierarchy. There are no remote
assets, decorative illustrations, testimonials, metrics, or invented product
screenshots.

The first viewport pairs the planning statement with the proposed information
architecture. Chapters pair a bounded reading column with route headings and
collapse into one semantic column on narrow screens. Search is an inline
document tool with initial, results, and no-results states. Meaning is always
expressed in text, never color alone.

## Validate

```powershell
node scripts\public-website\validate-prototype.mjs
```

The check starts a temporary loopback-only in-process static server and a
headless local Microsoft Edge process. It verifies representative responsive
viewports, light/dark contrast, 200% text resize, reduced-motion and
forced-colors behavior, mobile navigation, keyboard entry, local search, empty
browser storage, and the absence of cross-origin requests. The process and
browser profile are removed when the check exits.

The validator deliberately keeps a small local Edge/CDP client beside this
prototype. `scripts/evidence/browser-e2e.mjs` has a similar client, but extracting
a shared module would modify the established application evidence harness for a
Phase 0 prototype. Reconsider extraction only when both harnesses need a common
behavior; this check is local Microsoft Edge evidence, not a cross-browser
conformance claim.

Automated checks do not establish WCAG conformance. Manual screen-reader,
keyboard, zoom/reflow, forced-colors, reduced-motion, cognitive, and
localization review remains a publication gate.
