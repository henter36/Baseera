# Phase C.4 — Validation

`IFormResponseValidator` validates against pinned snapshot:
text/number/choice/date/boolean/attachments/org-ref/repeating tables/conditions/calculated AST (FormFormulaEvaluator). Draft allows partial; submit requires full validity. Hidden fields not required. Canonical JSON + SHA-256 on submit.
