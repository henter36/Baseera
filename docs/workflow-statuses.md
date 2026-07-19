# قاموس حالات سير العمل

مرجع موحّد للحالات. التخزين عبر جداول مرجعية أو تعدادات نطاقية؛ العرض بالعربية في الواجهة.

## الملاحظات (Notes) — مرحلة B

`Draft` → `New` → `UnderVerification` → `Confirmed` → `Assigned` → `InProgress` → `AwaitingVerification` → `ReturnedForCorrection` → `Closed`  
بدائل: `CancelledAsDuplicate`, `OutsideJurisdiction`, `Escalated`

**قاعدة:** الحرجة لا تُغلق دون دليل؛ الجهة المعالجة لا تعتمد الإغلاق النهائي منفردة.

## النماذج — الاستجابات (Forms) — مرحلة C

`NotOpened`, `Opened`, `Draft`, `PartiallyComplete`, `Submitted`, `Returned`, `Approved`, `Overdue`, `Exempt`, `NotApplicable`

## المشاريع — مرحلة E

`Proposed`, `UnderStudy`, `Approved`, `Planned`, `InProgress`, `AtRisk`, `Delayed`, `Suspended`, `Completed`, `Closed`, `Cancelled`

## الوقائع (Incidents) — مرحلة D

`InitialReport`, `Confirmed`, `ImmediateResponse`, `Escalated`, `UnderInvestigation`, `CorrectiveActionsOpen`, `UnderReview`, `Closed`

## الخطط — مرحلة D

`Draft`, `UnderReview`, `Approved`, `Published`, `Superseded`, `Archived`

## القرارات والتكليفات — مرحلة G

قرار: `Draft`, `UnderLegalReview`, `PendingApproval`, `Approved`, `Rejected`, `Superseded`  
تكليف: `Assigned`, `InProgress`, `AwaitingEvidence`, `Completed`, `Verified`, `Overdue`, `Escalated`, `Cancelled`

## التسليح — العهد — مرحلة F

`Draft`, `PendingApproval`, `Approved`, `Rejected`, `Cancelled`

## الكيانات المشتركة (مرحلة A)

| الكيان | حالات الأرشفة |
|--------|----------------|
| Organization / Region / Facility / … | `Active`, `Inactive` + Soft Delete |
| User | `Active`, `Disabled` |
| Attachment | `PendingScan`, `Clean`, `Quarantined`, `Rejected` |
| AuditLog | غير قابل للتعديل (append-only) |
