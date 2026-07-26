# Sensitive Custody Test Matrix

Unit tests:

- status/readiness policy;
- custody transaction transitions;
- ammunition ledger signs and negative balance prevention;
- serial protection and masking;
- workforce eligibility policy.

Integration tests:

- missing permission returns 403;
- out-of-scope facility returns 404;
- weapon create protects serial and viewer response redacts it;
- facility workspace includes sensitive custody widget without raw sensitive fields;
- sensitive audit rows do not contain raw serial values.

Frontend tests:

- Facility Workspace renders sensitive custody section and summary without serial or armory location disclosure.
