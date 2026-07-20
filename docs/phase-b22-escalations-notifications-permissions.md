# Phase B.2.2 Permissions

## Escalations

- `Escalations.View`
- `Escalations.Manage`
- `Escalations.Activate`
- `Escalations.Run`
- `Escalations.ViewOccurrences`
- `Escalations.RetryFailed`

## Notifications

- `Notifications.ViewOwn`
- `Notifications.MarkRead`
- `Notifications.ArchiveOwn`

## Role Seeding

- `SystemAdministrator`: جميع الصلاحيات.
- `DecisionSupportDirector`: إدارة وتشغيل التصعيد، عرض الحوادث، retry، وإشعاراته.
- `HeadquartersExecutive`, `RegionalDirector`, `FacilityDirector`: عرض التصعيد والحوادث ضمن النطاق، وإشعاراتهم.
- `RegionalCoordinator`, `FacilityCoordinator`, `Auditor`, `ReadOnlyUser`: إشعاراتهم عند المنح.

الخدمات تستخدم Permission Codes ولا تعتمد على أسماء الأدوار لاتخاذ قرار التفويض.
