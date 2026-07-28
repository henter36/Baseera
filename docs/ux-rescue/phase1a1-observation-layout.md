# Phase 1A.1 — Observation In-Page Layout

## Desktop

Observation Workspace تعرض:

| المنطقة | السلوك |
| --- | --- |
| Header | عنوان مساحة الملاحظات، الفلاتر، إنشاء ملاحظة، رابط العودة إلى Facility عند وجود `source=facility:*` |
| List pane | قائمة كثيفة قابلة للتمرير داخل الصفحة مع `aria-current` للعنصر المختار |
| Detail pane | تفاصيل الملاحظة داخل `main` في document flow، وليست `dialog` أو overlay |
| Sections | الملخص، المعالجة، التكليف، الأدلة، السجل |

القائمة والتفاصيل موجودتان معًا على Desktop. لا يوجد backdrop، ولا `aria-modal`، ولا scroll lock على `body`.

## Tablet

بين 721px و920px تستخدم القائمة عرضًا أصغر. زر "طي القائمة" يحول التخطيط إلى عمود قائمة مضغوط مع إبقاء التفاصيل داخل الصفحة.

## Mobile

تظهر القائمة أولًا. عند وجود `noteId` تدخل التفاصيل Focus Mode داخل نفس route. زر "رجوع إلى القائمة" يزيل `noteId` فقط ويحافظ على الفلاتر و`facilityId/source`.

## مكونات الواجهة

- `ObservationWorkspacePage`: URL state، الاستعلامات، اختيار الملاحظة.
- `ObservationWorkspaceHeader`: ترويسة مساحة الملاحظات وأوامرها العليا.
- `ObservationMasterDetailLayout`: تخطيط القائمة والتفاصيل فقط.
- `ObservationListPane`: منطقة القائمة.
- `ObservationDetailPane`: منطقة التفاصيل.
- أقسام التفاصيل باقية في الصفحة نفسها: Summary, Processing, Assignment, Evidence, History.

## قاعدة عدم الرجوع

تفاصيل الملاحظة لا تستخدم:

- `<dialog>`
- `aria-modal`
- backdrop
- `position: fixed`
- body scroll lock
- click-outside close
