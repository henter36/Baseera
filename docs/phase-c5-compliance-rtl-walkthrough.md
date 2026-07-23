# Phase C.5 — RTL Walkthrough

Manual walkthrough status: implementation prepared; full browser walkthrough still requires seeded C.5 data and authenticated HQ/region/facility users in the local environment.

Checklist:

1. Open `/form-compliance` as HQ admin.
2. Verify summary cards and generated timestamp.
3. Select a region and confirm URL query changes.
4. Open `/form-compliance/regions/:regionId`.
5. Open `/form-compliance/facilities/:facilityId`.
6. Open `/form-compliance/cycles/:cycleId`.
7. Review pending list and response/review/history links.
8. Apply overdue filter.
9. Change completion policy filter.
10. Verify zero denominator shows `—`.
11. Verify unavailable count is visible in summary.
12. Verify null responsible user displays `غير محدد`.
13. Repeat with region-scoped user.
14. Repeat with facility-scoped user.
15. Export CSV and compare visible values.
16. Check Arabic RTL rendering on mobile width.

Observed automated RTL checks:

- The page root uses `dir="rtl"`.
- Arabic labels are rendered for filters, cards, tables, and help text.
- The CSV export button is hidden without export permission.
