# Phase 1A.1 — Observation Popup Root Cause

## السبب الفعلي

بعد دمج PR #149 كانت `/notes/workspace` نفسها تعرض القائمة والتفاصيل داخل Grid في الصفحة، لكن مسار السجن اليومي ظل يفتح الملاحظة من `FacilityWorkspacePage` عبر `CommandContextPanel`.

`CommandContextPanel` عنصر `<dialog open>` عام في مساحة السجن. عند `panel=note&entityId=...` كان يحمل `NotePanel` ويجلب `api.notes.workspaceDetail(noteId)` داخل حوار كبير. لذلك كانت رحلة "ملاحظة حرجة من مساحة السجن" تتحول عمليًا إلى popup/detail drawer بدل فتح Observation Workspace الحقيقية.

## الأدلة من الكود قبل التصحيح

| الموضع | الدليل | الأثر |
| --- | --- | --- |
| `src/frontend/src/pages/workspaces/FacilityWorkspacePage.tsx` | `CommandContextPanel` يرندر `<dialog className="command-context-panel" open>` | تفاصيل الملاحظة كانت داخل حوار |
| `PanelDetail` | `panel.type === 'note'` يرندر `NotePanel` | الملاحظة تُدار داخل panel عام لمساحة السجن |
| `panelForPriorityItem` | عناصر `note` تتحول إلى `{ type: 'note', entityId }` | الضغط على ملاحظة من الأولويات لا ينتقل إلى `/notes/workspace` |
| `FacilityWorkspacePage.test.tsx` | الاختبار كان يتوقع `screen.getByRole('dialog')` عند فتح الملاحظة | الاختبار وثق السلوك الخاطئ |
| `src/frontend/src/index.css` | `.command-context-panel` له `position: fixed` | panel يتصرف كـdrawer/overlay كبير |

## القرار

الملاحظات اليومية لا تُفتح داخل `CommandContextPanel`. أي `panel=note` قديم أو ضغط على عنصر ملاحظة من Facility Workspace ينتقل إلى:

```text
/notes/workspace?facilityId=<facilityId>&noteId=<noteId>&source=facility:<facilityId>
```

وبذلك تكون القائمة والتفاصيل داخل Observation Workspace، لا داخل نافذة منبثقة.

## ما لم يتغير

`CommandContextPanel` بقي للمعاينات غير اليومية في Facility Workspace مثل corrective-action preview وdata-quality/risk/workforce panels. هذا التصحيح لا يحذف legacy fallback routes ولا يبدأ Phase 1B.
