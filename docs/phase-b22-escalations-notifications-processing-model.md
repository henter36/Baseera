# Phase B.2.2 Processing Model

## Due Rules

- DueSoon: `DueAtUtc >= now AND DueAtUtc <= now + ThresholdDays`.
- Overdue: تاريخ الاستحقاق المحلي في `Asia/Riyadh` قبل تاريخ اليوم المحلي.
- Due today in Riyadh is not overdue.

## Exclusions

- الملاحظات `Closed` و`Cancelled` والمؤرشفة.
- الإجراءات `Completed` و`Cancelled` والمؤرشفة.
- السجلات دون `DueAtUtc`.

## TargetCycleKey

يدخل `TargetCycleKey` في `OccurrenceKey` و`DeduplicationKey`.

- الملاحظة: `ReopenedAtUtc ?? SubmittedAtUtc ?? CreatedAtUtc`.
- الإجراء: `ReopenedAtUtc ?? SubmittedAtUtc ?? CreatedAtUtc`.

هذا يفصل دورة العمل القديمة عن دورة إعادة الفتح.

## Idempotency

- `OccurrenceKey = Rule + Target + Level/Cycle + OccurrenceNumber`.
- `DeduplicationKey = OccurrenceKey + Recipient + InApp`.
- التشغيل اليدوي والعامل يستخدمان `IEscalationProcessor` نفسه.

## Lease

العامل يحصل على lease في `BackgroundJobLeases`. العامل الآخر لا يعالج إذا كان lease غير منتهٍ. lease المنتهي يمكن استعادته.

## Retry

كل retry ينشئ `NotificationDeliveryAttempt` جديدًا. لا يتم تحديث محاولات التسليم السابقة.
