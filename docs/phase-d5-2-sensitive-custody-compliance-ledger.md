# Sensitive Custody Compliance Ledger

| Requirement | Status | Code evidence | Test evidence |
| ----------- | ------ | ------------- | ------------- |
| Independent weapons/sensitive custody domain | Verified | `Baseera.Domain/SensitiveCustody` | build |
| Serials protected and masked by default | Verified | `SensitiveSerialProtection`, weapon DTO projection | unit + integration redaction tests |
| Append-only custody ledger | Verified | `CustodyTransaction`, restrict deletes, transition service | unit transition tests |
| One active custody per weapon | Verified | filtered unique index in EF configuration | migration/build |
| Four-eyes approval | Verified | `EnforceFourEyes` | unit policy coverage |
| Ammunition ledger prevents negative balance | Verified | `AmmunitionLedgerPolicy` | unit test |
| Facility Workspace integration | Verified | `facility.sensitive-custody` widget/provider/frontend section | integration + frontend test |
| Server-side permissions | Verified | `SensitiveCustody.*` policies/endpoints | integration 403/404 tests |
| Audit safety | Verified | safe audit metadata only | integration audit redaction test |
| Region/HQ workspace | Not Applicable — خارج النطاق | scope document | n/a |
| Procurement/finance | Not Applicable — خارج النطاق | scope document | n/a |
| Screenshots as evidence | Not Applicable — خارج النطاق | scope document | n/a |
