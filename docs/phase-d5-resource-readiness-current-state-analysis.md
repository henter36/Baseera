# Phase D.5 Resource Readiness Current State Analysis

Status: implementation analysis completed before Phase D.5 changes.

Phase D.5 starts from `main` after the Workspace Framework, Facility Workspace MVP, Command Center UX, Facility Operations expansion, and Occupancy domain merges. The goal is the first core-assets slice of Issue #15, integrated into the existing Facility Workspace without adding personnel, weapons, inventory, Region Workspace, Headquarters Workspace, or predictive engines.

## Existing State Matrix

| المجال | الكيان موجود | API موجودة | شاشة موجودة | مصدر الحقيقة | الفجوة | قرار المرحلة |
| ------ | -----------: | ---------: | ----------: | ------------ | ------ | ------------ |
| المركبات | لا يوجد كيان مركبة فعلي؛ توجد صلاحيات قديمة فقط | لا | لا | غير متوفر | لا يوجد أصل، حالة، موقع، صيانة، أو احتياج | تنفيذ `ResourceAsset` + `VehicleProfile` ضمن مركز الموارد |
| أجهزة الاتصال | لا | لا | لا | غير متوفر | لا يوجد تصنيف أو جاهزية أو فحص | تنفيذ `CommunicationDeviceProfile` |
| المعدات التشغيلية | لا | لا | لا | غير متوفر | لا يوجد نموذج معدات أو معايرة/فحص | تنفيذ `EquipmentProfile` للفئات التشغيلية والأمنية غير الأسلحة |
| المعدات الأمنية غير الأسلحة | لا | لا | لا | غير متوفر | يجب فصلها عن الأسلحة والعهد الحساسة | تنفيذها كـ`SecurityEquipment` ضمن `ResourceType`، بدون أسلحة أو ذخيرة |
| المرافق والأصول الثابتة | يوجد `Building` و`FacilityAssetLocation` فقط كموقع تنظيمي | لا توجد API أصول ثابتة | تعرض ضمن هيكل السجن كأعداد | الهيكل التنظيمي الحالي | لا يوجد أصل ثابت، حالة، فحص، أو صيانة | تنفيذ `FacilityAssetProfile` وربطه بالمبنى/الوحدة/الموقع |
| الموقع التشغيلي | توجد `FacilityUnit` و`Building` و`FacilityAssetLocation` | API وحدات عامة فقط | موجودة في هيكل السجن | الهيكل التنظيمي | لا توجد placement تاريخية ولا فصل عن الملكية | تنفيذ `ResourcePlacement` مع placement نشطة واحدة |
| الملكية | توجد منظمة فقط | لا | لا | غير محدد | الملكية غير منفصلة عن الموقع | تخزين `OwnershipOrganizationId` في الأصل والـplacement |
| الحالة والجاهزية | لا | لا | لا | غير متوفر | لا يوجد تاريخ حالة أو state machine | تنفيذ `ResourceStatusEvent` و`ResourceReadinessPolicy` |
| الصيانة | لا | لا | لا | غير متوفر | لا توجد أوامر صيانة أو توقف أو تأخر | تنفيذ `MaintenanceWorkOrder` الأساسي |
| الاحتياج والفجوة | لا | لا | لا | غير متوفر | لا توجد baseline للاحتياج | تنفيذ `ResourceRequirement` وحساب gap بدون جدول مشتق |
| الاستيراد | لا يوجد للموارد | لا | لا | غير متوفر | لا يوجد batch أو idempotency | تنفيذ batch preview/confirm bounded دون تخزين ملف خام |
| التدقيق | `AuditLog` وخدمة تدقيق موجودة | نعم للعمليات العامة | صفحة Audit موجودة | Audit الحالي | يحتاج أحداث موارد آمنة ومختصرة | إعادة استخدام `IAuditService` وعدم تسجيل payload خام |
| الملاحظات والإجراءات | موجودة | نعم | نعم | خدمات Notes/CorrectiveActions | الربط التفصيلي الكامل مؤجل | إظهار أفعال مسموحة/روابط آمنة دون نسخ منطقها |
| الإشغال | موجود من D.4 | نعم | نعم | Snapshot وحركات نزلاء | خارج نطاق الموارد عدا أثر الطاقة | عدم تعديل Occupancy في D.5 |

## Reusable Services And Patterns

- `IOrganizationalScopeService` و`ICurrentUser` يفرضان facility/region/HQ scope.
- `PermissionCodes`, `AuthPolicies`, و`DatabaseInitializer` توفر نمط صلاحيات المجال.
- Occupancy D.4 يوفر نمط DTOs، services، endpoints، EF configurations، واختبارات SQL-backed.
- Facility Workspace D.3 يوفر section navigation, Context Panel, Data Quality, Priority Queue, Timeline.
- `IAuditService` يستخدم لتسجيل أحداث مختصرة بدل payload خام.

## Security And Privacy Risks

- أرقام اللوحات ومعرفات الأجهزة لا تعرض لمن يملك summary فقط.
- `Workspaces.ViewFacility` لا يكفي لعرض الموارد؛ يجب وجود صلاحيات `Resources.*`.
- يجب منع cross-facility reads في كل endpoint وworkspace widget.
- استيراد الموارد يجب أن يمنع duplicate asset codes ولا يسجل محتوى ملف خام.
- الأسلحة والذخائر والعهد الفردية الحساسة خارج هذه المرحلة.

## Performance Risks

- التجميع يجب أن يكون server-side ولا يعتمد على تحميل كل الأصول.
- placement/status/latest maintenance يجب أن تكون bounded ومفهرسة.
- Workspace payload يجب أن يعرض summary/category/exceptions فقط، وليس قائمة أصول كاملة.
- Context Panel وasset list يستخدمان pagination وTop N.

## Phase Boundaries

Included: core resource assets, profiles for vehicles/communication/equipment/facility assets, status history, placements, maintenance work orders, requirements, readiness/gap/data-quality calculations, bounded import, Facility Workspace integration.

Excluded: workforce, weapons/ammunition, sensitive individual custody, procurement/financial contracts, warehouse inventory, Region/HQ workspaces, AI/prediction.

## Expected File Map

- `src/backend/Baseera.Domain/Resources/*`
- `src/backend/Baseera.Application/Resources/*`
- `src/backend/Baseera.Infrastructure/Persistence/Configurations/ResourceConfigurations.cs`
- `src/backend/Baseera.Infrastructure/Persistence/Migrations/*PhaseD5ResourceReadinessCore*`
- `src/backend/Baseera.Api/Endpoints/ApiEndpoints.cs`
- `src/backend/Baseera.Api/Authorization/AuthorizationExtensions.cs`
- `src/backend/Baseera.Infrastructure/Persistence/DatabaseInitializer.cs`
- `src/backend/Baseera.Application/Workspaces/FacilityWorkspace*`
- `src/frontend/src/api/client.ts`
- `src/frontend/src/pages/resources/FacilityResourcesPage.tsx`
- `src/frontend/src/pages/workspaces/FacilityWorkspacePage.tsx`
- `src/frontend/src/index.css`
- `docs/phase-d5-resource-*.md`
