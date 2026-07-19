# Phase B.1 — API Contract

Base path: `/api/v1/notes`  
Auth: Bearer (Entra) or TestAuth headers in Development only.  
Errors: ProblemDetails; concurrency / invalid transition → **409**; missing/out-of-scope → **404**; in-scope forbidden → **403**.

## Endpoints

| Method | Path | Permission | Body |
|--------|------|------------|------|
| GET | `/api/v1/notes` | Notes.View | — query filters |
| GET | `/api/v1/notes/{id}` | Notes.View | — |
| POST | `/api/v1/notes` | Notes.Create | `CreateNoteRequest` |
| PUT | `/api/v1/notes/{id}` | Notes.Update | `UpdateNoteRequest` |
| POST | `/api/v1/notes/{id}/submit` | Notes.Update | `TransitionNoteRequest` |
| POST | `/api/v1/notes/{id}/assign` | Notes.Assign | `AssignNoteRequest` |
| POST | `/api/v1/notes/{id}/start-work` | Notes.StartWork | `WorkflowActionRequest` |
| POST | `/api/v1/notes/{id}/submit-for-verification` | Notes.SubmitForVerification | `WorkflowActionRequest` |
| POST | `/api/v1/notes/{id}/return-for-rework` | Notes.ReturnForRework | `TransitionNoteRequest` |
| POST | `/api/v1/notes/{id}/verify-closure` | Notes.VerifyClosure | `CloseNoteRequest` |
| POST | `/api/v1/notes/{id}/reopen` | Notes.Reopen | `ReopenNoteRequest` |
| POST | `/api/v1/notes/{id}/cancel` | Notes.Cancel | `TransitionNoteRequest` |
| POST | `/api/v1/notes/{id}/archive` | Notes.Archive | `TransitionNoteRequest` |
| POST | `/api/v1/notes/{id}/restore` | Notes.Restore | `TransitionNoteRequest` |
| GET | `/api/v1/notes/{id}/history` | Notes.View | — |
| GET | `/api/v1/notes/{id}/assignments` | Notes.View | — |
| GET | `/api/v1/notes/{id}/attachments` | Notes.View | — metadata only |

### Supporting org lookups (for forms)

| Method | Path | Permission |
|--------|------|------------|
| GET | `/api/v1/facility-units?facilityId=` | Organization.View |
| GET | `/api/v1/departments` | Organization.View |

Attachments upload/download remain under `/api/v1/attachments` with entity type `OperationalNote`.

## List query parameters

`search`, `status`, `severity`, `category`, `sourceType`, `classification`, `regionId`, `facilityId`, `facilityUnitId`, `ownerDepartmentId`, `assignedToUserId`, `overdueOnly`, `dueFrom`, `dueTo`, `createdFrom`, `createdTo`, `page`, `pageSize` (max 200), `sortBy` (allowlist), `sortDesc`.

### Sort allowlist

`createdAtUtc`, `dueAtUtc`, `severity`, `status`, `referenceNumber`, `title`

## Enums (numeric JSON)

See Domain `NoteStatus`, `NoteSeverity`, `NoteCategory`, `NoteSourceType`, `ClassificationLevel`, `ScopeType`.

## DTOs

Response shapes: `NoteListItemDto`, `NoteDetailDto`, `NoteAssignmentDto`, `NoteStatusHistoryDto`, `AttachmentDto`.  
Requests validated with FluentValidation (trim, lengths, XOR assignment, reasons, rowVersion).
