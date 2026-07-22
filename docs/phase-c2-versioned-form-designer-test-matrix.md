# Phase C.2 Test Matrix

| Area | Coverage |
|------|----------|
| Version state machine | Unit |
| Canonical hash determinism | Unit |
| Cycles / nested repeating | Unit |
| Snapshot immutability (DbContext) | Unit |
| Formula evaluator | Unit |
| Create/save/submit/approve-lock | Integration |
| Locked update 409 | Integration |
| RowVersion 409 | Integration |
| IDOR 404 | Integration |
| Undo/Redo + preview logic | Frontend |
| Designer permission gate | Frontend |
| SQL trigger | Migration + integration against SQL |
