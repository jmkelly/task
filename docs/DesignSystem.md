# Core Design System (Bootstrap Custom)

---

## 1. Color Palette

Our palette ensures strong contrast, accessibility, and branding. Map these colors to Bootstrap SCSS variables for consistency in utility classes/components.

**Palette:**

| Role         | Sample   | HEX        | Usage                              | Bootstrap Variable         |
|--------------|----------|------------|------------------------------------|---------------------------|
| Primary      | ![#2563eb](https://via.placeholder.com/20/2563eb/FFFFFF?text=+) | #2563EB   | Buttons, links, highlights         | `$primary`                |
| Secondary    | ![#64748b](https://via.placeholder.com/20/64748b/FFFFFF?text=+) | #64748B   | Secondary buttons/CTAs, nav        | `$secondary`              |
| Success      | ![#10b981](https://via.placeholder.com/20/10b981/FFFFFF?text=+) | #10B981   | Success states                     | `$success`                |
| Danger       | ![#dc2626](https://via.placeholder.com/20/dc2626/FFFFFF?text=+) | #DC2626   | Errors, delete actions             | `$danger`                 |
| Warning      | ![#f59e42](https://via.placeholder.com/20/f59e42/FFFFFF?text=+) | #F59E42   | Warnings, highlights               | `$warning`                |
| Info         | ![#0ea5e9](https://via.placeholder.com/20/0ea5e9/FFFFFF?text=+) | #0EA5E9   | Info messages                      | `$info`                   |
| Light        | ![#f3f4f6](https://via.placeholder.com/20/f3f4f6/222222?text=+) | #F3F4F6   | Backgrounds, surface               | `$light`/`$body-bg`       |
| Dark         | ![#111827](https://via.placeholder.com/20/111827/FFFFFF?text=+) | #111827   | Text, nav, footers                 | `$dark`/`$body-color`     |
| Border       | ![#e5e7eb](https://via.placeholder.com/20/e5e7eb/222222?text=+) | #E5E7EB   | Input borders, dividers            | `$border-color`           |

**Accessibility:** Use color contrast tools to ensure WCAG AA/AAA compliance for text & backgrounds. Avoid using color as the only indicator.

---
## 2. Spacing Scale

Base spacing unit: **4px** (rem = 16px base).
Recommended scale (for `gap`, margin, and padding):

| Spacer Name | px  | rem  |
|-------------|-----|------|
| xs          | 4   | 0.25 |
| sm          | 8   | 0.5  |
| md          | 16  | 1    |
| lg          | 24  | 1.5  |
| xl          | 32  | 2    |
| 2xl         | 48  | 3    |

- Use only these increments for all component and layout spacing.
- Match these to Bootstrap spacers: override `$spacers` if needed.

---
## 3. Typography Hierarchy

| Role      | Example                 | Size   | Weight | Line Height | Bootstrap Var     |
|-----------|-------------------------|--------|--------|-------------|------------------|
| H1        | Large heading           | 2rem   | 700    | 1.2         | `$h1-font-size`  |
| H2        | Section heading         | 1.5rem | 600    | 1.3         | `$h2-font-size`  |
| H3        | Subsection/Panel title  | 1.25rem| 600    | 1.35        | `$h3-font-size`  |
| Body lg   | Lead body               | 1.125rem| 400   | 1.6         | `$font-size-lg`  |
| Body      | Default text            | 1rem   | 400    | 1.6         | `$font-size-base`|
| Caption   | Meta/instructions       | 0.875rem| 400   | 1.4         | `$font-size-sm`  |

**Base font:** Inter, system-ui, sans-serif (fallback; override `$font-family-base`).

---
## 4. Button Styles

**Button Types:**
- Primary: solid, brand color (`$btn-primary-bg`)
- Secondary: neutral (`$btn-secondary-bg`)
- Text/Link: transparent bg, brand color text

**States:** normal, hover, active, disabled

![Button Sketch](./assets/button-styles.png)

- Always use at least 40px min height for taps/clicks
- Spacing: 16px horizontal padding standard
- Disabled: 40% opacity, no shadow
- Error/Success: Use `$danger` / `$success` for context

---
## 5. Form Controls

- Input height: 40px minimum
- Padding: horizontal 12–16px, vertical 8–10px
- Border color: `$border-color`; on focus: `$primary`
- Error state: `$danger` border; label and helper in `$danger`
- Success state: `$success` border
- Labels always above inputs, bold, same left align as input

**Accessible feedback:**
 - `aria-invalid`, error message in text
 - Icon indicator (ex: ![Error Icon](./assets/input-error-icon.png))

![Form Control Sketch](./assets/form-control.png)

---
## 6. Layout Rules

- Use Bootstrap grid with 12 columns; override breakpoints only if required
- Content containers: Max width 1140px, centered
- Gaps between cards: use spacing scale
- Always maintain 16–32px content padding

![Grid Layout Sketch](./assets/layout-grid.png)

---
## 7. Recommended Bootstrap Variable Overrides

| Bootstrap Variable        | Recommended Override        |
|--------------------------|----------------------------|
| `$theme-colors`          | Use full palette above     |
| `$spacers`               | Map to spacing scale table |
| `$font-size-base`        | 1rem (16px)                |
| `$font-family-base`      | Inter, system-ui, sans-serif |
| `$btn-border-radius`     | 0.375rem (6px)             |
| `$input-border-radius`   | 0.375rem (6px)             |
| `$input-height`          | 2.5rem (40px)              |
| `$body-bg`               | #F3F4F6                     |
| `$body-color`            | #111827                     |
| `$headings-font-weight`  | 600–700, depending on level |

---
## 8. Error State Appearance

- Border color: #DC2626 (danger)
- Message: below input, 0.875rem, color #DC2626
- Accessible icon (SVG or font): left of message
- Do not rely on color alone—always display icon and text

---
## 9. Sample Component Diagrams

_Replace these with your actual brand assets/diagrams._
- Button States: ![Button Sketch](./assets/button-styles.png)
- Input Field: ![Form Control Sketch](./assets/form-control.png)
- Spacing & Grid: ![Grid Layout Sketch](./assets/layout-grid.png)
- Color Palette Swatches: ![Color Palette](./assets/color-palette.png)

---

## Summary
This design system offers scalable, accessible patterns that can be implemented in Bootstrap via variable overrides and consistent usage of tokens. Use this guide for product consistency, developer handoff, and rapid design-to-dev collaboration.
