# Phase C.4 — Domain

## Persisted status
`FormResponseStatus`: Draft, Submitted, UnderReview, Returned, Approved, Rejected, Closed

## Derived work status
`FormAssignmentWorkStatus` includes NotStarted and Overdue. Overdue is never persisted; computed from due + completion basis + current status. DTOs expose both `ResponseStatus` and `WorkStatus` plus `IsOverdue`/`IsCompleted`.

## Policy
`FormCampaignResponsePolicy` 1:1 with campaign. Mutable only while campaign Draft; immutable after publish. Existing campaigns backfilled to Submitted/None/levels=0/late+resubmit allowed.

## Entities
FormResponse, FormResponseSubmission (immutable), FormResponseReviewDecision, FormResponseReviewComment, FormResponseMutation, FormResponseHistory
