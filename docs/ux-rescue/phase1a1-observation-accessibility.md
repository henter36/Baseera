# Phase 1A.1 — Accessibility

## Desktop

- List pane لديه `aria-label="قائمة الملاحظات"`.
- عنصر الملاحظة المختار يستخدم `aria-current="true"`.
- تفاصيل الملاحظة داخل `main` وليست modal.
- عند اختيار ملاحظة ينتقل focus إلى عنوان التفاصيل.
- عند الرجوع إلى القائمة يعود focus إلى عنصر الملاحظة المختار عندما يكون موجودًا.

## Mobile

- زر "رجوع إلى القائمة" أول أمر عملي داخل التفاصيل.
- Detail Focus Mode لا يستخدم `aria-modal`.
- Browser back يعمل عبر URL state، لا عبر close handler خاص بالحوار.

## Modal regression

اختبار `Observation detail must not render as modal or overlay` ممثل في:

```text
ObservationWorkspacePage.test.tsx
renders observation detail as in-page master-detail content, not as modal or overlay
```

يتحقق من غياب:

- `role="dialog"`
- `[aria-modal="true"]`
- backdrop/overlay selectors
- body scroll lock
