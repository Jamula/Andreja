# Design

## Scope

This visual system describes the local Phase 0 public/help-site prototype under
`docs/public-website/prototype/`. It does not replace the authenticated
application's interface or establish a cleared public brand.

## Direction

The prototype is an evidence field guide: ruled content sheets, numbered route
codes, visible status marks, and a strong contents rail make boundaries easy to
inspect. It refuses a promise-first marketing page. The first viewport gives
equal structural weight to the status statement and the proposed information
architecture.

The surface uses the Clawpilot light/dark theme tokens required for this HTML
artifact. Deep rose is the only product accent; neutral surfaces and rules carry
the reading hierarchy. There are no gradients, remote assets, decorative
illustrations, testimonials, metrics, or invented product screenshots.

## Typography

- Primary: `"Segoe UI", Aptos, Calibri, -apple-system, BlinkMacSystemFont,
  sans-serif`
- Data and route codes: `Consolas, "Courier New", Courier, monospace`
- Display headings use the primary family at heavy weight, compact line height,
  and no tighter than `-0.04em` tracking.
- Body copy stays within approximately 72 characters per line.
- Hierarchy comes from size, weight, measure, and ruled position rather than
  uppercase micro-labels or ornamental type.

## Layout

- Maximum reading frame: 76rem with fluid edge padding.
- First viewport: statement sheet plus numbered guide rail; below 62rem the rail
  becomes the next block in reading order.
- Chapters pair a sticky route heading with a bounded reading column; narrow
  screens return to one semantic column.
- Information groups use rules and open rows before cards. Rounded surfaces are
  reserved for the search sheet, status pill, note, and controls.
- Spacing follows a 4px-derived scale and uses generous chapter separation.

## Components and states

- The persistent planning banner is the strongest status surface and is never
  dismissible.
- Navigation collapses to a labeled 44px menu control on smaller viewports.
- Search is an inline document tool, not a modal. It has initial, results, and
  no-results states announced through a polite live region.
- Links remain underlined in running copy. Primary actions use the accent fill.
- Focus uses a high-visibility two-ring treatment that works in both themes.
- Status and meaning are always expressed in text, never by color alone.

## Motion and adaptation

The prototype uses only smooth anchor scrolling. Reduced-motion preference
disables it. There are no entrance sequences, parallax, auto-advancing regions,
or animated decoration. Forced-colors mode preserves explicit control and
status boundaries.

## Content and imagery

Only planning content grounded in the repository is allowed. Availability,
security, privacy, accessibility, legal, commercial, customer, and performance
claims remain gated by `docs/public-website/claims-inventory.md`. No image or
brand asset is currently authoritative.

## Accessibility floor

Semantic landmarks, a single page heading, skip navigation, labeled controls,
keyboard operation, visible focus, 44px form/button targets, 320 CSS-pixel
reflow, theme contrast, reduced motion, and forced-colors support are required.
Automated checks remain regression evidence, not a WCAG conformance claim.
