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
        var issues = new List<FormSchemaValidationIssue>();
        if (document.SchemaFormatVersion != CurrentSchemaFormatVersion)
        {
            issues.Add(Issue("UnsupportedSchemaFormat", "$", null, null, "إصدار تنسيق المخطط غير مدعوم."));
        }

        if (document.Pages.Count > MaxPages)
        {
            issues.Add(Issue("TooManyPages", "pages", null, null, $"عدد الصفحات يتجاوز الحد ({MaxPages})."));
        }

        var ids = new HashSet<Guid>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldsByKey = new Dictionary<string, FormFieldSchema>(StringComparer.OrdinalIgnoreCase);
        var conditionNodes = 0;
        var formulaNodes = 0;

        if (requireMinimumContent)
        {
            ValidateMinimumContent(document, issues);
        }

        foreach (var page in document.Pages.OrderBy(p => p.Order).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            ValidatePage(page, issues, ids, keys, fieldsByKey, ref conditionNodes, ref formulaNodes);
        }

        ValidateCrossReferences(document, fieldsByKey, issues, ref conditionNodes, ref formulaNodes);

        if (conditionNodes > MaxConditionNodes)
        {
            issues.Add(Issue("TooManyConditionNodes", "conditions", null, null, $"عدد عقد الشروط يتجاوز الحد ({MaxConditionNodes})."));
        }

        if (formulaNodes > MaxFormulaNodes)
        {
            issues.Add(Issue("TooManyFormulaNodes", "formulas", null, null, $"عدد عقد المعادلات يتجاوز الحد ({MaxFormulaNodes})."));
        }

        issues.AddRange(FormDependencyGraph.DetectCyclesAndMissingRefs(document, fieldsByKey));
        return issues;
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

    private static void ValidatePage(
        FormPageSchema page,
        List<FormSchemaValidationIssue> issues,
        HashSet<Guid> ids,
        HashSet<string> keys,
        Dictionary<string, FormFieldSchema> fieldsByKey,
        ref int conditionNodes,
        ref int formulaNodes)
    {
        TrackId(page.Id, $"pages[{page.Key}]", ids, issues);
        TrackKey(page.Key, $"pages[{page.Key}].key", page.Id, keys, issues);
        ValidateTitle(page.TitleAr, $"pages[{page.Key}].titleAr", page.Id, page.Key, issues);
        ValidateCondition(page.VisibilityCondition, $"pages[{page.Key}].visibility", page.Id, fieldsByKey, issues, deferFieldCheck: true, depth: 0, ref conditionNodes);

        if (page.Sections.Count > MaxSectionsPerPage)
        {
            issues.Add(Issue("TooManySections", $"pages[{page.Key}].sections", page.Id, page.Key, $"عدد الأقسام يتجاوز الحد ({MaxSectionsPerPage})."));
        }

        foreach (var section in page.Sections.OrderBy(s => s.Order).ThenBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            ValidateSection(section, page.Key, issues, ids, keys, fieldsByKey, ref conditionNodes, ref formulaNodes);
        }
    }

    private static void ValidateSection(
        FormSectionSchema section,
        string pageKey,
        List<FormSchemaValidationIssue> issues,
        HashSet<Guid> ids,
        HashSet<string> keys,
        Dictionary<string, FormFieldSchema> fieldsByKey,
        ref int conditionNodes,
        ref int formulaNodes)
    {
        TrackId(section.Id, $"sections[{section.Key}]", ids, issues);
        TrackKey(section.Key, $"sections[{section.Key}].key", section.Id, keys, issues);
        ValidateTitle(section.TitleAr, $"sections[{section.Key}].titleAr", section.Id, section.Key, issues);
        ValidateCondition(section.VisibilityCondition, $"sections[{section.Key}].visibility", section.Id, fieldsByKey, issues, deferFieldCheck: true, depth: 0, ref conditionNodes);

        if (section.Fields.Count > MaxFieldsPerSection)
        {
            issues.Add(Issue("TooManyFields", $"sections[{section.Key}].fields", section.Id, section.Key, $"عدد الحقول يتجاوز الحد ({MaxFieldsPerSection})."));
        }

        foreach (var field in section.Fields.OrderBy(f => f.Order).ThenBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            ValidateField(field, $"fields[{field.Key}]", issues, ids, keys, fieldsByKey, insideRepeating: false, ref conditionNodes, ref formulaNodes);
        }
    }

    private static void ValidateCrossReferences(
        FormSchemaDocument document,
        Dictionary<string, FormFieldSchema> fieldsByKey,
        List<FormSchemaValidationIssue> issues,
        ref int conditionNodes,
        ref int formulaNodes)
    {
        foreach (var page in document.Pages)
        {
            ValidateCondition(page.VisibilityCondition, $"pages[{page.Key}].visibility", page.Id, fieldsByKey, issues, deferFieldCheck: false, depth: 0, ref conditionNodes);
            foreach (var section in page.Sections)
            {
                ValidateCondition(section.VisibilityCondition, $"sections[{section.Key}].visibility", section.Id, fieldsByKey, issues, deferFieldCheck: false, depth: 0, ref conditionNodes);
                foreach (var field in section.Fields)
                {
                    ValidateFieldReferences(field, fieldsByKey, issues, ref conditionNodes, ref formulaNodes);
                }
            }
        }
    }

    private static void ValidateFieldReferences(
        FormFieldSchema field,
        Dictionary<string, FormFieldSchema> fieldsByKey,
        List<FormSchemaValidationIssue> issues,
        ref int conditionNodes,
        ref int formulaNodes)
    {
        ValidateCondition(field.VisibilityCondition, $"fields[{field.Key}].visibility", field.Id, fieldsByKey, issues, deferFieldCheck: false, depth: 0, ref conditionNodes);
        ValidateCondition(field.RequiredCondition, $"fields[{field.Key}].required", field.Id, fieldsByKey, issues, deferFieldCheck: false, depth: 0, ref conditionNodes);
        ValidateFormula(field.Formula, $"fields[{field.Key}].formula", field.Id, field.Type, fieldsByKey, issues, ref formulaNodes);
        if (field.RepeatingTable is null)
        {
            return;
        }

        foreach (var col in field.RepeatingTable.Columns)
        {
            var colPath = $"fields[{field.Key}].columns[{col.Key}]";
            ValidateCondition(col.VisibilityCondition, $"{colPath}.visibility", col.Id, fieldsByKey, issues, deferFieldCheck: false, depth: 0, ref conditionNodes);
            ValidateCondition(col.RequiredCondition, $"{colPath}.required", col.Id, fieldsByKey, issues, deferFieldCheck: false, depth: 0, ref conditionNodes);
            ValidateFormula(col.Formula, $"{colPath}.formula", col.Id, col.Type, fieldsByKey, issues, ref formulaNodes);
        }
    }

    private static void ValidateField(
        FormFieldSchema field,
        string path,
        List<FormSchemaValidationIssue> issues,
        HashSet<Guid> ids,
        HashSet<string> keys,
        Dictionary<string, FormFieldSchema> fieldsByKey,
        bool insideRepeating,
        ref int conditionNodes,
        ref int formulaNodes)
    {
        if (!ids.Add(field.Id))
        {
            issues.Add(Issue("DuplicateId", path, field.Id, field.Key, "معرّف مكرر داخل المخطط."));
        }

        if (string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > MaxKeyLength || !keys.Add(field.Key))
        {
            issues.Add(Issue(
                string.IsNullOrWhiteSpace(field.Key) ? "MissingKey" : field.Key.Length > MaxKeyLength ? "KeyTooLong" : "DuplicateKey",
                path + ".key",
                field.Id,
                field.Key,
                string.IsNullOrWhiteSpace(field.Key) ? "المفتاح مطلوب." : field.Key.Length > MaxKeyLength ? "المفتاح طويل جدًا." : "المفتاح مكرر دون اعتبار حالة الأحرف."));
        }
        else
        {
            fieldsByKey[field.Key] = field;
        }

        ValidateTitle(field.LabelAr, path + ".labelAr", field.Id, field.Key, issues);
        ValidateTextLength(field.LabelEn, path + ".labelEn", field.Id, field.Key, issues);
        ValidateTextLength(field.Description, path + ".description", field.Id, field.Key, issues);

        var calculated = field.Type is FormFieldType.CalculatedNumber or FormFieldType.CalculatedText;
        if (field.IsCalculated != calculated)
        {
            issues.Add(Issue("CalculatedFlagMismatch", path, field.Id, field.Key, "علامة الحقل المحسوب غير متوافقة مع النوع."));
        }

        if (calculated && field.Formula is null)
        {
            issues.Add(Issue("MissingFormula", path + ".formula", field.Id, field.Key, "الحقل المحسوب يتطلب معادلة."));
        }

        ValidateRegex(field, path, issues);

        if (field.Type is FormFieldType.SingleChoice or FormFieldType.MultipleChoice)
        {
            if (field.Choice is null || field.Choice.Options.Count == 0)
            {
                issues.Add(Issue("MissingOptions", path + ".choice", field.Id, field.Key, "خيارات الحقل مطلوبة."));
            }
            else if (field.Choice.Options.Count > MaxOptionsPerChoice)
            {
                issues.Add(Issue("TooManyOptions", path + ".choice.options", field.Id, field.Key, $"عدد الخيارات يتجاوز الحد ({MaxOptionsPerChoice})."));
            }
        }

        if (field.Type == FormFieldType.RepeatingTable)
        {
            ValidateRepeatingTable(field, path, issues, ids, keys, fieldsByKey, insideRepeating, ref conditionNodes, ref formulaNodes);
        }
    }

    private static void ValidateRepeatingTable(
        FormFieldSchema field,
        string path,
        List<FormSchemaValidationIssue> issues,
        HashSet<Guid> ids,
        HashSet<string> keys,
        Dictionary<string, FormFieldSchema> fieldsByKey,
        bool insideRepeating,
        ref int conditionNodes,
        ref int formulaNodes)
    {
        if (insideRepeating)
        {
            issues.Add(Issue("NestedRepeatingTable", path, field.Id, field.Key, "لا يُسمح بجدول متكرر داخل جدول متكرر."));
        }

        if (field.RepeatingTable is null)
        {
            issues.Add(Issue("MissingRepeatingTable", path, field.Id, field.Key, "إعدادات الجدول المتكرر مطلوبة."));
            return;
        }

        if (field.RepeatingTable.Columns.Count > MaxRepeatingTableColumns)
        {
            issues.Add(Issue("TooManyRepeatingColumns", path + ".repeatingTable.columns", field.Id, field.Key, $"عدد أعمدة الجدول يتجاوز الحد ({MaxRepeatingTableColumns})."));
        }

        if (field.RepeatingTable.MaxRows > MaxRepeatingTableRows)
        {
            issues.Add(Issue("TooManyRepeatingRows", path + ".repeatingTable.maxRows", field.Id, field.Key, $"الحد الأقصى للصفوف يتجاوز ({MaxRepeatingTableRows})."));
        }

        foreach (var col in field.RepeatingTable.Columns)
        {
            ValidateField(col, path + $".columns[{col.Key}]", issues, ids, keys, fieldsByKey, insideRepeating: true, ref conditionNodes, ref formulaNodes);
        }
    }

    private static void ValidateRegex(FormFieldSchema field, string path, List<FormSchemaValidationIssue> issues)
    {
        if (field.Text?.Kind != FormTextValidationKind.CustomPattern)
        {
            return;
        }

        var pattern = field.Text.CustomPattern ?? string.Empty;
        if (pattern.Length is 0 or > MaxRegexLength)
        {
            issues.Add(Issue("UnsafeRegex", path + ".text.customPattern", field.Id, field.Key, "نمط التحقق غير صالح أو يتجاوز الحد الآمن."));
            return;
        }

        try
        {
            _ = Regex.IsMatch("test", pattern, RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
        }
        catch (Exception)
        {
            issues.Add(Issue("UnsafeRegex", path + ".text.customPattern", field.Id, field.Key, "نمط التحقق غير آمن أو غير صالح."));
        }
    }

    private static void ValidateCondition(
        FormConditionGroup? group,
        string path,
        Guid? entityId,
        IReadOnlyDictionary<string, FormFieldSchema> fieldsByKey,
        List<FormSchemaValidationIssue> issues,
        bool deferFieldCheck,
        int depth,
        ref int conditionNodes)
    {
        if (group is null)
        {
            return;
        }

        if (depth > MaxConditionDepth)
        {
            issues.Add(Issue("ConditionDepthExceeded", path, entityId, null, $"عمق الشروط يتجاوز الحد ({MaxConditionDepth})."));
            return;
        }

        conditionNodes += group.Predicates.Count;
        foreach (var predicate in group.Predicates)
        {
            if (!deferFieldCheck)
            {
                if (!fieldsByKey.TryGetValue(predicate.FieldKey, out var field))
                {
                    issues.Add(Issue("MissingFieldReference", path, entityId, predicate.FieldKey, $"مرجع الحقل '{predicate.FieldKey}' غير موجود."));
                    continue;
                }

                if (!IsOperatorCompatible(field.Type, predicate.Operator))
                {
                    issues.Add(Issue("OperatorTypeMismatch", path, entityId, predicate.FieldKey, "عامل الشرط غير متوافق مع نوع الحقل."));
                }
            }
        }

        foreach (var nested in group.Groups)
        {
            ValidateCondition(nested, path, entityId, fieldsByKey, issues, deferFieldCheck, depth + 1, ref conditionNodes);
        }
    }

    private static void ValidateFormula(
        FormFormulaNode? node,
        string path,
        Guid entityId,
        FormFieldType fieldType,
        IReadOnlyDictionary<string, FormFieldSchema> fieldsByKey,
        List<FormSchemaValidationIssue> issues,
        ref int formulaNodes)
    {
        if (node is null)
        {
            return;
        }

        formulaNodes++;
        switch (node)
        {
            case FormConstantNumberNode:
            case FormConstantTextNode:
                break;
            case FormFieldReferenceNode fr:
                if (string.IsNullOrWhiteSpace(fr.FieldKey))
                {
                    issues.Add(Issue("MissingFormulaFieldKey", path, entityId, null, "مرجع الحقل في المعادلة مطلوب."));
                }
                else if (!fieldsByKey.ContainsKey(fr.FieldKey))
                {
                    issues.Add(Issue("MissingFieldReference", path, entityId, fr.FieldKey, $"مرجع الحقل '{fr.FieldKey}' غير موجود."));
                }

                break;
            case FormBinaryOperationNode bin:
                if (bin.Left is null)
                {
                    issues.Add(Issue("MissingFormulaOperand", path + ".left", entityId, null, "العامل الأيسر في المعادلة مطلوب."));
                }
                else
                {
                    ValidateFormula(bin.Left, path + ".left", entityId, fieldType, fieldsByKey, issues, ref formulaNodes);
                }

                if (bin.Right is null)
                {
                    issues.Add(Issue("MissingFormulaOperand", path + ".right", entityId, null, "العامل الأيمن في المعادلة مطلوب."));
                }
                else
                {
                    ValidateFormula(bin.Right, path + ".right", entityId, fieldType, fieldsByKey, issues, ref formulaNodes);
                }

                break;
            case FormFunctionCallNode fn:
                if (!Enum.IsDefined(fn.Function))
                {
                    issues.Add(Issue("UnknownFormulaFunction", path, entityId, null, "دالة معادلة غير مسجلة."));
                }
                else if (!HasRequiredArguments(fn))
                {
                    issues.Add(Issue("MissingFormulaArguments", path, entityId, null, "عدد وسائط الدالة غير كافٍ."));
                }

                foreach (var arg in fn.Arguments)
                {
                    ValidateFormula(arg, path, entityId, fieldType, fieldsByKey, issues, ref formulaNodes);
                }

                break;
            default:
                issues.Add(Issue("UnknownFormulaNode", path, entityId, null, "عقدة معادلة غير معروفة."));
                break;
        }

        if (fieldType == FormFieldType.CalculatedText && node is FormConstantNumberNode)
        {
            return;
        }

        if (fieldType == FormFieldType.CalculatedNumber && node is FormConstantTextNode)
        {
            issues.Add(Issue("FormulaResultTypeMismatch", path, entityId, null, "نوع نتيجة المعادلة غير متوافق مع نوع الحقل."));
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

    private static void TrackId(Guid id, string path, HashSet<Guid> ids, List<FormSchemaValidationIssue> issues)
    {
        if (!ids.Add(id))
        {
            issues.Add(Issue("DuplicateId", path, id, null, "معرّف مكرر داخل المخطط."));
        }
    }

    private static void TrackKey(string key, string path, Guid entityId, HashSet<string> keys, List<FormSchemaValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            issues.Add(Issue("MissingKey", path, entityId, key, "المفتاح مطلوب."));
            return;
        }

        if (key.Length > MaxKeyLength)
        {
            issues.Add(Issue("KeyTooLong", path, entityId, key, "المفتاح طويل جدًا."));
            return;
        }

        if (!keys.Add(key))
        {
            issues.Add(Issue("DuplicateKey", path, entityId, key, "المفتاح مكرر دون اعتبار حالة الأحرف."));
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
}
