# Phase B.2.2 Scope

تنفذ B.2.2 نواة التصعيد الآلي والإشعارات الداخلية فقط.

## Included

- DueSoon وOverdue للملاحظات التشغيلية والإجراءات التصحيحية.
- سياسات وقواعد تصعيد قابلة للتفعيل والتعطيل.
- قناة `InApp` فقط.
- Inbox، عداد غير المقروء، قراءة وأرشفة.
- Background worker قابل للتعطيل.
- تشغيل يدوي يستخدم نفس `IEscalationProcessor`.
- Idempotency عبر `OccurrenceKey` و`DeduplicationKey`.
- Lease عبر SQL Server لمنع تعدد المعالجة.
- Retry لمحاولات التسليم الداخلية.
- AuditLog للأحداث الجوهرية.

## Excluded

- Email/SMS/WhatsApp/Push/Teams.
- مزودات خارجية أو Message Broker.
- Dashboard قيادي أو تقارير أو تصدير.
- Rule Builder رسومي أو Workflow Designer.
- Phase B.2.3 أو Phase C.
