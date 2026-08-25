# Local public-site prototype

This dependency-free prototype exercises the proposed information architecture,
responsive reading layout, and in-artifact search. It is deliberately not a
production site or public-claim source.

## Run locally

Open `index.html` directly, or serve only on loopback:

```powershell
python -m http.server 4173 --bind 127.0.0.1 --directory docs\public-website\prototype
```

Then visit `http://127.0.0.1:4173/`. Stop the process when finished. Do not bind
to a non-loopback interface or publish the directory.

The theme follows the operating-system preference. Append
`?scoutTheme=light` or `?scoutTheme=dark` to exercise a specific theme.

## Validate

```powershell
node scripts\public-website\validate-prototype.mjs
```

The check starts a temporary loopback-only in-process static server and a
headless local Microsoft Edge process. It verifies representative responsive
viewports, semantic/accessibility invariants, keyboard entry, local search,
empty browser storage, and the absence of cross-origin requests. The process and
browser profile are removed when the check exits.

Automated checks do not establish WCAG conformance. Manual screen-reader,
keyboard, zoom/reflow, forced-colors, reduced-motion, cognitive, and
localization review remains a publication gate.

