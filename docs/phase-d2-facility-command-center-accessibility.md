# Phase D.2 Facility Command Center Accessibility

Implemented:
- Semantic `main`, `header`, `nav`, `section`, `aside`, headings, lists, and description lists.
- Context panel uses a dialog role without forcing modal behavior.
- Focus moves to the context panel when opened.
- Focus returns to the selected priority row after close.
- Escape closes the panel.
- Partial data warning uses `output` with polite live announcement.
- Buttons and links keep visible focus rings.
- Color is paired with text labels and priority reasons.
- RTL is applied at the command center root.

Risks to monitor:
- Very long Arabic titles may need truncation in future visual QA.
- Full keyboard roving navigation for the queue can be improved later; native buttons are currently keyboard reachable.

