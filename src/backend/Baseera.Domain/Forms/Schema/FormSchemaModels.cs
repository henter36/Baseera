namespace Baseera.Domain.Forms.Schema;

using System.Text.Json.Serialization;
using Baseera.Domain.Attachments;

// Numeric values are part of the immutable schema contract; append only.
public enum FormFieldType
{
    ShortText = 0,
    LongText = 1,
    Number = 2,
    Percentage = 3,
    Date = 4,
    Time = 5,
    DateTime = 6,
    SingleChoice = 7,
    MultipleChoice = 8,
    YesNo = 9,
    File = 10,
    Image = 11,
    Signature = 12,
    Location = 13,
    RepeatingTable = 14,
    OrganizationalReference = 15,
    CalculatedNumber = 16,
    CalculatedText = 17
}

public enum FormLayoutWidth
{
    Full = 0,
    Half = 1,
    Third = 2,
    Quarter = 3
}

public enum FormTextValidationKind
{
    None = 0,
    Email = 1,
    Phone = 2,
    Url = 3,
    CustomPattern = 4
}

public enum FormOrganizationalReferenceKind
{
    Region = 0,
    Facility = 1,
    FacilityUnit = 2,
    Department = 3
}

public enum FormConditionCombinator
{
    All = 0,
    Any = 1
}

public enum FormConditionOperator
{
    Equals = 0,
    NotEquals = 1,
    GreaterThan = 2,
    GreaterThanOrEqual = 3,
    LessThan = 4,
    LessThanOrEqual = 5,
    Contains = 6,
    NotContains = 7,
    IsEmpty = 8,
    IsNotEmpty = 9,
    IsTrue = 10,
    IsFalse = 11,
    In = 12,
    NotIn = 13,
    Before = 14,
    After = 15
}

public enum FormFormulaBinaryOperator
{
    Add = 0,
    Subtract = 1,
    Multiply = 2,
    Divide = 3,
    Modulo = 4
}

public enum FormFormulaFunction
{
    Min = 0,
    Max = 1,
    Sum = 2,
    Average = 3,
    Round = 4,
    Floor = 5,
    Ceiling = 6,
    Abs = 7,
    Coalesce = 8,
    Concat = 9
}

public enum FormSchemaValidationSeverity
{
    Error = 0,
    Warning = 1
}

public sealed class FormSchemaDocument
{
    public int SchemaFormatVersion { get; set; } = 1;
    public List<FormPageSchema> Pages { get; set; } = [];
}

public sealed class FormPageSchema
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public FormConditionGroup? VisibilityCondition { get; set; }
    public List<FormSectionSchema> Sections { get; set; } = [];
}

public sealed class FormSectionSchema
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public FormConditionGroup? VisibilityCondition { get; set; }
    public List<FormFieldSchema> Fields { get; set; } = [];
}

public sealed class FormFieldSchema
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public FormFieldType Type { get; set; }
    public string LabelAr { get; set; } = string.Empty;
    public string? LabelEn { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public string? Placeholder { get; set; }
    public int Order { get; set; }
    public FormLayoutWidth LayoutWidth { get; set; } = FormLayoutWidth.Full;
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public FormConditionGroup? VisibilityCondition { get; set; }
    public FormConditionGroup? RequiredCondition { get; set; }
    public List<FormValidationRule> ValidationRules { get; set; } = [];
    public ClassificationLevel? ClassificationOverride { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsCalculated { get; set; }
    public FormTextFieldSettings? Text { get; set; }
    public FormNumberFieldSettings? Number { get; set; }
    public FormChoiceFieldSettings? Choice { get; set; }
    public FormFileFieldSettings? File { get; set; }
    public FormRepeatingTableSettings? RepeatingTable { get; set; }
    public FormOrganizationalReferenceSettings? OrganizationalReference { get; set; }
    public FormFormulaNode? Formula { get; set; }
}

public sealed class FormTextFieldSettings
{
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public FormTextValidationKind Kind { get; set; } = FormTextValidationKind.None;
    public string? CustomPattern { get; set; }
}

public sealed class FormNumberFieldSettings
{
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public int? DecimalPlaces { get; set; }
    public decimal? Step { get; set; }
    public string? Unit { get; set; }
}

public sealed class FormChoiceFieldSettings
{
    public List<FormFieldOption> Options { get; set; } = [];
    public bool AllowOther { get; set; }
    public int? MinSelections { get; set; }
    public int? MaxSelections { get; set; }
}

public sealed class FormFieldOption
{
    public string Value { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public string? LabelEn { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class FormFileFieldSettings
{
    public int MaxFiles { get; set; } = 1;
    public long MaxFileSizeBytes { get; set; } = 5_000_000;
    public List<string> AllowedMimeTypes { get; set; } = [];
    public List<string> AllowedExtensions { get; set; } = [];
    public bool RequireVirusScan { get; set; } = true;
}

public sealed class FormRepeatingTableSettings
{
    public int MinRows { get; set; }
    public int MaxRows { get; set; } = 20;
    public List<FormFieldSchema> Columns { get; set; } = [];
}

public sealed class FormOrganizationalReferenceSettings
{
    public FormOrganizationalReferenceKind Kind { get; set; }
}

public sealed class FormValidationRule
{
    public string Code { get; set; } = string.Empty;
    public string MessageAr { get; set; } = string.Empty;
    public string? MessageEn { get; set; }
}

public sealed class FormConditionGroup
{
    public FormConditionCombinator Combinator { get; set; } = FormConditionCombinator.All;
    public List<FormConditionPredicate> Predicates { get; set; } = [];
    public List<FormConditionGroup> Groups { get; set; } = [];
}

public sealed class FormConditionPredicate
{
    public string FieldKey { get; set; } = string.Empty;
    public FormConditionOperator Operator { get; set; }
    public string? Value { get; set; }
    public List<string>? Values { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(FormConstantNumberNode), "constantNumber")]
[JsonDerivedType(typeof(FormConstantTextNode), "constantText")]
[JsonDerivedType(typeof(FormFieldReferenceNode), "fieldReference")]
[JsonDerivedType(typeof(FormBinaryOperationNode), "binary")]
[JsonDerivedType(typeof(FormFunctionCallNode), "function")]
public abstract class FormFormulaNode
{
}

public sealed class FormConstantNumberNode : FormFormulaNode
{
    public decimal Value { get; set; }
}

public sealed class FormConstantTextNode : FormFormulaNode
{
    public string Value { get; set; } = string.Empty;
}

public sealed class FormFieldReferenceNode : FormFormulaNode
{
    public string FieldKey { get; set; } = string.Empty;
}

public sealed class FormBinaryOperationNode : FormFormulaNode
{
    public FormFormulaBinaryOperator Operator { get; set; }
    public FormFormulaNode Left { get; set; } = null!;
    public FormFormulaNode Right { get; set; } = null!;
}

public sealed class FormFunctionCallNode : FormFormulaNode
{
    public FormFormulaFunction Function { get; set; }
    public List<FormFormulaNode> Arguments { get; set; } = [];
}

public sealed class FormSchemaValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? FieldKey { get; set; }
    public string MessageAr { get; set; } = string.Empty;
    public FormSchemaValidationSeverity Severity { get; set; } = FormSchemaValidationSeverity.Error;
}

public sealed class FormSchemaCanonicalResult
{
    public required FormSchemaDocument Document { get; init; }
    public required string CanonicalJson { get; init; }
    public required string SchemaHash { get; init; }
    public required int SchemaSizeBytes { get; init; }
    public required int PageCount { get; init; }
    public required int SectionCount { get; init; }
    public required int FieldCount { get; init; }
    public required int CalculatedFieldCount { get; init; }
    public required int ConditionCount { get; init; }
    public required IReadOnlyList<FormSchemaValidationIssue> Issues { get; init; }
    public bool IsValid => Issues.All(i => i.Severity != FormSchemaValidationSeverity.Error);
}
