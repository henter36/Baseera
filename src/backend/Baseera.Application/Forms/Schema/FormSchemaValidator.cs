namespace Baseera.Application.Forms.Schema;

using System.Text.RegularExpressions;
using Baseera.Domain.Forms.Schema;

public static class FormSchemaValidator
{
    public const int MaxSchemaBytes = 1_048_576;
    public const int MaxRegexLength = 200;
    public const int CurrentSchemaFormatVersion = 1;
    public const int MaxPages = 50;
    public const int MaxSectionsPerPage = 50;
    public const int MaxFieldsPerSection = 100;
    public const int MaxOptionsPerChoice = 100;
    public const int MaxConditionNodes = 500;
    public const int MaxFormulaNodes = 500;
    public const int MaxConditionDepth = 10;
    public const int MaxRepeatingTableColumns = 50;
    public const int MaxRepeatingTableRows = 200;
    public const int MaxTextLength = 10_000;
    public const int MaxKeyLength = 80;

    private static readonly HashSet<FormConditionOperator> YesNoOps =
    [
        FormConditionOperator.IsTrue, FormConditionOperator.IsFalse,
        FormConditionOperator.Equals, FormConditionOperator.NotEquals,
        FormConditionOperator.IsEmpty, FormConditionOperator.IsNotEmpty
    ];

    private static readonly HashSet<FormConditionOperator> NumericOps =
    [
        FormConditionOperator.GreaterThan, FormConditionOperator.GreaterThanOrEqual,
        FormConditionOperator.LessThan, FormConditionOperator.LessThanOrEqual,
        FormConditionOperator.Equals, FormConditionOperator.NotEquals,
        FormConditionOperator.In, FormConditionOperator.NotIn,
        FormConditionOperator.IsEmpty, FormConditionOperator.IsNotEmpty
    ];

    private static readonly HashSet<FormConditionOperator> DateOps =
    [
        FormConditionOperator.Before, FormConditionOperator.After,
        FormConditionOperator.Equals, FormConditionOperator.NotEquals,
        FormConditionOperator.IsEmpty, FormConditionOperator.IsNotEmpty
    ];

    private static readonly HashSet<FormConditionOperator> TextChoiceOps =
    [
        FormConditionOperator.Equals, FormConditionOperator.NotEquals,
        FormConditionOperator.Contains, FormConditionOperator.NotContains,
        FormConditionOperator.In, FormConditionOperator.NotIn,
        FormConditionOperator.IsEmpty, FormConditionOperator.IsNotEmpty
    ];

    public static List<FormSchemaValidationIssue> Validate(FormSchemaDocument document, bool requireMinimumContent)
    {
        var context = new SchemaValidationContext();

        if (document.SchemaFormatVersion != CurrentSchemaFormatVersion)
        {
            context.Issues.Add(Issue("UnsupportedSchemaFormat", "$", null, null, "إصدار تنسيق المخطط غير مدعوم."));
        }

        if (document.Pages.Count > MaxPages)
        {
            context.Issues.Add(Issue("TooManyPages", "pages", null, null, $"عدد الصفحات يتجاوز الحد ({MaxPages})."));
        }

        if (requireMinimumContent)
        {
            ValidateMinimumContent(document, context.Issues);
        }

        foreach (var page in document.Pages.OrderBy(p => p.Order).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            ValidatePage(page, context);
        }

        ValidateCrossReferences(document, context);

        if (context.ConditionNodes > MaxConditionNodes)
        {
            context.Issues.Add(Issue("TooManyConditionNodes", "conditions", null, null, $"عدد عقد الشروط يتجاوز الحد ({MaxConditionNodes})."));
        }

        if (context.FormulaNodes > MaxFormulaNodes)
        {
            context.Issues.Add(Issue("TooManyFormulaNodes", "formulas", null, null, $"عدد عقد المعادلات يتجاوز الحد ({MaxFormulaNodes})."));
        }

        context.Issues.AddRange(FormDependencyGraph.DetectCyclesAndMissingRefs(document, context.FieldsByKey));
        return context.Issues;
    }

    private static void ValidateMinimumContent(FormSchemaDocument document, List<FormSchemaValidationIssue> issues)
    {
        if (document.Pages.Count == 0)
        {
            issues.Add(Issue("MissingPage", "pages", null, null, "يجب وجود صفحة واحدة واحدة على الأقل."));
        }
        else if (document.Pages.All(p => p.Sections.Count == 0))
        {
            issues.Add(Issue("MissingSection", "sections", null, null, "يجب وجود قسم واحد على الأقل."));
        }
        else if (document.Pages.SelectMany(p => p.Sections).All(s => s.Fields.Count == 0))
        {
            issues.Add(Issue("MissingField", "fields", null, null, "يجب وجود حقل واحد على الأقل."));
        }
    }

    private static void ValidatePage(FormPageSchema page, SchemaValidationContext context)
    {
        TrackId(page.Id, $"pages[{page.Key}]", context);
        TrackKey(page.Key, $"pages[{page.Key}].key", page.Id, context);
        ValidateTitle(page.TitleAr, $"pages[{page.Key}].titleAr", page.Id, page.Key, context.Issues);

        if (page.Sections.Count > MaxSectionsPerPage)
        {
            context.Issues.Add(Issue("TooManySections", $"pages[{page.Key}].sections", page.Id, page.Key, $"عدد الأقسام يتجاوز الحد ({MaxSectionsPerPage})."));
        }

        foreach (var section in page.Sections.OrderBy(s => s.Order).ThenBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            ValidateSection(section, context);
        }
    }

    private static void ValidateSection(FormSectionSchema section, SchemaValidationContext context)
    {
        TrackId(section.Id, $"sections[{section.Key}]", context);
        TrackKey(section.Key, $"sections[{section.Key}].key", section.Id, context);
        ValidateTitle(section.TitleAr, $"sections[{section.Key}].titleAr", section.Id, section.Key, context.Issues);

        if (section.Fields.Count > MaxFieldsPerSection)
        {
            context.Issues.Add(Issue("TooManyFields", $"sections[{section.Key}].fields", section.Id, section.Key, $"عدد الحقول يتجاوز الحد ({MaxFieldsPerSection})."));
        }

        foreach (var field in section.Fields.OrderBy(f => f.Order).ThenBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            ValidateField(field, $"fields[{field.Key}]", context, insideRepeating: false);
        }
    }

    private static void ValidateCrossReferences(FormSchemaDocument document, SchemaValidationContext context)
    {
        foreach (var page in document.Pages)
        {
            ValidateConditionGroup(page.VisibilityCondition, $"pages[{page.Key}].visibility", page.Id, context, deferFieldCheck: false, depth: 0);
            foreach (var section in page.Sections)
            {
                ValidateConditionGroup(section.VisibilityCondition, $"sections[{section.Key}].visibility", section.Id, context, deferFieldCheck: false, depth: 0);
                foreach (var field in section.Fields)
                {
                    ValidateFieldReferences(field, context);
                }
            }
        }
    }

    private static void ValidateFieldReferences(FormFieldSchema field, SchemaValidationContext context)
    {
        ValidateConditionGroup(field.VisibilityCondition, $"fields[{field.Key}].visibility", field.Id, context, deferFieldCheck: false, depth: 0);
        ValidateConditionGroup(field.RequiredCondition, $"fields[{field.Key}].required", field.Id, context, deferFieldCheck: false, depth: 0);
        ValidateFormulaNode(field.Formula, $"fields[{field.Key}].formula", field.Id, field.Type, context);
        if (field.RepeatingTable is null)
        {
            return;
        }

        foreach (var col in field.RepeatingTable.Columns)
        {
            var colPath = $"fields[{field.Key}].columns[{col.Key}]";
            ValidateConditionGroup(col.VisibilityCondition, $"{colPath}.visibility", col.Id, context, deferFieldCheck: false, depth: 0);
            ValidateConditionGroup(col.RequiredCondition, $"{colPath}.required", col.Id, context, deferFieldCheck: false, depth: 0);
            ValidateFormulaNode(col.Formula, $"{colPath}.formula", col.Id, col.Type, context);
        }
    }

    private static void ValidateField(
        FormFieldSchema field,
        string path,
        SchemaValidationContext context,
        bool insideRepeating)
    {
        ValidateFieldIdentity(field, path, context);
        ValidateFieldLabel(field, path, context);
        ValidateCalculatedConfiguration(field, path, context);
        ValidateTextConfiguration(field, path, context);
        ValidateChoiceConfiguration(field, path, context);

        if (field.Type == FormFieldType.RepeatingTable)
        {
            ValidateRepeatingTableConfiguration(field, path, context, insideRepeating);
        }
    }

    private static void ValidateFieldIdentity(FormFieldSchema field, string path, SchemaValidationContext context)
    {
        if (!context.Ids.Add(field.Id))
        {
            context.Issues.Add(Issue("DuplicateId", path, field.Id, field.Key, "معرّف مكرر داخل المخطط."));
        }

        if (string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > MaxKeyLength || !context.Keys.Add(field.Key))
        {
            string code;
            string message;
            if (string.IsNullOrWhiteSpace(field.Key))
            {
                code = "MissingKey";
                message = "المفتاح مطلوب.";
            }
            else if (field.Key.Length > MaxKeyLength)
            {
                code = "KeyTooLong";
                message = "المفتاح طويل جدًا.";
            }
            else
            {
                code = "DuplicateKey";
                message = "المفتاح مكرر دون اعتبار حالة الأحرف.";
            }

            context.Issues.Add(Issue(code, path + ".key", field.Id, field.Key, message));
        }
        else
        {
            context.FieldsByKey[field.Key] = field;
        }
    }

    private static void ValidateFieldLabel(FormFieldSchema field, string path, SchemaValidationContext context)
    {
        ValidateTitle(field.LabelAr, path + ".labelAr", field.Id, field.Key, context.Issues);
        ValidateTextLength(field.LabelEn, path + ".labelEn", field.Id, field.Key, context.Issues);
        ValidateTextLength(field.Description, path + ".description", field.Id, field.Key, context.Issues);
    }

    private static void ValidateCalculatedConfiguration(FormFieldSchema field, string path, SchemaValidationContext context)
    {
        var calculated = field.Type is FormFieldType.CalculatedNumber or FormFieldType.CalculatedText;
        if (field.IsCalculated != calculated)
        {
            context.Issues.Add(Issue("CalculatedFlagMismatch", path, field.Id, field.Key, "علامة الحقل المحسوب غير متوافقة مع النوع."));
        }

        if (calculated && field.Formula is null)
        {
            context.Issues.Add(Issue("MissingFormula", path + ".formula", field.Id, field.Key, "الحقل المحسوب يتطلب معادلة."));
        }
    }

    private static void ValidateTextConfiguration(FormFieldSchema field, string path, SchemaValidationContext context)
    {
        if (field.Text?.Kind != FormTextValidationKind.CustomPattern)
        {
            return;
        }

        var pattern = field.Text.CustomPattern ?? string.Empty;
        if (pattern.Length is 0 or > MaxRegexLength)
        {
            context.Issues.Add(Issue("UnsafeRegex", path + ".text.customPattern", field.Id, field.Key, "نمط التحقق غير صالح أو يتجاوز الحد الآمن."));
            return;
        }

        try
        {
            _ = Regex.IsMatch("test", pattern, RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
        }
        catch (Exception)
        {
            context.Issues.Add(Issue("UnsafeRegex", path + ".text.customPattern", field.Id, field.Key, "نمط التحقق غير آمن أو غير صالح."));
        }
    }

    private static void ValidateChoiceConfiguration(FormFieldSchema field, string path, SchemaValidationContext context)
    {
        if (field.Type is not (FormFieldType.SingleChoice or FormFieldType.MultipleChoice))
        {
            return;
        }

        if (field.Choice is null || field.Choice.Options.Count == 0)
        {
            context.Issues.Add(Issue("MissingOptions", path + ".choice", field.Id, field.Key, "خيارات الحقل مطلوبة."));
        }
        else if (field.Choice.Options.Count > MaxOptionsPerChoice)
        {
            context.Issues.Add(Issue("TooManyOptions", path + ".choice.options", field.Id, field.Key, $"عدد الخيارات يتجاوز الحد ({MaxOptionsPerChoice})."));
        }
    }

    private static void ValidateRepeatingTableConfiguration(
        FormFieldSchema field,
        string path,
        SchemaValidationContext context,
        bool insideRepeating)
    {
        if (insideRepeating)
        {
            context.Issues.Add(Issue("NestedRepeatingTable", path, field.Id, field.Key, "لا يُسمح بجدول متكرر داخل جدول متكرر."));
        }

        if (field.RepeatingTable is null)
        {
            context.Issues.Add(Issue("MissingRepeatingTable", path, field.Id, field.Key, "إعدادات الجدول المتكرر مطلوبة."));
            return;
        }

        if (field.RepeatingTable.Columns.Count > MaxRepeatingTableColumns)
        {
            context.Issues.Add(Issue("TooManyRepeatingColumns", path + ".repeatingTable.columns", field.Id, field.Key, $"عدد أعمدة الجدول يتجاوز الحد ({MaxRepeatingTableColumns})."));
        }

        if (field.RepeatingTable.MaxRows > MaxRepeatingTableRows)
        {
            context.Issues.Add(Issue("TooManyRepeatingRows", path + ".repeatingTable.maxRows", field.Id, field.Key, $"الحد الأقصى للصفوف يتجاوز ({MaxRepeatingTableRows})."));
        }

        foreach (var col in field.RepeatingTable.Columns)
        {
            ValidateField(col, path + $".columns[{col.Key}]", context, insideRepeating: true);
        }
    }

    private static void ValidateConditionGroup(
        FormConditionGroup? group,
        string path,
        Guid? entityId,
        SchemaValidationContext context,
        bool deferFieldCheck,
        int depth)
    {
        if (group is null)
        {
            return;
        }

        if (!ValidateConditionDepth(depth, path, entityId, context))
        {
            return;
        }

        context.ConditionNodes += group.Predicates.Count;
        foreach (var predicate in group.Predicates)
        {
            ValidateConditionPredicate(predicate, path, entityId, context, deferFieldCheck);
        }

        foreach (var nested in group.Groups)
        {
            ValidateConditionGroup(nested, path, entityId, context, deferFieldCheck, depth + 1);
        }
    }

    private static bool ValidateConditionDepth(int depth, string path, Guid? entityId, SchemaValidationContext context)
    {
        if (depth <= MaxConditionDepth)
        {
            return true;
        }

        context.Issues.Add(Issue("ConditionDepthExceeded", path, entityId, null, $"عمق الشروط يتجاوز الحد ({MaxConditionDepth})."));
        return false;
    }

    private static void ValidateConditionPredicate(
        FormConditionPredicate predicate,
        string path,
        Guid? entityId,
        SchemaValidationContext context,
        bool deferFieldCheck)
    {
        if (deferFieldCheck)
        {
            return;
        }

        if (!context.FieldsByKey.TryGetValue(predicate.FieldKey, out var field))
        {
            context.Issues.Add(Issue("MissingFieldReference", path, entityId, predicate.FieldKey, $"مرجع الحقل '{predicate.FieldKey}' غير موجود."));
            return;
        }

        ValidateConditionOperator(field.Type, predicate.Operator, path, entityId, predicate.FieldKey, context);
    }

    private static void ValidateConditionOperator(
        FormFieldType fieldType,
        FormConditionOperator op,
        string path,
        Guid? entityId,
        string fieldKey,
        SchemaValidationContext context)
    {
        if (!IsOperatorCompatible(fieldType, op))
        {
            context.Issues.Add(Issue("OperatorTypeMismatch", path, entityId, fieldKey, "عامل الشرط غير متوافق مع نوع الحقل."));
        }
    }

    private static void ValidateFormulaNode(
        FormFormulaNode? node,
        string path,
        Guid entityId,
        FormFieldType fieldType,
        SchemaValidationContext context)
    {
        if (node is null)
        {
            return;
        }

        context.FormulaNodes++;
        switch (node)
        {
            case FormConstantNumberNode:
            case FormConstantTextNode:
                break;
            case FormFieldReferenceNode fr:
                ValidateFormulaFieldReference(fr, path, entityId, context);
                break;
            case FormBinaryOperationNode bin:
                ValidateFormulaBinary(bin, path, entityId, fieldType, context);
                break;
            case FormFunctionCallNode fn:
                ValidateFormulaFunction(fn, path, entityId, fieldType, context);
                break;
            default:
                context.Issues.Add(Issue("UnknownFormulaNode", path, entityId, null, "عقدة معادلة غير معروفة."));
                break;
        }

        if (fieldType == FormFieldType.CalculatedText && node is FormConstantNumberNode)
        {
            return;
        }

        if (fieldType == FormFieldType.CalculatedNumber && node is FormConstantTextNode)
        {
            context.Issues.Add(Issue("FormulaResultTypeMismatch", path, entityId, null, "نوع نتيجة المعادلة غير متوافق مع نوع الحقل."));
        }
    }

    private static void ValidateFormulaFieldReference(
        FormFieldReferenceNode fr,
        string path,
        Guid entityId,
        SchemaValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(fr.FieldKey))
        {
            context.Issues.Add(Issue("MissingFormulaFieldKey", path, entityId, null, "مرجع الحقل في المعادلة مطلوب."));
        }
        else if (!context.FieldsByKey.ContainsKey(fr.FieldKey))
        {
            context.Issues.Add(Issue("MissingFieldReference", path, entityId, fr.FieldKey, $"مرجع الحقل '{fr.FieldKey}' غير موجود."));
        }
    }

    private static void ValidateFormulaBinary(
        FormBinaryOperationNode bin,
        string path,
        Guid entityId,
        FormFieldType fieldType,
        SchemaValidationContext context)
    {
        if (bin.Left is null)
        {
            context.Issues.Add(Issue("MissingFormulaOperand", path + ".left", entityId, null, "العامل الأيسر في المعادلة مطلوب."));
        }
        else
        {
            ValidateFormulaNode(bin.Left, path + ".left", entityId, fieldType, context);
        }

        if (bin.Right is null)
        {
            context.Issues.Add(Issue("MissingFormulaOperand", path + ".right", entityId, null, "العامل الأيمن في المعادلة مطلوب."));
        }
        else
        {
            ValidateFormulaNode(bin.Right, path + ".right", entityId, fieldType, context);
        }
    }

    private static void ValidateFormulaFunction(
        FormFunctionCallNode fn,
        string path,
        Guid entityId,
        FormFieldType fieldType,
        SchemaValidationContext context)
    {
        if (!Enum.IsDefined(fn.Function))
        {
            context.Issues.Add(Issue("UnknownFormulaFunction", path, entityId, null, "دالة معادلة غير مسجلة."));
        }
        else if (!HasRequiredArguments(fn))
        {
            context.Issues.Add(Issue("MissingFormulaArguments", path, entityId, null, "عدد وسائط الدالة غير كافٍ."));
        }

        foreach (var arg in fn.Arguments)
        {
            ValidateFormulaNode(arg, path, entityId, fieldType, context);
        }
    }

    private static bool HasRequiredArguments(FormFunctionCallNode fn) => fn.Function switch
    {
        FormFormulaFunction.Round => fn.Arguments.Count >= 1,
        FormFormulaFunction.Coalesce or FormFormulaFunction.Concat => fn.Arguments.Count >= 1,
        FormFormulaFunction.Min or FormFormulaFunction.Max or FormFormulaFunction.Sum
            or FormFormulaFunction.Average => fn.Arguments.Count >= 1,
        FormFormulaFunction.Floor or FormFormulaFunction.Ceiling or FormFormulaFunction.Abs => fn.Arguments.Count == 1,
        _ => fn.Arguments.Count >= 1
    };

    internal static bool IsOperatorCompatible(FormFieldType type, FormConditionOperator op)
    {
        if (!Enum.IsDefined(op))
        {
            return false;
        }

        return type switch
        {
            FormFieldType.YesNo => YesNoOps.Contains(op),
            FormFieldType.Number or FormFieldType.Percentage or FormFieldType.CalculatedNumber => NumericOps.Contains(op),
            FormFieldType.Date or FormFieldType.DateTime or FormFieldType.Time => DateOps.Contains(op),
            FormFieldType.ShortText or FormFieldType.LongText or FormFieldType.SingleChoice
                or FormFieldType.MultipleChoice or FormFieldType.CalculatedText => TextChoiceOps.Contains(op),
            _ => op is FormConditionOperator.IsEmpty or FormConditionOperator.IsNotEmpty
        };
    }

    private static void TrackId(Guid id, string path, SchemaValidationContext context)
    {
        if (!context.Ids.Add(id))
        {
            context.Issues.Add(Issue("DuplicateId", path, id, null, "معرّف مكرر داخل المخطط."));
        }
    }

    private static void TrackKey(string key, string path, Guid entityId, SchemaValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            context.Issues.Add(Issue("MissingKey", path, entityId, key, "المفتاح مطلوب."));
            return;
        }

        if (key.Length > MaxKeyLength)
        {
            context.Issues.Add(Issue("KeyTooLong", path, entityId, key, "المفتاح طويل جدًا."));
            return;
        }

        if (!context.Keys.Add(key))
        {
            context.Issues.Add(Issue("DuplicateKey", path, entityId, key, "المفتاح مكرر دون اعتبار حالة الأحرف."));
        }
    }

    private static void ValidateTitle(string? titleAr, string path, Guid entityId, string? fieldKey, List<FormSchemaValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(titleAr))
        {
            issues.Add(Issue("MissingTitleAr", path, entityId, fieldKey, "العنوان العربي مطلوب."));
            return;
        }

        ValidateTextLength(titleAr, path, entityId, fieldKey, issues);
    }

    private static void ValidateTextLength(string? value, string path, Guid entityId, string? fieldKey, List<FormSchemaValidationIssue> issues)
    {
        if (value is not null && value.Length > MaxTextLength)
        {
            issues.Add(Issue("TextTooLong", path, entityId, fieldKey, $"النص يتجاوز الحد ({MaxTextLength})."));
        }
    }

    private static FormSchemaValidationIssue Issue(
        string code, string path, Guid? entityId, string? fieldKey, string messageAr) => new()
    {
        Code = code,
        Path = path,
        EntityId = entityId,
        FieldKey = fieldKey,
        MessageAr = messageAr,
        Severity = FormSchemaValidationSeverity.Error
    };

    internal sealed class SchemaValidationContext
    {
        public List<FormSchemaValidationIssue> Issues { get; } = [];
        public HashSet<Guid> Ids { get; } = [];
        public HashSet<string> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, FormFieldSchema> FieldsByKey { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int ConditionNodes { get; set; }
        public int FormulaNodes { get; set; }
    }
}
