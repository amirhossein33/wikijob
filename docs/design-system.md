# Frontend design system

Approved direction: **B — Developer Community**. The product uses a content-first feed, visible filters, restrained technical accents and a strong contribution action. The application is Persian and RTL by default. Technical terms and URLs keep their natural Latin direction where needed.

## Tokens and layout

- Dark navigation: `#162722`; primary green: `#168765`; action green: `#20a578`; page background: `#f7f9f7`; surface: white; border: `#dfe8e1`; muted text: `#687970`.
- Local Vazirmatn font files are bundled through `@fontsource/vazirmatn`; Tahoma and Segoe UI are fallbacks.
- Content width is at most 1180 px. Desktop grids use two or three columns; widths below 720 px use a single column, a mobile menu, and stacked filters/forms.
- Shared primitives in `frontend/src/components/ui.tsx` provide Button, Card, Input, Textarea, Field and loading/error/empty states. Radix Dialog is used for content reporting.
- Focus outlines, semantic headings, labelled inputs, a skip link, contrast-aware colors and reduced-motion support are included.
- User-supplied content is rendered as React text. External job-ad URLs are accepted for links only with HTTP(S) schemes.

## Interaction rules

- Feed filters and question search use URL query parameters, preserving navigation state.
- Data requests use TanStack Query with cancellation, a short stale time and explicit loading/error/empty states.
- Mutations display success only after a successful API response. Submission is disabled while pending.
- Hidden public names and company associations remain hidden in cards and detail views.
- Public report values below the backend's five-contribution threshold are not fabricated.
