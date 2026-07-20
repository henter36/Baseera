# Phase B.2.2 Domain Model

## Tables

- `EscalationPolicies`: سياسة التصعيد، Soft Delete، RowVersion، `Code` فريد.
- `EscalationRules`: مستوى تصعيد داخل سياسة، Soft Delete، RowVersion، `PolicyId + Level` فريد.
- `EscalationOccurrences`: سجل حدوث append-only، `OccurrenceKey` فريد.
- `Notifications`: إشعار داخلي لمستخدم واحد، RowVersion، `DeduplicationKey` فريد.
- `NotificationDeliveryAttempts`: سجل محاولات append-only، قناة `InApp` فقط.
- `BackgroundJobLeases`: سجل lease ذري باسم job.

## Relationships

- Policy 1:N Rules.
- Policy/Rule 1:N Occurrences.
- Occurrence 1:N Notifications.
- Notification 1:N DeliveryAttempts.
- جميع العلاقات التشغيلية تستخدم `DeleteBehavior.Restrict`.

## Constraints and Indexes

- Unique policy code.
- Unique rule level per policy.
- Unique occurrence key.
- Unique notification deduplication key.
- Unique delivery attempt number per notification/channel.
- Check constraints للنطاق في السياسات ولشكل المستلم في القواعد.
- Indexes للسياسات المفعلة، اكتشاف الاستحقاق، حالة الحدوث، صندوق المستخدم، إعادة المحاولة، وانتهاء lease.

## Target Scope

نطاق الإجراءات التصحيحية يُستمد من الملاحظة الأصلية. لا تقبل API نطاقًا من العميل عند إنشاء occurrence أو notification.
