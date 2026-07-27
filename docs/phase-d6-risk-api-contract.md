# Phase D.6 — عقد الـ API

جميع نقاط النهاية مُعرَّفة في `Baseera.Api/Endpoints/RiskManagementEndpoints.cs` (Minimal API، نفس نمط `ApiEndpoints.cs`)، ومُسجَّلة عبر `RiskManagementEndpoints.MapRiskManagementEndpoints(api)` داخل `MapBaseeraApi`.

## Facility scope (`/api/v1/facilities/{facilityId:guid}/risks/...`)

| Method | المسار | الصلاحية | ملاحظات |
| --- | --- | --- | --- |
| GET | `/summary` | `Risks.ViewSummary` | |
| GET | `/` | `Risks.View` | `RiskListQueryParams` عبر `[AsParameters]`؛ ترقيم صفحات (حد أقصى 50). |
| GET | `/categories` | `Risks.ViewSummary` | |
| POST | `/categories` | `Risks.ManageCategories` | |
| GET | `/{riskId}` | `Risks.View` | 404 عند عدم الوجود ضمن النطاق. |
| POST | `/` | `Risks.Create` | |
| PUT | `/{riskId}` | `Risks.Update` | RowVersion إلزامي، 409 عند التعارض. |
| POST | `/{riskId}/command` | `Risks.View` (حد أدنى) | الصلاحية الفعلية تُفرض داخليًا لكل نوع أمر (`AssignOwner`, `StartMonitoring`, `Escalate`, `Reopen`, `Archive`). |
| GET/POST | `/{riskId}/assessments`, `/{assessmentId}/submit`\|`/review`\|`/approve` | `Risks.Assess` / `Risks.ReviewAssessment` / `Risks.ApproveAssessment` حسب الفعل | |
| GET/POST | `/{riskId}/controls`, `/{controlId}/test` | `Risks.ManageControls` | |
| GET/POST | `/{riskId}/treatments`, `/{planId}/command`, `/{planId}/actions`, `.../actions/{actionId}/command` | `Risks.ManageTreatments` (حد أدنى للأوامر المركّبة، فصل مهام فعلي داخليًا) | |
| GET/POST | `/{riskId}/reviews`, `/{reviewId}/decision` | `Risks.View` (حد أدنى) | نوع المراجعة يحدد الصلاحية الفعلية داخليًا. |
| GET/POST/DELETE | `/{riskId}/sources`, `/{linkId}` | `Risks.LinkSources` | DELETE يتطلب `RemovalReason` في الجسم. |
| GET | `/interventions` | `Risks.ViewSummary` | حد أقصى قابل للتهيئة (افتراضي 20، حد أعلى 50). |
| GET | `/data-quality` | `Risks.ViewSummary` | |
| POST | `/import/preview`, `/import/confirm` | `Risks.Import` | Idempotent على (facility, ImportKind.RiskRecords, FileHash). |
| GET/POST | `/reconciliation`, `/reconciliation/resolve` | `Risks.Import` | إضافة عملية لم تُطلب صراحة في قائمة المسارات، أُضيفت لأن `IRiskReconciliationService` بلا endpoint كان سيبقى غير قابل للوصول. |

## Organization scope (`/api/v1/risk-matrices`)

| Method | المسار | الصلاحية |
| --- | --- | --- |
| GET | `/?organizationId=` | `Risks.View` |
| POST | `/?organizationId=` | `Risks.ManageMatrices` |
| POST | `/{matrixId}/approve?organizationId=` | `Risks.ApproveMatrices` |
| POST | `/{matrixId}/activate?organizationId=` | `Risks.ApproveMatrices` |

**انحراف موثَّق عن نص المواصفة**: المسار الأصلي المقترح `POST /api/v1/risk-matrices` بلا معامل. بما أن مصفوفات التقييم مؤسسية (مستوى منظمة) ولا يوجد مفهوم "منظمة حالية ضمنية" في اصطلاحات هذا المشروع (كل شيء آخر Facility/Region-scoped صراحة)، أُضيف `organizationId` كمعامل استعلام إلزامي بدل تخمين قيمة ضمنية. القرار موثَّق هنا بدل تركه صامتًا.

## رموز الحالة (مطابقة للنمط العام الموجود في `Middleware.cs`، لم يُضَف نمط جديد)

* `403` — `UnauthorizedAccessException` (صلاحية مفقودة).
* `404` — `KeyNotFoundException` (خارج النطاق أو غير موجود).
* `409` — `InvalidOperationException` (RowVersion، انتقال حالة غير صالح، فصل مهام، قواعد عمل مثل "المبرر مطلوب" أو "التقييم المتبقي غير معتمد"). **لا تُستخدم 422** لهذه الحالات — هذا اتساق متعمد مع الاصطلاح الموجود فعليًا في بقية الوحدات (SensitiveCustody، Resources)، وانحراف واعٍ عن نص المواصفة الذي اقترح تمييز 409/422.

## متطلبات إضافية مطبَّقة

* `AsNoTracking()` لكل استعلامات القراءة.
* `CancellationToken` في كل توقيع.
* `RowVersion` (Base64) في كل استجابة قابلة للتعديل.
* لا تصدير غير محدود (لا يوجد تصدير أصلًا في هذه المرحلة — انظر Compliance Ledger).
