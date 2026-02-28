# Razor Pages Web App Style Guide

## Table of Contents

1. [Color Palette](#color-palette)  
2. [Typography](#typography)  
3. [Spacing & Layout](#spacing--layout)  
4. [Button Styles](#button-styles)  
5. [Form Elements](#form-elements)  
6. [Accessibility Guidelines](#accessibility-guidelines)  
7. [Reusable UI Components](#reusable-ui-components)  

---

## Color Palette

| Purpose         | Color Name     | Hex      | Usage Example                | WCAG Contrast |
|-----------------|---------------|----------|------------------------------|---------------|
| Primary         | Blue 600       | #2563eb  | Buttons, links, highlights   | 7.0:1         |
| Primary Light   | Blue 100       | #dbeafe  | Backgrounds, hovers          | —             |
| Secondary       | Gray 700       | #374151  | Text, icons                  | 12.6:1        |
| Accent          | Emerald 500    | #10b981  | Success, highlights          | 4.5:1         |
| Error           | Red 600        | #dc2626  | Error states, alerts         | 5.0:1         |
| Warning         | Amber 500      | #f59e42  | Warnings, notifications      | 4.6:1         |
| Background      | White          | #ffffff  | Main background              | —             |
| Surface         | Gray 100       | #f3f4f6  | Cards, panels                | —             |
| Border/Divider  | Gray 300       | #d1d5db  | Borders, dividers            | —             |

**Notes:**  
- Ensure all text/background combinations meet at least 4.5:1 contrast ratio.
- Use color only as a secondary indicator (never as the sole means of conveying information).

---

## Typography

| Element         | Font Family                | Weight   | Size      | Line Height | Usage                |
|-----------------|---------------------------|----------|-----------|-------------|----------------------|
| Headings (h1)   | 'Segoe UI', Arial, sans   | 700      | 2.25rem   | 1.2         | Page titles          |
| Headings (h2)   | 'Segoe UI', Arial, sans   | 600      | 1.5rem    | 1.3         | Section titles       |
| Headings (h3)   | 'Segoe UI', Arial, sans   | 600      | 1.25rem   | 1.3         | Subsection titles    |
| Body           | 'Segoe UI', Arial, sans   | 400      | 1rem      | 1.5         | Main content         |
| Small Text      | 'Segoe UI', Arial, sans   | 400      | 0.875rem  | 1.4         | Captions, help text  |

**Best Practices:**  
- Use system fonts for performance and accessibility.
- Limit to 2 font weights for consistency and speed.
- Use rem units for scalability.

---

## Spacing & Layout

| Spacing Token | Value    | Usage Example         |
|---------------|----------|----------------------|
| XS            | 0.25rem  | Small gaps, icon spacing |
| S             | 0.5rem   | Between form fields  |
| M             | 1rem     | Default padding/margin |
| L             | 2rem     | Section spacing      |
| XL            | 4rem     | Page padding         |

**Guidelines:**  
- Use an 8px (0.5rem) base grid for consistency.
- Maintain consistent vertical rhythm.
- Use generous whitespace for clarity.

---

## Button Styles

### Primary Button

```html
<button class="btn btn-primary">Primary Action</button>
```

**Styles:**  
- Background: `#2563eb` (Blue 600)  
- Text: `#ffffff`  
- Border-radius: `0.375rem` (6px)  
- Padding: `0.5rem 1.25rem`  
- Font-weight: 600  
- Hover: Slightly darker blue, underline text  
- Focus: 2px solid `#2563eb` outline

### Secondary Button

```html
<button class="btn btn-secondary">Secondary Action</button>
```

**Styles:**  
- Background: `#f3f4f6` (Gray 100)  
- Text: `#2563eb`  
- Border: 1px solid `#2563eb`  
- Hover: Background `#dbeafe`  
- Focus: 2px solid `#2563eb` outline

### Disabled State

- Opacity: 0.5  
- Cursor: not-allowed  
- No hover/focus effects

---

## Form Elements

### Input Fields

```html
<input type="text" class="form-input" placeholder="Enter value" />
```

**Styles:**  
- Background: `#ffffff`  
- Border: 1px solid `#d1d5db`  
- Border-radius: `0.375rem`  
- Padding: `0.5rem`  
- Font-size: 1rem  
- Focus: 2px solid `#2563eb` outline

### Labels

- Font-weight: 600  
- Margin-bottom: 0.25rem  
- Always associate with input via `for` attribute

### Error States

- Border: 1px solid `#dc2626` (Red 600)  
- Error message: Red 600, small text below input

### Checkbox/Radio

- Use native elements for accessibility  
- Minimum touch target: 44x44px  
- Custom styles: Use `:focus-visible` for outlines

---

## Accessibility Guidelines

- **Contrast:** All text/background pairs must meet WCAG 2.1 AA (4.5:1 for normal text, 3:1 for large text).
- **Keyboard Navigation:** All interactive elements must be reachable and usable via keyboard (Tab, Shift+Tab, Enter, Space).
- **Focus Indicators:** Always show a visible focus outline (do not remove default unless replaced with a clear custom style).
- **ARIA Labels:** Use ARIA attributes only when native HTML is insufficient.
- **Form Labels:** Every input must have a visible label.
- **Error Feedback:** Provide clear, descriptive error messages and associate them with the relevant input.
- **Alt Text:** All images/icons must have descriptive `alt` attributes.
- **Responsive:** Layouts must adapt to all screen sizes (mobile, tablet, desktop).

---

## Reusable UI Components

### Card

```html
<div class="card">
  <h3>Card Title</h3>
  <p>Card content goes here.</p>
</div>
```

**Styles:**  
- Background: `#ffffff`  
- Border-radius: `0.5rem`  
- Box-shadow: `0 1px 4px rgba(0,0,0,0.04)`  
- Padding: `1.5rem`  
- Margin-bottom: `1rem`

---

### Alert

```html
<div class="alert alert-success" role="alert">
  Success message here.
</div>
```

**Variants:**  
- Success: `#10b981` background, white text  
- Error: `#dc2626` background, white text  
- Warning: `#f59e42` background, dark text

**Styles:**  
- Border-radius: `0.375rem`  
- Padding: `1rem`  
- Icon (optional): Left-aligned

---

### Modal

- Centered overlay with background dim (`rgba(0,0,0,0.5)`)
- Card-style modal window
- Focus trap inside modal
- Close button with accessible label

---

### Navigation Bar

- Horizontal layout on desktop, collapsible on mobile
- High contrast for links
- Clear focus indicators
- Skip to content link for accessibility

---

## Example CSS Snippet

```css
:root {
  --primary: #2563eb;
  --primary-light: #dbeafe;
  --secondary: #374151;
  --accent: #10b981;
  --error: #dc2626;
  --warning: #f59e42;
  --background: #ffffff;
  --surface: #f3f4f6;
  --border: #d1d5db;
  --radius: 0.375rem;
  --font-family: 'Segoe UI', Arial, sans-serif;
}

body {
  font-family: var(--font-family);
  color: var(--secondary);
  background: var(--background);
  line-height: 1.5;
}

.btn-primary {
  background: var(--primary);
  color: #fff;
  border: none;
  border-radius: var(--radius);
  padding: 0.5rem 1.25rem;
  font-weight: 600;
  cursor: pointer;
}

.btn-primary:focus {
  outline: 2px solid var(--primary);
  outline-offset: 2px;
}

.form-input:focus {
  outline: 2px solid var(--primary);
  border-color: var(--primary);
}
```

---

## References

- [WCAG 2.1 AA Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [Accessible Forms](https://www.w3.org/WAI/tutorials/forms/)
- [Color Contrast Checker](https://webaim.org/resources/contrastchecker/)

---

**For questions or updates to this style guide, contact the design team.**
