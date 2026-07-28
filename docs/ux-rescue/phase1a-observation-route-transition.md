# Phase 1A — خطة انتقال الـRoutes

## القاعدة الحاكمة

الـRollback على مستوى **حلّ الـRoute نفسه** (Route resolution)، ليس مجرد إخفاء رابط شريط جانبي. علم الميزة `VITE_OBSERVATION_WORKSPACE_V2` (متغيّر بيئة وقت البناء في Vite):

```ts
// src/frontend/src/pages/notes/observationWorkspaceFlag.ts
export function isObservationWorkspaceV2Enabled(): boolean {
  return import.meta.env.VITE_OBSERVATION_WORKSPACE_V2 !== 'false'
}
```

**مفعَّل افتراضيًا** — القيمة الوحيدة التي تُعطِّله هي `'false'` الحرفية. هذا مقصود: هدف هذه المرحلة أن تصبح التجربة الجديدة هي الأساسية، مع مخرج طوارئ فوري (إعادة نشر بقيمة `false`) دون أي تعديل كود.

## `/notes` (كان يُعرِّف `ObservationWorkspacePage` مباشرة)

```tsx
<Route path="/notes" element={<Protected><NotesIndexRoute /></Protected>} />
```

`NotesIndexRoute` (في `NotesRouteResolvers.tsx`):
- العلم مفعَّل → `<Navigate to="/notes/workspace?<فلاتر آمنة محوَّلة>" replace />` — **`<Navigate>` على مستوى React Router، ليس 301/302 HTTP**. أي حاجة لإعادة توجيه HTTP فعلية (لروابط خارجية/محركات بحث) قرار Server/CDN منفصل يُوثَّق لاحقًا إن طُلِب، لا علاقة له بهذا التغيير.
- العلم معطَّل (`false`) → يُعرِض `<NotesListPage />` (Legacy) فعليًا، كما كان قبل هذه الدفعة تمامًا.

الفلاتر المنقولة (allowlist صريح، لا نقل كل شيء): `search, status, severity, noteTypeId, classification, regionId, facilityId, facilityUnitId, ownerDepartmentId, overdueOnly, requiresMyAction, requiresRouting, sortBy, sortDesc, page`. أي معامل آخر غير معروف يُسقَط بصمت — لا خطأ للمستخدم، لا تسريب معامل غير متوقَّع.

## `/notes/:id` (كان يُعرِّف `NoteDetailPage` مباشرة)

```tsx
<Route path="/notes/:id" element={<Protected><NoteDetailRoute /></Protected>} />
```

`NoteDetailRoute`:
- العلم مفعَّل → `<Navigate to="/notes/workspace?noteId=:id&source=legacy-link&<فلاتر آمنة>" replace />`. هذا يخدم روابط الإشعارات/البريد القديمة مباشرة: تفتح الآن نفس الملاحظة **داخل** Workspace بدل صفحة منفصلة.
- العلم معطَّل → `<NoteDetailPage />` (Legacy) كما كان.

## `/notes/:id/edit` — Legacy fallback غير مشروط

بقي كما هو (`NoteEditPage` مباشرة)، **بلا علم وبلا Resolver** — التعديل الكامل لم يُدمَج بعد داخل الـWorkspace في هذه الدفعة، فيبقى Fallback دائم حتى إشعار لاحق. هذا قرار نطاق صريح، لا سهو.

## `/notes/:noteId/corrective-actions/new` — Legacy fallback مقصود

بقي كما هو أيضًا؛ عملية `ADD_ACTION` داخل الـWorkspace تفتحه كرابط صريح (Fallback متقدّم)، لا Navigate قسري — المستخدم يضغط زرًا واضحًا "إضافة إجراء" يعرف أنه سيغادر السياق مؤقتًا.

## `NotesListPage.tsx` — لم يعد يتيمًا

قبل هذه الدفعة: مكوّن غير مستورَد في أي مكان (يتيم مؤكَّد، موثَّق في `screen-and-route-inventory.md`)، يحمل قدرة فريدة (استعادة ملاحظة مؤرشفة عبر RowVersion) لا تتوفر في أي مكان آخر.

بعد هذه الدفعة: مستورَد فعليًا من `NotesRouteResolvers.tsx` ويُعرَض حين يكون العلم معطَّلاً. **لم يُحذَف أي كود**، ولم تُنقَل قدرة الاستعادة الفريدة إلى مكان آخر بعد — لأنها الآن ما زالت متاحة للاستخدام الفعلي عبر الـLegacy fallback نفسه. نقل هذه القدرة تحديدًا إلى الـWorkspace يبقى بندًا مفتوحًا لـPhase 1B/1C عند إزالة الـLegacy fallback نهائيًا (راجع `phase1a-observation-compliance-ledger.md`).

## اختبار الـRollback

`src/frontend/src/pages/notes/NotesRouteResolvers.test.tsx` — يثبّت `VITE_OBSERVATION_WORKSPACE_V2` داخل كل suite، ويعرض `useLocation()` داخل route الهدف. التغطية تتحقق من توجيه `/notes`→`/notes/workspace`، توجيه `/notes/:id` إلى `noteId` الصحيح، احتفاظ الفلاتر الآمنة، حذف المعاملات غير الآمنة، وتغليب `noteId` القادم من المسار على `noteId` عدائي في query string. عند تعطيل العلم، تبقى صفحات Legacy (`NotesListPage`/`NoteDetailPage`) هي المعروضة.
