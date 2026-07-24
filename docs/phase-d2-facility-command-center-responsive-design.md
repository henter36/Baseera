# Phase D.2 Facility Command Center Responsive Design

Desktop:
- Sticky command header.
- Situation overview and intervention queue are side by side.
- Context panel opens from the inline end without replacing the workspace.

Tablet:
- Grid collapses to a single primary column.
- Intervention queue remains in normal flow.
- Context panel behaves as an overlay with stable width.

Mobile:
- Header is no longer sticky to avoid consuming vertical space.
- One-column overview/queue flow.
- Context panel fills the screen as detail focus.
- Close button returns to the command scene and preserves filters.
- No horizontal table layout is used.

Reduced motion:
- Animations are only enabled when `prefers-reduced-motion: no-preference`.

