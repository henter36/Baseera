namespace Baseera.Domain.Forms;

using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;

public enum FormResponseStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Returned = 3,
    Approved = 4,
    Rejected = 5,
    Closed = 6
}

public enum FormAssignmentWorkStatus
{
    NotStarted = 0,
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    Approved = 5,
    Rejected = 6,
    Closed = 7,
    Overdue = 8
}

public enum FormCompletionBasis
{
    Submitted = 0,
    Approved = 1
}

public enum FormReviewMode
{
    None = 0,
    SingleLevel = 1,
    MultiLevel = 2
}

public enum FormResponseReviewDecisionType
{
    StartReview = 0,
    Return = 1,
    Approve = 2,
    Reject = 3,
    Close = 4
}

public class FormCampaignResponsePolicy : EntityBase
{
    public Guid CampaignId { get; set; }
    public FormCampaign Campaign { get; set; } = null!;
    public FormCompletionBasis CompletionBasis { get; set; } = FormCompletionBasis.Submitted;
    public FormReviewMode ReviewMode { get; set; } = FormReviewMode.None;
    public int RequiredApprovalLevels { get; set; }
    public bool AllowLateSubmission { get; set; } = true;
    public bool AllowResubmissionAfterReturn { get; set; } = true;
    public bool RequireSubmissionAcknowledgement { get; set; }
    public bool RequireSeparationOfDuties { get; set; } = true;
}

public class FormResponse : EntityBase
{
    public Guid AssignmentId { get; set; }
    public FormFacilityAssignment Assignment { get; set; } = null!;
    public Guid CampaignId { get; set; }
    public FormCampaign Campaign { get; set; } = null!;
    public Guid CycleId { get; set; }
    public FormCycle Cycle { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid FormSchemaSnapshotId { get; set; }
    public FormSchemaSnapshot FormSchemaSnapshot { get; set; } = null!;
    public string SchemaHash { get; set; } = string.Empty;
    public FormResponseStatus Status { get; set; } = FormResponseStatus.Draft;
    public string DraftAnswersJson { get; set; } = "{}";
    public string DraftAnswersHash { get; set; } = string.Empty;
    public int DraftVersion { get; set; }
    public int CurrentSubmissionNumber { get; set; }
    public int CurrentReviewLevel { get; set; }
    public DateTimeOffset? FirstStartedAtUtc { get; set; }
    public DateTimeOffset? LastSavedAtUtc { get; set; }
    public Guid? LastSavedByUserId { get; set; }
    public User? LastSavedByUser { get; set; }
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public User? SubmittedByUser { get; set; }
    public DateTimeOffset? ReturnedAtUtc { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public DateTimeOffset? RejectedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public DateTimeOffset? DueAtUtcOverride { get; set; }
    public ICollection<FormResponseSubmission> Submissions { get; set; } = new List<FormResponseSubmission>();
    public ICollection<FormResponseReviewDecision> ReviewDecisions { get; set; } = new List<FormResponseReviewDecision>();
    public ICollection<FormResponseReviewComment> ReviewComments { get; set; } = new List<FormResponseReviewComment>();
    public ICollection<FormResponseMutation> Mutations { get; set; } = new List<FormResponseMutation>();
    public ICollection<FormResponseHistory> History { get; set; } = new List<FormResponseHistory>();
}

public class FormResponseSubmission : EntityBase
{
    public Guid ResponseId { get; set; }
    public FormResponse Response { get; set; } = null!;
    public int SubmissionNumber { get; set; }
    public Guid FormSchemaSnapshotId { get; set; }
    public FormSchemaSnapshot FormSchemaSnapshot { get; set; } = null!;
    public string SchemaHash { get; set; } = string.Empty;
    public string CanonicalAnswersJson { get; set; } = "{}";
    public string AnswersHash { get; set; } = string.Empty;
    public Guid SubmittedByUserId { get; set; }
    public User SubmittedByUser { get; set; } = null!;
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public bool WasLateAtSubmission { get; set; }
    public DateTimeOffset EffectiveDueAtSubmissionUtc { get; set; }
    public bool Acknowledged { get; set; }
    public string? AcknowledgementText { get; set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public ICollection<FormResponseReviewDecision> ReviewDecisions { get; set; } = new List<FormResponseReviewDecision>();
    public ICollection<FormResponseReviewComment> ReviewComments { get; set; } = new List<FormResponseReviewComment>();
}

public class FormResponseReviewDecision : EntityBase
{
    public Guid ResponseId { get; set; }
    public FormResponse Response { get; set; } = null!;
    public Guid SubmissionId { get; set; }
    public FormResponseSubmission Submission { get; set; } = null!;
    public int ReviewLevel { get; set; }
    public FormResponseReviewDecisionType Decision { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? NewDueAtUtc { get; set; }
    public Guid ReviewedByUserId { get; set; }
    public User ReviewedByUser { get; set; } = null!;
    public DateTimeOffset ReviewedAtUtc { get; set; }
    public FormResponseStatus FromStatus { get; set; }
    public FormResponseStatus ToStatus { get; set; }
    public ICollection<FormResponseReviewComment> Comments { get; set; } = new List<FormResponseReviewComment>();
}

public class FormResponseReviewComment : EntityBase
{
    public Guid ResponseId { get; set; }
    public FormResponse Response { get; set; } = null!;
    public Guid SubmissionId { get; set; }
    public FormResponseSubmission Submission { get; set; } = null!;
    public Guid? ReviewDecisionId { get; set; }
    public FormResponseReviewDecision? ReviewDecision { get; set; }
    public string? FieldKey { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsVisibleToRespondent { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}

public class FormResponseMutation : EntityBase
{
    public Guid ResponseId { get; set; }
    public FormResponse Response { get; set; } = null!;
    public Guid ClientMutationId { get; set; }
    public int AppliedDraftVersion { get; set; }
    public DateTimeOffset AppliedAtUtc { get; set; }
    public string ResultPayloadJson { get; set; } = "{}";
}

public class FormResponseHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResponseId { get; set; }
    public FormResponse Response { get; set; } = null!;
    public string EventType { get; set; } = string.Empty;
    public FormResponseStatus? FromStatus { get; set; }
    public FormResponseStatus? ToStatus { get; set; }
    public int? SubmissionNumber { get; set; }
    public int? ReviewLevel { get; set; }
    public string? Reason { get; set; }
    public Guid ActorUserId { get; set; }
    public User ActorUser { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? MetadataJson { get; set; }
}
