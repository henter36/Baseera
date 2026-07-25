# Phase D.5.1 Workforce Qualifications

`WorkforceQualification` stores operational credentials on a member:

- Types: RoleCertification, Skill, License, SecurityClearance, FitnessClearance, Other.
- Status: Valid, ExpiringSoon, Expired, Suspended, PendingVerification, Unknown.
- Optional `RoleDefinitionId`, issuer/reference, issue/expiry, verification metadata, optional `AttachmentId`.

Validity for a required role (`IsQualificationValidForRole`):

- Role link must match when set.
- Expiry must be after `asOfUtc` when set.
- Status must be `Valid` or `ExpiringSoon`.

Role definitions may flag `RequiresCertification`, `RequiresActiveFitness`, `RequiresSecurityClearance`. Summary qualification coverage counts valid quals against active assignments. Fatigue policy flags nearest expiry within 30 days.

API: `POST .../workforce/qualifications` requires `Workforce.ManageQualifications`. View via member detail under `Workforce.ViewMembers`.

Institutional LMS, recruitment pipelines, and full performance reviews are not implemented.
