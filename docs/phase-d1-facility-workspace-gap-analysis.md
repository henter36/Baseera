# Phase D.1 Facility Workspace MVP Gap Analysis

## المتاح فعليًا

- Workspace Framework من Issue #10: `WorkspaceDefinition`, `WidgetDefinition`, `WorkspaceContextResolver`, `WorkspaceQueryService`, `WorkspaceShell`, وواجهات `/api/v1/workspaces/{workspaceKey}`.
- Facility-level context server-side: `WorkspaceContextResolver` يفرض `level=Facility`, وجود `facilityId`, صلاحية `Workspaces.ViewFacility`, و`IOrganizationalScopeService.CanAccessFacility`.
- بيانات السجن والمنطقة: `Facilities` و`Regions` متاحة ومفلترة بالنطاق.
- الملاحظات التشغيلية: `OperationalNotes`, `NoteAssignments`, `NoteStatusHistory`, `NoteRoutingDecisions`, `NoteTypeAccessService`, و`OperationalDashboardFilterBuilder`.
- الإجراءات التصحيحية: `CorrectiveActions`, `CorrectiveActionAssignments`, `CorrectiveActionStatusHistory`, مع تصفية مبنية على الملاحظة الأصلية عبر `OperationalDashboardFilterBuilder`.
- التصعيد والتنبيهات: `EscalationOccurrences` و`Notifications` متاحة، لكن Notifications شخصية للمستخدم ولا تمثل Facility Alert normalized.
- التزام النماذج: `IFormComplianceQueryService` يقدم ملخصًا متسقًا مع لوحة الالتزام الحالية.
- Drill-down routes موجودة: `/notes/workspace`, `/corrective-actions`, `/settings/escalations/occurrences`, `/form-compliance/facilities/:facilityId`, و`/dashboard`.

## غير المتاح

- لا يوجد نموذج Facility Alert مستقل normalized؛ لذلك Widget التنبيهات سيعرض التصعيدات التشغيلية ضمن السجن والتنبيهات الشخصية غير المقروءة للمستخدم عندما ترتبط بالتصعيد فقط.
- لا يوجد Unified Operational Timeline كامل؛ Recent Activity سيكون محدودًا إلى أحداث الملاحظات والإجراءات والتصعيدات والنماذج من الجداول الحالية.
- لا توجد موارد بشرية أو معدات أو خرائط أو مخاطر مؤسسية ضمن هذا MVP.
- لا يوجد persistence لـSaved Views؛ يبقى مؤجلًا لـIssue #21.

## Widgets الممكنة دون Mock

- `facility-executive-summary`: تقييم deterministic من مؤشرات الملاحظات والإجراءات والتصعيد والنماذج.
- `facility-notes-overview`: تجميع server-side للملاحظات المفتوحة والحرجة والمتأخرة وغير المسندة والجديدة وأعلى الأنواع.
- `facility-corrective-actions`: تجميع server-side للإجراءات المفتوحة والمتأخرة وقيد التنفيذ وبانتظار التحقق والمعاد فتحها والحرجة.
- `facility-alerts-escalations`: التصعيدات المفتوحة والحرجة وتنبيهات المستخدم غير المقروءة المرتبطة بالتصعيد.
- `facility-form-compliance`: ملخص `IFormComplianceQueryService` لنفس facility.
- `facility-priority-queue`: أعلى 10 عناصر من مصادر مصرح بها ومحدودة.
- `facility-recent-activity`: آخر 10 أحداث تشغيلية غير تقنية.

## التكرار الممكن تجنبه

- استخدام `OperationalDashboardFilterBuilder` بدل إعادة بناء قواعد scope/type/sensitive للملاحظات والإجراءات.
- استخدام `IFormComplianceQueryService` بدل إعادة كتابة denominator/completion basis.
- استخدام `WorkspaceContractFactory.Envelope` و`WorkspaceShell` الحاليين بدل Shell أو registry جديد.
- استخدام routeKey mappings في `WorkspaceShell` وتوسيعها فقط.

## مخاطر أمنية وأدائية

- أي Widget غير مصرح يجب أن يختفي كليًا عبر `RequiredPermission`.
- Facility scope لا يعتمد على الواجهة؛ الخادم يفرض `facilityId` وصلاحية النطاق.
- الإجراءات التصحيحية يجب أن تبقى مشتقة من الملاحظات المصرح بها حتى لا تسرب ملاحظات حساسة أو خارج نطاق type grants.
- Notifications شخصية؛ لا يجوز عرض تنبيهات مستخدمين آخرين.
- Priority Queue وRecent Activity bounded بحد 10 ولا تحمّل كل السجلات في الذاكرة.
- بعض التجميعات تستخدم عدة queries ثابتة؛ يجب ألا يزيد العدد خطيًا مع عدد العناصر.

## حدود MVP

- Facility Workspace فقط بمفتاح `facility-operations` ودعم `WorkspaceLevel.Facility`.
- لا Region/Headquarters Workspace.
- لا AI أو prediction أو simulation.
- لا EF migration متوقعة.
- لا Saved Views persistence.
- لا تنفيذ Issue #18 timeline كامل أو Issue #19 alert center كامل.

## خريطة الملفات المتوقعة

- Backend Application:
  - `Baseera.Application/Workspaces/FacilityWorkspaceDefinitions.cs`
  - `Baseera.Application/Workspaces/FacilityWorkspaceWidgetProviders.cs`
  - `Baseera.Application/Workspaces/FacilityWorkspaceDtos.cs`
  - `Baseera.Application/Workspaces/FacilityWorkspaceRules.cs`
  - `Baseera.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- Frontend:
  - `src/api/client.ts`
  - `src/workspaces/WorkspaceShell.tsx`
  - `src/pages/workspaces/FacilityWorkspacePage.tsx`
  - `src/pages/workspaces/FacilityWorkspacePage.test.tsx`
  - `src/App.tsx`
  - `src/index.css`
- Tests:
  - `Baseera.UnitTests/Workspaces/FacilityWorkspaceTests.cs`
  - `Baseera.IntegrationTests/FacilityWorkspaceIntegrationTests.cs`
- Docs:
  - Phase D.1 scope, architecture, widget catalog, API, security, priority, freshness/confidence, performance, RTL, test matrix, completion report.

