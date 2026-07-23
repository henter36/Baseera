namespace Baseera.Application.Forms.Responses;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Baseera.Application.Forms.Schema;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms.Schema;

public interface IFormResponseValidator
{
    FormResponseValidationResult Validate(
        FormSchemaDocument schema,
        JsonElement answers,
        FormResponseValidationMode mode,
        IReadOnlyDictionary<Guid, Attachment>? attachmentsById = null);
}

public enum FormResponseValidationMode
{
    DraftPartial = 0,
    FullSubmit = 1
}

public sealed class FormResponseValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<FormResponseValidationIssueDto> Issues { get; init; } = [];
    public string CanonicalAnswersJson { get; init; } = "{}";
    public string AnswersHash { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> CalculatedValues { get; init; } =
        new Dictionary<string, object?>();
    public IReadOnlySet<string> VisibleFieldKeys { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> RequiredFieldKeys { get; init; } = new HashSet<string>();
    public IReadOnlyDictionary<string, object?> AnswerValues { get; init; } =
        new Dictionary<string, object?>();
}

public sealed class FormResponseValidator : IFormResponseValidator
{
    private const string ErrorSeverity = "Error";
    private const string TypeMismatchCode = "TYPE_MISMATCH";
    public const int MaxAnswersJsonBytes = 512 * 1024;
    public const int MaxAnswerKeys = 500;
    public const int MaxRepeatingRows = 100;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public FormResponseValidationResult Validate(
        FormSchemaDocument schema,
        JsonElement answers,
        FormResponseValidationMode mode,
        IReadOnlyDictionary<Guid, Attachment>? attachmentsById = null)
    {
        var issues = new List<FormResponseValidationIssueDto>();
        if (answers.ValueKind is not JsonValueKind.Object)
        {
            issues.Add(Issue("MALFORMED", "$", null, "صيغة الإجابات غير صالحة.", ErrorSeverity));
            return Fail(issues);
        }

        var raw = answers.GetRawText();
        if (Encoding.UTF8.GetByteCount(raw) > MaxAnswersJsonBytes)
        {
            issues.Add(Issue("PAYLOAD_TOO_LARGE", "$", null, "حجم الإجابات كبير جدًا.", ErrorSeverity));
            return Fail(issues);
        }

        var fields = FlattenFields(schema);
        var provided = ParseAnswers(answers, fields, issues);
        if (issues.Any(i => i.Severity == ErrorSeverity && IsStructural(i.Code)))
        {
            return Fail(issues, provided);
        }

        var values = provided.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        ApplyCalculated(fields, values);
        var visible = ResolveVisible(schema, values);
        var required = ResolveRequired(fields, values, visible);

        foreach (var field in fields.Values)
        {
            ValidateField(new FieldValidationContext
            {
                Field = field,
                Values = values,
                Visible = visible,
                Required = required,
                Mode = mode,
                AttachmentsById = attachmentsById,
                Issues = issues
            });
        }

        var canonical = BuildCanonical(fields, values, visible);
        var hash = Sha256(canonical);
        var calculated = fields.Values
            .Where(f => f.IsCalculated)
            .ToDictionary(f => f.Key, f => values.GetValueOrDefault(f.Key), StringComparer.Ordinal);

        return new FormResponseValidationResult
        {
            IsValid = issues.All(i => i.Severity != ErrorSeverity),
            Issues = issues,
            CanonicalAnswersJson = canonical,
            AnswersHash = hash,
            CalculatedValues = calculated,
            VisibleFieldKeys = visible,
            RequiredFieldKeys = required,
            AnswerValues = values
        };
    }

    private static bool IsStructural(string code) =>
        code is "UNKNOWN_FIELD" or "DUPLICATE_KEY" or TypeMismatchCode or "READONLY_WRITE"
            or "CALCULATED_WRITE" or "ATTACHMENT_UNAUTHORIZED" or "MALFORMED" or "PAYLOAD_TOO_LARGE";

    private static FormResponseValidationResult Fail(
        List<FormResponseValidationIssueDto> issues,
        Dictionary<string, object?>? values = null) =>
        new()
        {
            IsValid = false,
            Issues = issues,
            CanonicalAnswersJson = "{}",
            AnswersHash = Sha256("{}"),
            AnswerValues = values ?? new Dictionary<string, object?>()
        };

    private static Dictionary<string, FormFieldSchema> FlattenFields(FormSchemaDocument schema)
    {
        var map = new Dictionary<string, FormFieldSchema>(StringComparer.Ordinal);
        foreach (var page in schema.Pages.OrderBy(p => p.Order))
        {
            foreach (var section in page.Sections.OrderBy(s => s.Order))
            {
                foreach (var field in section.Fields.OrderBy(f => f.Order))
                {
                    map[field.Key] = field;
                }
            }
        }

        return map;
    }

    private sealed class AnswerParsingContext
    {
        public required Dictionary<string, FormFieldSchema> Fields { get; init; }
        public required List<FormResponseValidationIssueDto> Issues { get; init; }
        public Dictionary<string, object?> Result { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Seen { get; } = new(StringComparer.Ordinal);
        public int KeyCount { get; set; }
    }

    private static Dictionary<string, object?> ParseAnswers(
        JsonElement answers,
        Dictionary<string, FormFieldSchema> fields,
        List<FormResponseValidationIssueDto> issues)
    {
        var context = new AnswerParsingContext { Fields = fields, Issues = issues };
        foreach (var prop in answers.EnumerateObject())
        {
            if (!TryProcessAnswerProperty(context, prop))
            {
                break;
            }
        }

        return context.Result;
    }

    private static bool TryProcessAnswerProperty(AnswerParsingContext context, JsonProperty prop)
    {
        context.KeyCount++;
        if (context.KeyCount > MaxAnswerKeys)
        {
            context.Issues.Add(Issue("TOO_MANY_KEYS", "$", null, "عدد مفاتيح الإجابات يتجاوز الحد.", ErrorSeverity));
            return false;
        }

        if (!context.Seen.Add(prop.Name))
        {
            context.Issues.Add(Issue("DUPLICATE_KEY", prop.Name, prop.Name, "مفتاح إجابة مكرر.", ErrorSeverity));
            return true;
        }

        if (!context.Fields.TryGetValue(prop.Name, out var field))
        {
            context.Issues.Add(Issue("UNKNOWN_FIELD", prop.Name, prop.Name, "حقل غير موجود في المخطط.", ErrorSeverity));
            return true;
        }

        if (field.IsCalculated || field.IsReadOnly)
        {
            context.Issues.Add(Issue(
                field.IsCalculated ? "CALCULATED_WRITE" : "READONLY_WRITE",
                prop.Name,
                prop.Name,
                "لا يمكن تعديل هذا الحقل من العميل.",
                ErrorSeverity));
            return true;
        }

        context.Result[prop.Name] = CoerceValue(field, prop.Value, context.Issues);
        return true;
    }

    private static object? CoerceValue(
        FormFieldSchema field,
        JsonElement value,
        List<FormResponseValidationIssueDto> issues)
    {
        try
        {
            return field.Type switch
            {
                FormFieldType.ShortText or FormFieldType.LongText => ParseNullableString(value, field.Key),
                FormFieldType.Number or FormFieldType.Percentage => ParseDecimal(value, field.Key),
                FormFieldType.YesNo => ParseNullableBoolean(value, field.Key),
                FormFieldType.Date => ParseDateOnly(value, field.Key),
                FormFieldType.Time => ParseNullableString(value, field.Key),
                FormFieldType.DateTime => ParseDateTime(value, field.Key),
                FormFieldType.SingleChoice => ParseNullableString(value, field.Key),
                FormFieldType.MultipleChoice => ParseStringArray(value, field.Key),
                FormFieldType.File or FormFieldType.Image or FormFieldType.Signature => ParseGuidArray(value, field.Key),
                FormFieldType.Location => ParseNullableJsonObject(value, field.Key),
                FormFieldType.OrganizationalReference => ParseGuid(value, field.Key),
                FormFieldType.RepeatingTable => ParseRepeating(value, field, issues),
                _ => value.ValueKind == JsonValueKind.Null ? null : value.GetRawText()
            };
        }
        catch (InvalidCastException)
        {
            issues.Add(Issue(TypeMismatchCode, field.Key, field.Key, "نوع القيمة غير مطابق للحقل.", ErrorSeverity));
            return null;
        }
    }

    private static object ThrowType(string key) =>
        throw new InvalidCastException(key);

    private static string? ParseNullableString(JsonElement value, string key)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (value.ValueKind != JsonValueKind.String) throw new InvalidCastException(key);
        return value.GetString();
    }

    private static bool? ParseNullableBoolean(JsonElement value, string key)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
        throw new InvalidCastException(key);
    }

    private static string? ParseNullableJsonObject(JsonElement value, string key)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidCastException(key);
        return value.GetRawText();
    }

    private static decimal? ParseDecimal(JsonElement value, string key)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var d)) return d;
        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        throw new InvalidCastException(key);
    }

    private static string? ParseDateOnly(JsonElement value, string key)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new InvalidCastException(key);
        var s = value.GetString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (!DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new InvalidCastException(key);
        }

        return s;
    }

    private static string? ParseDateTime(JsonElement value, string key)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new InvalidCastException(key);
        var s = value.GetString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (!DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            throw new InvalidCastException(key);
        }

        return s;
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement value, string key)
    {
        if (value.ValueKind == JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidCastException(key);
        return value.EnumerateArray().Select(x =>
        {
            if (x.ValueKind != JsonValueKind.String) throw new InvalidCastException(key);
            return x.GetString() ?? string.Empty;
        }).ToList();
    }

    private static IReadOnlyList<Guid> ParseGuidArray(JsonElement value, string key)
    {
        if (value.ValueKind == JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidCastException(key);
        return value.EnumerateArray().Select(x =>
        {
            if (x.ValueKind != JsonValueKind.String || !Guid.TryParse(x.GetString(), out var id))
            {
                throw new InvalidCastException(key);
            }

            return id;
        }).ToList();
    }

    private static Guid? ParseGuid(JsonElement value, string key)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String || !Guid.TryParse(value.GetString(), out var id))
        {
            throw new InvalidCastException(key);
        }

        return id;
    }

    private static IReadOnlyList<Dictionary<string, object?>> ParseRepeating(
        JsonElement value,
        FormFieldSchema field,
        List<FormResponseValidationIssueDto> issues)
    {
        if (value.ValueKind == JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidCastException(field.Key);
        if (value.GetArrayLength() > MaxRepeatingRows)
        {
            issues.Add(Issue("TOO_MANY_ROWS", field.Key, field.Key, "عدد صفوف الجدول يتجاوز الحد.", ErrorSeverity));
            return [];
        }

        var rows = new List<Dictionary<string, object?>>();
        var rowIds = new HashSet<string>(StringComparer.Ordinal);
        var columns = field.RepeatingTable?.Columns ?? [];
        var colMap = columns.ToDictionary(c => c.Key, c => c, StringComparer.Ordinal);
        var index = 0;
        foreach (var row in value.EnumerateArray())
        {
            ParseRepeatingRow(row, field, colMap, rowIds, rows, issues, ref index);
        }

        return rows;
    }

    private static void ParseRepeatingRow(
        JsonElement row,
        FormFieldSchema field,
        IReadOnlyDictionary<string, FormFieldSchema> colMap,
        HashSet<string> rowIds,
        List<Dictionary<string, object?>> rows,
        List<FormResponseValidationIssueDto> issues,
        ref int index)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(TypeMismatchCode, $"{field.Key}[{index}]", field.Key, "صف غير صالح.", ErrorSeverity));
            index++;
            return;
        }

        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in row.EnumerateObject())
        {
            ProcessRepeatingProperty(prop, field, colMap, rowIds, dict, issues, index);
        }

        rows.Add(dict);
        index++;
    }

    private static void ProcessRepeatingProperty(
        JsonProperty prop,
        FormFieldSchema field,
        IReadOnlyDictionary<string, FormFieldSchema> colMap,
        HashSet<string> rowIds,
        Dictionary<string, object?> dict,
        List<FormResponseValidationIssueDto> issues,
        int index)
    {
        if (prop.Name == "_rowId")
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
            {
                issues.Add(Issue(TypeMismatchCode, $"{field.Key}[{index}]._rowId", field.Key, "نوع القيمة غير مطابق للحقل.", ErrorSeverity));
                return;
            }

            var id = prop.Value.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                issues.Add(Issue("INVALID_ROW_ID", $"{field.Key}[{index}]._rowId", field.Key, "معرّف الصف فارغ.", ErrorSeverity));
                return;
            }

            if (!rowIds.Add(id))
            {
                issues.Add(Issue("DUPLICATE_ROW", $"{field.Key}[{index}]._rowId", field.Key, "معرّف صف مكرر.", ErrorSeverity));
            }

            dict["_rowId"] = id;
            return;
        }

        if (!colMap.ContainsKey(prop.Name))
        {
            issues.Add(Issue("UNKNOWN_FIELD", $"{field.Key}[{index}].{prop.Name}", field.Key, "عمود غير معروف.", ErrorSeverity));
            return;
        }

        dict[prop.Name] = CoerceRepeatingCellValue(prop.Value);
    }

    private static object? CoerceRepeatingCellValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetDecimal(out var d) => d,
        JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => value.GetRawText()
    };

    private static void ApplyCalculated(
        Dictionary<string, FormFieldSchema> fields,
        Dictionary<string, object?> values)
    {
        foreach (var field in fields.Values.Where(f => f.IsCalculated))
        {
            values[field.Key] = FormFormulaEvaluator.Evaluate(field.Formula, values);
        }
    }

    private static HashSet<string> ResolveVisible(
        FormSchemaDocument schema,
        IReadOnlyDictionary<string, object?> values)
    {
        var visible = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in schema.Pages.Where(page =>
                     FormConditionEvaluator.Evaluate(page.VisibilityCondition, values)))
        {
            foreach (var section in page.Sections.Where(section =>
                         FormConditionEvaluator.Evaluate(section.VisibilityCondition, values)))
            {
                foreach (var field in section.Fields.Where(field =>
                             FormConditionEvaluator.Evaluate(field.VisibilityCondition, values)))
                {
                    visible.Add(field.Key);
                }
            }
        }

        return visible;
    }

    private static HashSet<string> ResolveRequired(
        Dictionary<string, FormFieldSchema> fields,
        IReadOnlyDictionary<string, object?> values,
        IReadOnlySet<string> visible)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields.Values.Where(field =>
                     visible.Contains(field.Key) && !field.IsCalculated && !field.IsReadOnly))
        {
            var isRequired = field.IsRequired
                || (field.RequiredCondition is not null
                    && FormConditionEvaluator.Evaluate(field.RequiredCondition, values));
            if (isRequired) required.Add(field.Key);
        }

        return required;
    }

    private static void ValidateField(FieldValidationContext context)
    {
        if (!context.Visible.Contains(context.Field.Key)) return;
        if (ValidateRequired(context)) return;
        context.Values.TryGetValue(context.Field.Key, out var value);
        if (IsEmptyValue(value)) return;

        switch (context.Field.Type)
        {
            case FormFieldType.ShortText:
            case FormFieldType.LongText:
                ValidateTextField(context, value);
                break;
            case FormFieldType.Number:
            case FormFieldType.Percentage:
                ValidateNumberField(context, value);
                break;
            case FormFieldType.SingleChoice:
            case FormFieldType.MultipleChoice:
                ValidateChoiceField(context, value);
                break;
            case FormFieldType.Date:
            case FormFieldType.Time:
            case FormFieldType.DateTime:
                ValidateTemporalField(context, value);
                break;
            case FormFieldType.File:
            case FormFieldType.Image:
            case FormFieldType.Signature:
                ValidateFileField(context, value);
                break;
            case FormFieldType.OrganizationalReference:
                ValidateOrgField(context, value);
                break;
            case FormFieldType.RepeatingTable:
                ValidateRepeatingField(context, value);
                break;
        }
    }

    private static bool ValidateRequired(FieldValidationContext context)
    {
        if (context.Mode != FormResponseValidationMode.FullSubmit || !context.Required.Contains(context.Field.Key))
        {
            return false;
        }

        context.Values.TryGetValue(context.Field.Key, out var value);
        if (!IsEmptyValue(value))
        {
            return false;
        }

        context.Issues.Add(Issue("REQUIRED", context.Field.Key, context.Field.Key, $"الحقل «{context.Field.LabelAr}» مطلوب.", ErrorSeverity));
        return true;
    }

    private static bool IsEmptyValue(object? value) =>
        value is null
        || (value is string s && string.IsNullOrWhiteSpace(s))
        || (value is IEnumerable<object?> e && !e.Any())
        || (value is IReadOnlyList<Guid> g && g.Count == 0)
        || (value is IReadOnlyList<string> ss && ss.Count == 0);

    private static void ValidateTextField(FieldValidationContext context, object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        var settings = context.Field.Text;
        if (settings?.MinLength is int min && text.Length < min)
        {
            context.Issues.Add(Issue("MIN_LENGTH", context.Field.Key, context.Field.Key, $"الحد الأدنى للطول {min}.", ErrorSeverity));
        }

        if (settings?.MaxLength is int max && text.Length > max)
        {
            context.Issues.Add(Issue("MAX_LENGTH", context.Field.Key, context.Field.Key, $"الحد الأقصى للطول {max}.", ErrorSeverity));
        }

        ValidateTextKind(context.Field, text, settings, context.Issues);
    }

    private static void ValidateTextKind(
        FormFieldSchema field,
        string text,
        FormTextFieldSettings? settings,
        List<FormResponseValidationIssueDto> issues)
    {
        if (settings is null || settings.Kind == FormTextValidationKind.None) return;
        var ok = settings.Kind switch
        {
            FormTextValidationKind.Email => Regex.IsMatch(text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, RegexTimeout),
            FormTextValidationKind.Phone => Regex.IsMatch(text, @"^\+?[0-9\s\-]{8,20}$", RegexOptions.CultureInvariant, RegexTimeout),
            FormTextValidationKind.Url => Uri.TryCreate(text, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
            FormTextValidationKind.CustomPattern when !string.IsNullOrWhiteSpace(settings.CustomPattern) =>
                SafeRegexMatch(text, settings.CustomPattern),
            _ => true
        };
        if (!ok)
        {
            issues.Add(Issue("PATTERN", field.Key, field.Key, "صيغة النص غير صالحة.", ErrorSeverity));
        }
    }

    private static bool SafeRegexMatch(string text, string pattern)
    {
        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (RegexParseException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static void ValidateNumberField(FieldValidationContext context, object? value)
    {
        if (value is not decimal number
            && !(value is string s && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out number)))
        {
            context.Issues.Add(Issue(TypeMismatchCode, context.Field.Key, context.Field.Key, "قيمة رقمية غير صالحة.", ErrorSeverity));
            return;
        }

        var settings = context.Field.Number;
        if (settings?.Min is decimal min && number < min)
        {
            context.Issues.Add(Issue("MIN", context.Field.Key, context.Field.Key, $"الحد الأدنى {min.ToString(CultureInfo.InvariantCulture)}.", ErrorSeverity));
        }

        if (settings?.Max is decimal max && number > max)
        {
            context.Issues.Add(Issue("MAX", context.Field.Key, context.Field.Key, $"الحد الأقصى {max.ToString(CultureInfo.InvariantCulture)}.", ErrorSeverity));
        }

        if (context.Field.Type == FormFieldType.Percentage && (number < 0 || number > 100))
        {
            context.Issues.Add(Issue("PERCENTAGE_RANGE", context.Field.Key, context.Field.Key, "النسبة يجب أن تكون بين 0 و100.", ErrorSeverity));
        }

        if (settings?.DecimalPlaces is int places)
        {
            var scaled = decimal.Round(number, places, MidpointRounding.AwayFromZero);
            if (scaled != number)
            {
                context.Issues.Add(Issue("DECIMAL_PLACES", context.Field.Key, context.Field.Key, $"عدد الخانات العشرية المسموح {places}.", ErrorSeverity));
            }
        }
    }

    private static void ValidateChoiceField(FieldValidationContext context, object? value)
    {
        var options = context.Field.Choice?.Options?.Where(o => o.IsActive).Select(o => o.Value).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        if (context.Field.Type == FormFieldType.SingleChoice)
        {
            var selected = value?.ToString() ?? string.Empty;
            if (!options.Contains(selected) && context.Field.Choice?.AllowOther != true)
            {
                context.Issues.Add(Issue("INVALID_OPTION", context.Field.Key, context.Field.Key, "خيار غير صالح.", ErrorSeverity));
            }

            return;
        }

        var list = value as IReadOnlyList<string> ?? [];
        if (context.Field.Choice?.MinSelections is int minSel && list.Count < minSel)
        {
            context.Issues.Add(Issue("MIN_SELECTIONS", context.Field.Key, context.Field.Key, $"الحد الأدنى للاختيارات {minSel}.", ErrorSeverity));
        }

        if (context.Field.Choice?.MaxSelections is int maxSel && list.Count > maxSel)
        {
            context.Issues.Add(Issue("MAX_SELECTIONS", context.Field.Key, context.Field.Key, $"الحد الأقصى للاختيارات {maxSel}.", ErrorSeverity));
        }

        foreach (var item in list.Where(item => !options.Contains(item) && context.Field.Choice?.AllowOther != true))
        {
            context.Issues.Add(Issue("INVALID_OPTION", context.Field.Key, context.Field.Key, "خيار غير صالح.", ErrorSeverity));
        }
    }

    private static void ValidateTemporalField(FieldValidationContext context, object? value)
    {
        if (value is null) return;
        if (value is not string)
        {
            context.Issues.Add(Issue(TypeMismatchCode, context.Field.Key, context.Field.Key, "قيمة زمنية غير صالحة.", ErrorSeverity));
        }
    }

    private static void ValidateOrgField(FieldValidationContext context, object? value)
    {
        if (value is null) return;
        if (value is not Guid)
        {
            context.Issues.Add(Issue(TypeMismatchCode, context.Field.Key, context.Field.Key, "مرجع تنظيمي غير صالح.", ErrorSeverity));
        }
    }

    private static void ValidateFileField(FieldValidationContext context, object? value)
    {
        var ids = value as IReadOnlyList<Guid> ?? [];
        var settings = context.Field.File;
        if (settings is not null && ids.Count > settings.MaxFiles)
        {
            context.Issues.Add(Issue("MAX_FILES", context.Field.Key, context.Field.Key, $"الحد الأقصى للملفات {settings.MaxFiles}.", ErrorSeverity));
        }

        if (context.AttachmentsById is null) return;
        foreach (var id in ids)
        {
            if (!context.AttachmentsById.TryGetValue(id, out var attachment))
            {
                context.Issues.Add(Issue("ATTACHMENT_UNAUTHORIZED", context.Field.Key, context.Field.Key, "مرفق غير مصرح.", ErrorSeverity));
                continue;
            }

            if (attachment.ScanStatus is AttachmentScanStatus.PendingScan or AttachmentScanStatus.Quarantined or AttachmentScanStatus.Rejected)
            {
                context.Issues.Add(Issue("ATTACHMENT_SCAN", context.Field.Key, context.Field.Key, "حالة فحص المرفق غير مقبولة للإرسال.", ErrorSeverity));
            }
        }
    }

    private static void ValidateRepeatingField(FieldValidationContext context, object? value)
    {
        var rows = value as IReadOnlyList<Dictionary<string, object?>> ?? [];
        var settings = context.Field.RepeatingTable;
        if (settings is not null && context.Mode == FormResponseValidationMode.FullSubmit && rows.Count < settings.MinRows)
        {
            context.Issues.Add(Issue("MIN_ROWS", context.Field.Key, context.Field.Key, $"الحد الأدنى للصفوف {settings.MinRows}.", ErrorSeverity));
        }

        if (settings is not null && rows.Count > settings.MaxRows)
        {
            context.Issues.Add(Issue("MAX_ROWS", context.Field.Key, context.Field.Key, $"الحد الأقصى للصفوف {settings.MaxRows}.", ErrorSeverity));
        }
    }

    private static string BuildCanonical(
        Dictionary<string, FormFieldSchema> fields,
        IReadOnlyDictionary<string, object?> values,
        IReadOnlySet<string> visible)
    {
        var ordered = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in fields.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!visible.Contains(key) && !fields[key].IsCalculated) continue;
            if (!values.TryGetValue(key, out var value)) continue;
            ordered[key] = NormalizeCanonical(value);
        }

        return JsonSerializer.Serialize(ordered, JsonOptions);
    }

    private static object? NormalizeCanonical(object? value) => value switch
    {
        null => null,
        decimal d => d,
        bool b => b,
        string s => s,
        IReadOnlyList<string> ss => ss.ToList(),
        IReadOnlyList<Guid> gg => gg.Select(g => g.ToString("D")).ToList(),
        IReadOnlyList<Dictionary<string, object?>> rows => rows.Select(r =>
            new SortedDictionary<string, object?>(r, StringComparer.Ordinal)).ToList(),
        _ => value.ToString()
    };

    private static string Sha256(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static FormResponseValidationIssueDto Issue(
        string code,
        string path,
        string? fieldKey,
        string messageAr,
        string severity) =>
        new(code, path, fieldKey, messageAr, severity);

    private sealed record FieldValidationContext
    {
        public FormFieldSchema Field { get; init; } = null!;
        public IReadOnlyDictionary<string, object?> Values { get; init; } = null!;
        public IReadOnlySet<string> Visible { get; init; } = null!;
        public IReadOnlySet<string> Required { get; init; } = null!;
        public FormResponseValidationMode Mode { get; init; }
        public IReadOnlyDictionary<Guid, Attachment>? AttachmentsById { get; init; }
        public List<FormResponseValidationIssueDto> Issues { get; init; } = null!;
    }
}
