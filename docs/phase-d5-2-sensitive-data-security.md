# Sensitive Data Security

Sensitive custody data is protected by design:

- raw serial numbers are not stored in `WeaponAsset`;
- a protected serial field and `SerialNumberHash` are stored separately;
- list/workspace/timeline/audit payloads use masked serials or entity references;
- full serial exposure is behind `SensitiveCustody.ViewSerialNumbers`;
- armory location details are behind `SensitiveCustody.ViewArmoryLocations`;
- no serials in URLs, audit payloads, logs, or frontend workspace state;
- export remains a separate permission and is not enabled as an unbounded endpoint in this slice.

Regression coverage: `Sensitive_audit_does_not_store_raw_serial`, workspace redaction test, and serial masking unit test.
