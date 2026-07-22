namespace Baseera.Application.Forms.Schema;

using System.Text.RegularExpressions;
using Baseera.Domain.Forms.Schema;

public static class FormSchemaValidator
{
    public const int MaxSchemaBytes = 1_048_576;
    public const int MaxRegexLength = 200;
    public const int CurrentSchemaFormatVersion = 1;

    private static readonly HashSet<FormConditionOperator> NumericOps =
    [
        FormConditionOperator.GreaterThan, FormConditionOperator.GreaterThanOrEqual,
        FormConditionOperator.LessThan, FormConditionOperator.LessThanOrEqual
    ];

    private static readonly HashSet<FormConditionOperator> DateOps =
    [
        FormConditionOperator.Before, FormConditionOperator.After,
        FormConditionOperator.Equals, FormConditionOperator.NotEquals,
        FormConditionOperator.IsEmpty, FormConditionOperator.IsNotEmpty
    ];

    public static List<FormSchemaValidationIssue> Validate(FormSchemaDocument document, bool requireMinimumContent)
    {
        var issues = new List<FormSchemaValidationIssue>();
        if (document.SchemaFormatVersion != CurrentSchemaFormatVersion)
        {
            issues.Add(Issue("UnsupportedSchemaFormat", "$", null, null, "إصدار تنسيق المخطط غير مدعوم."));
        }

        var ids = new HashSet<Guid>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldsByKey = new Dictionary<string, FormFieldSchema>(StringComparer.OrdinalIgnoreCase);

        void TrackId(Guid id, string path)
        {
            if (!ids.Add(id))
            {
                issues.Add(Issue("DuplicateId", path, id, null, "معرّف مكرر داخل المخطط."));
            }
        }

        void TrackKey(string key, string path, Guid entityId)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                issues.Add(Issue("MissingKey", path, entityId, key, "المفتاح مطلوب."));
                return;
            }

            if (!keys.Add(key))
            {
                issues.Add(Issue("DuplicateKey", path, entityId, key, "المفتاح مكرر دون اعتبار حالة الأحرف."));
            }
        }

        if (requireMinimumContent)
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

        foreach (var page in document.Pages.OrderBy(p => p.Order).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            TrackId(page.Id, $"pages[{page.Key}]");
            TrackKey(page.Key, $"pages[{page.Key}].key", page.Id);
            if (string.IsNullOrWhiteSpace(page.TitleAr))
            {
                issues.Add(Issue("MissingTitleAr", $"pages[{page.Key}].titleAr", page.Id, page.Key, "عنوان الصفحة العربي مطلوب."));
            }

            ValidateCondition(page.VisibilityCondition, $"pages[{page.Key}].visibility", page.Id, fieldsByKey, issues, deferFieldCheck: true);

            foreach (var section in page.Sections.OrderBy(s => s.Order).ThenBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
            {
                TrackId(section.Id, $"sections[{section.Key}]");
                TrackKey(section.Key, $"sections[{section.Key}].key", section.Id);
                if (string.IsNullOrWhiteSpace(section.TitleAr))
                {
                    issues.Add(Issue("MissingTitleAr", $"sections[{section.Key}].titleAr", section.Id, section.Key, "عنوان القسم العربي مطلوب."));
                }

                ValidateCondition(section.VisibilityCondition, $"sections[{section.Key}].visibility", section.Id, fieldsByKey, issues, deferFieldCheck: true);

                foreach (var field in section.Fields.OrderBy(f => f.Order).ThenBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
                {
                    ValidateField(field, $"fields[{field.Key}]", issues, ids, keys, fieldsByKey, insideRepeating: false);
                }
            }
        }

        // second pass for conditions/formulas after field index built
        foreach (var page in document.Pages)
        {
            ValidateCondition(page.VisibilityCondition, $"pages[{page.Key}].visibility", page.Id, fieldsByKey, issues, deferFieldCheck: false);
            foreach (var section in page.Sections)
            {
                ValidateCondition(section.VisibilityCondition, $"sections[{section.Key}].visibility", section.Id, fieldsByKey, issues, deferFieldCheck: false);
                foreach (var field in section.Fields)
                {
                    ValidateCondition(field.VisibilityCondition, $"fields[{field.Key}].visibility", field.Id, fieldsByKey, issues, deferFieldCheck: false);
                    ValidateCondition(field.RequiredCondition, $"fields[{field.Key}].required", field.Id, fieldsByKey, issues, deferFieldCheck: false);
                    ValidateFormula(field.Formula, $"fields[{field.Key}].formula", field.Id, field.Type, issues);
                    if (field.RepeatingTable is not null)
                    {
                        foreach (var col in field.RepeatingTable.Columns)
                        {
                            ValidateCondition(col.VisibilityCondition, $"fields[{field.Key}].columns[{col.Key}].visibility", col.Id, fieldsByKey, issues, deferFieldCheck: false);
                            ValidateFormula(col.Formula, $"fields[{field.Key}].columns[{col.Key}].formula", col.Id, col.Type, issues);
                        }
                    }
                }
            }
        }

        issues.AddRange(FormDependencyGraph.DetectCyclesAndMissingRefs(document, fieldsByKey));
        return issues;
    }

    private static void ValidateField(
        FormFieldSchema field,
        string path,
        List<FormSchemaValidationIssue> issues,
        HashSet<Guid> ids,
        HashSet<string> keys,
        Dictionary<string, FormFieldSchema> fieldsByKey,
        bool insideRepeating)
    {
        if (!ids.Add(field.Id))
        {
            issues.Add(Issue("DuplicateId", path, field.Id, field.Key, "معرّف مكرر داخل المخطط."));
        }

        if (string.IsNullOrWhiteSpace(field.Key) || !keys.Add(field.Key))
        {
            issues.Add(Issue(string.IsNullOrWhiteSpace(field.Key) ? "MissingKey" : "DuplicateKey", path + ".key", field.Id, field.Key,
                string.IsNullOrWhiteSpace(field.Key) ? "المفتاح مطلوب." : "المفتاح مكرر دون اعتبار حالة الأحرف."));
        }
        else
        {
            fieldsByKey[field.Key] = field;
        }

        if (string.IsNullOrWhiteSpace(field.LabelAr))
        {
            issues.Add(Issue("MissingLabelAr", path + ".labelAr", field.Id, field.Key, "تسمية الحقل العربية مطلوبة."));
        }

        var calculated = field.Type is FormFieldType.CalculatedNumber or FormFieldType.CalculatedText;
        if (field.IsCalculated != calculated)
        {
            issues.Add(Issue("CalculatedFlagMismatch", path, field.Id, field.Key, "علامة الحقل المحسوب غير متوافقة مع النوع."));
        }

        if (calculated && field.Formula is null)
        {
            issues.Add(Issue("MissingFormula", path + ".formula", field.Id, field.Key, "الحقل المحسوب يتطلب معادلة."));
        }

        if (field.Text?.Kind == FormTextValidationKind.CustomPattern)
        {
            var pattern = field.Text.CustomPattern ?? string.Empty;
            if (pattern.Length is 0 or > MaxRegexLength)
            {
                issues.Add(Issue("UnsafeRegex", path + ".text.customPattern", field.Id, field.Key, "نمط التحقق غير صالح أو يتجاوز الحد الآمن."));
            }
            else
            {
                try
                {
                    _ = Regex.IsMatch("test", pattern, RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
                }
                catch (Exception)
                {
                    issues.Add(Issue("UnsafeRegex", path + ".text.customPattern", field.Id, field.Key, "نمط التحقق غير آمن أو غير صالح."));
                }
            }
        }

        if (field.Type is FormFieldType.SingleChoice or FormFieldType.MultipleChoice)
        {
            if (field.Choice is null || field.Choice.Options.Count == 0)
            {
                issues.Add(Issue("MissingOptions", path + ".choice", field.Id, field.Key, "خيارات الحقل مطلوبة."));
            }
        }

        if (field.Type == FormFieldType.RepeatingTable)
        {
            if (insideRepeating)
            {
                issues.Add(Issue("NestedRepeatingTable", path, field.Id, field.Key, "لا يُسمح بجدول متكرر داخل جدول متكرر."));
            }

            if (field.RepeatingTable is null)
            {
                issues.Add(Issue("MissingRepeatingTable", path, field.Id, field.Key, "إعدادات الجدول المتكرر مطلوبة."));
            }
            else
            {
                foreach (var col in field.RepeatingTable.Columns)
                {
                    ValidateField(col, path + $".columns[{col.Key}]", issues, ids, keys, fieldsByKey, insideRepeating: true);
                }
            }
        }
    }

    private static void ValidateCondition(
        FormConditionGroup? group,
        string path,
        Guid? entityId,
        IReadOnlyDictionary<string, FormFieldSchema> fieldsByKey,
        List<FormSchemaValidationIssue> issues,
        bool deferFieldCheck)
    {
        if (group is null) return;
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
            ValidateCondition(nested, path, entityId, fieldsByKey, issues, deferFieldCheck);
        }
    }

    private static void ValidateFormula(
        FormFormulaNode? node,
        string path,
        Guid entityId,
        FormFieldType fieldType,
        List<FormSchemaValidationIssue> issues)
    {
        if (node is null) return;
        switch (node)
        {
            case FormConstantNumberNode:
            case FormConstantTextNode:
            case FormFieldReferenceNode:
                break;
            case FormBinaryOperationNode bin:
                ValidateFormula(bin.Left, path, entityId, fieldType, issues);
                ValidateFormula(bin.Right, path, entityId, fieldType, issues);
                break;
            case FormFunctionCallNode fn:
                if (!Enum.IsDefined(fn.Function))
                {
                    issues.Add(Issue("UnknownFormulaFunction", path, entityId, null, "دالة معادلة غير مسجلة."));
                }

                foreach (var arg in fn.Arguments)
                {
                    ValidateFormula(arg, path, entityId, fieldType, issues);
                }

                break;
            default:
                issues.Add(Issue("UnknownFormulaNode", path, entityId, null, "عقدة معادلة غير معروفة."));
                break;
        }

        if (fieldType == FormFieldType.CalculatedText && node is FormConstantNumberNode)
        {
            // allowed via coercion
        }
    }

    private static bool IsOperatorCompatible(FormFieldType type, FormConditionOperator op)
    {
        if (op is FormConditionOperator.IsEmpty or FormConditionOperator.IsNotEmpty) return true;
        if (type is FormFieldType.YesNo)
        {
            return op is FormConditionOperator.IsTrue or FormConditionOperator.IsFalse
                or FormConditionOperator.Equals or FormConditionOperator.NotEquals;
        }

        if (type is FormFieldType.Number or FormFieldType.Percentage or FormFieldType.CalculatedNumber)
        {
            return NumericOps.Contains(op)
                   || op is FormConditionOperator.Equals or FormConditionOperator.NotEquals
                   || op is FormConditionOperator.In or FormConditionOperator.NotIn;
        }

        if (type is FormFieldType.Date or FormFieldType.DateTime or FormFieldType.Time)
        {
            return DateOps.Contains(op);
        }

        if (op is FormConditionOperator.IsTrue or FormConditionOperator.IsFalse) return false;
        if (NumericOps.Contains(op)) return false;
        return true;
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
