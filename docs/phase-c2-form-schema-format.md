# Form Schema Format v1

CamelCase JSON. Root:
```json
{ "schemaFormatVersion": 1, "pages": [ /* FormPageSchema */ ] }
```
Pages contain sections; sections contain fields. Field types 0–17 as documented in Issue #46.
Conditions and formulas use nested typed nodes with discriminators (`kind` for formulas).
Server canonicalizes order (Order, then Key) and computes SHA-256; client hashes are ignored.
