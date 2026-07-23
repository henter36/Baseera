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
            issues.Add(Issue("MALFORMED", "$", null, "صيغة الإجابات غير صالحة.", "Error"));
            return Fail(issues);
        }

        var raw = answers.GetRawText();
        if (Encoding.UTF8.GetByteCount(raw) > MaxAnswersJsonBytes)
        {
            issues.Add(Issue("PAYLOAD_TOO_LARGE", "$", null, "حجم الإجابات كبير جدًا.", "Error"));
            return Fail(issues);
        }

        var fields = FlattenFields(schema);
        var provided = ParseAnswers(answers, fields, issues, mode);
        if (issues.Any(i => i.Severity == "Error" && IsStructural(i.Code)))
        {
            return Fail(issues, provided);
        }

        var values = provided.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        ApplyCalculated(fields, values);
        var visible = ResolveVisible(schema, values);
        var required = ResolveRequired(fields, values, visible);

        foreach (var field in fields.Values)
        {
            ValidateField(field, values, visible, required, mode, attachmentsById, issues);
        }

        var canonical = BuildCanonical(fields, values, visible);
        var hash = Sha256(canonical);
        var calculated = fields.Values
            .Where(f => f.IsCalculated)
            .ToDictionary(f => f.Key, f => values.GetValueOrDefault(f.Key), StringComparer.Ordinal);

        return new FormResponseValidationResult
        {
            IsValid = issues.All(i => i.Severity != "Error"),
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
        code is "UNKNOWN_FIELD" or "DUPLICATE_KEY" or "TYPE_MISMATCH" or "READONLY_WRITE"
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

    private static Dictionary<string, object?> ParseAnswers(
        JsonElement answers,
        Dictionary<string, FormFieldSchema> fields,
        List<FormResponseValidationIssueDto> issues,
        FormResponseValidationMode mode)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var prop in answers.EnumerateObject())
        {
            count++;
            if (count > MaxAnswerKeys)
            {
                issues.Add(Issue("TOO_MANY_KEYS", "$", null, "عدد مفاتيح الإجابات يتجاوز الحد.", "Error"));
                break;
            }

            if (!seen.Add(prop.Name))
            {
                issues.Add(Issue("DUPLICATE_KEY", prop.Name, prop.Name, "مفتاح إجابة مكرر.", "Error"));
                continue;
            }

            if (!fields.TryGetValue(prop.Name, out var field))
            {
                issues.Add(Issue("UNKNOWN_FIELD", prop.Name, prop.Name, "حقل غير موجود في المخطط.", "Error"));
                continue;
            }

            if (field.IsCalculated || field.IsReadOnly)
            {
                issues.Add(Issue(
                    field.IsCalculated ? "CALCULATED_WRITE" : "READONLY_WRITE",
                    prop.Name,
                    prop.Name,
                    "لا يمكن تعديل هذا الحقل من العميل.",
                    "Error"));
                continue;
            }

            result[prop.Name] = CoerceValue(field, prop.Value, issues);
        }

        return result;
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
                FormFieldType.ShortText or FormFieldType.LongText => value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : ThrowType(field.Key),
                FormFieldType.Number or FormFieldType.Percentage => ParseDecimal(value, field.Key),
                FormFieldType.YesNo => value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? value.GetBoolean()
                    : ThrowType(field.Key),
                FormFieldType.Date => ParseDateOnly(value, field.Key),
                FormFieldType.Time => value.ValueKind == JsonValueKind.String ? value.GetString() : ThrowType(field.Key),
                FormFieldType.DateTime => ParseDateTime(value, field.Key),
                FormFieldType.SingleChoice => value.ValueKind == JsonValueKind.String ? value.GetString() : ThrowType(field.Key),
                FormFieldType.MultipleChoice => ParseStringArray(value, field.Key),
                FormFieldType.File or FormFieldType.Image or FormFieldType.Signature => ParseGuidArray(value, field.Key),
                FormFieldType.Location => value.ValueKind == JsonValueKind.Object ? value.GetRawText() : ThrowType(field.Key),
                FormFieldType.OrganizationalReference => ParseGuid(value, field.Key),
                FormFieldType.RepeatingTable => ParseRepeating(value, field, issues),
                _ => value.ValueKind == JsonValueKind.Null ? null : value.GetRawText()
            };
        }
        catch (InvalidCastException)
        {
            issues.Add(Issue("TYPE_MISMATCH", field.Key, field.Key, "نوع القيمة غير مطابق للحقل.", "Error"));
            return null;
        }
    }

    private static object ThrowType(string key) =>
        throw new InvalidCastException(key);

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
            issues.Add(Issue("TOO_MANY_ROWS", field.Key, field.Key, "عدد صفوف الجدول يتجاوز الحد.", "Error"));
            return [];
        }

        var rows = new List<Dictionary<string, object?>>();
        var rowIds = new HashSet<string>(StringComparer.Ordinal);
        var columns = field.RepeatingTable?.Columns ?? [];
        var colMap = columns.ToDictionary(c => c.Key, c => c, StringComparer.Ordinal);
        var index = 0;
        foreach (var row in value.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue("TYPE_MISMATCH", $"{field.Key}[{index}]", field.Key, "صف غير صالح.", "Error"));
                index++;
                continue;
            }

            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var prop in row.EnumerateObject())
            {
                if (prop.Name == "_rowId")
                {
                    var id = prop.Value.GetString() ?? string.Empty;
                    if (!rowIds.Add(id))
                    {
                        issues.Add(Issue("DUPLICATE_ROW", $"{field.Key}[{index}]._rowId", field.Key, "معرّف صف مكرر.", "Error"));
                    }

                    dict["_rowId"] = id;
                    continue;
                }

                if (!colMap.ContainsKey(prop.Name))
                {
                    issues.Add(Issue("UNKNOWN_FIELD", $"{field.Key}[{index}].{prop.Name}", field.Key, "عمود غير معروف.", "Error"));
                    continue;
                }

                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()
                    : prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var d) ? d
                    : prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False ? prop.Value.GetBoolean()
                    : prop.Value.GetRawText();
            }

            rows.Add(dict);
            index++;
        }

        return rows;
    }

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
        foreach (var page in schema.Pages)
        {
            if (!FormConditionEvaluator.Evaluate(page.VisibilityCondition, values)) continue;
            foreach (var section in page.Sections)
            {
                if (!FormConditionEvaluator.Evaluate(section.VisibilityCondition, values)) continue;
                foreach (var field in section.Fields)
                {
                    if (FormConditionEvaluator.Evaluate(field.VisibilityCondition, values))
                    {
                        visible.Add(field.Key);
                    }
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
        foreach (var field in fields.Values)
        {
            if (!visible.Contains(field.Key) || field.IsCalculated || field.IsReadOnly) continue;
            var isRequired = field.IsRequired
                || (field.RequiredCondition is not null
                    && FormConditionEvaluator.Evaluate(field.RequiredCondition, values));
            if (isRequired) required.Add(field.Key);
        }

        return required;
    }

    private static void ValidateField(
        FormFieldSchema field,
        IReadOnlyDictionary<string, object?> values,
        IReadOnlySet<string> visible,
        IReadOnlySet<string> required,
        FormResponseValidationMode mode,
        IReadOnlyDictionary<Guid, Attachment>? attachmentsById,
        List<FormResponseValidationIssueDto> issues)
    {
        if (!visible.Contains(field.Key)) return;
        values.TryGetValue(field.Key, out var value);
        if (mode == FormResponseValidationMode.FullSubmit && required.Contains(field.Key) && IsEmptyValue(value))
        {
            issues.Add(Issue("REQUIRED", field.Key, field.Key, $"الحقل «{field.LabelAr}» مطلوب.", "Error"));
            return;
        }

        if (IsEmptyValue(value)) return;
        switch (field.Type)
        {
            case FormFieldType.ShortText:
            case FormFieldType.LongText:
                ValidateText(field, value, issues);
                break;
            case FormFieldType.Number:
            case FormFieldType.Percentage:
                ValidateNumber(field, value, issues);
                break;
            case FormFieldType.SingleChoice:
            case FormFieldType.MultipleChoice:
                ValidateChoice(field, value, issues);
                break;
            case FormFieldType.File:
            case FormFieldType.Image:
            case FormFieldType.Signature:
                ValidateAttachments(field, value, attachmentsById, issues);
                break;
            case FormFieldType.RepeatingTable:
                ValidateRepeating(field, value, mode, issues);
                break;
        }
    }

    private static bool IsEmptyValue(object? value) =>
        value is null
        || (value is string s && string.IsNullOrWhiteSpace(s))
        || (value is IEnumerable<object?> e && !e.Any())
        || (value is IReadOnlyList<Guid> g && g.Count == 0)
        || (value is IReadOnlyList<string> ss && ss.Count == 0);

    private static void ValidateText(FormFieldSchema field, object? value, List<FormResponseValidationIssueDto> issues)
    {
        var text = value?.ToString() ?? string.Empty;
        var settings = field.Text;
        if (settings?.MinLength is int min && text.Length < min)
        {
            issues.Add(Issue("MIN_LENGTH", field.Key, field.Key, $"الحد الأدنى للطول {min}.", "Error"));
        }

        if (settings?.MaxLength is int max && text.Length > max)
        {
            issues.Add(Issue("MAX_LENGTH", field.Key, field.Key, $"الحد الأقصى للطول {max}.", "Error"));
        }

        ValidateTextKind(field, text, settings, issues);
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
            issues.Add(Issue("PATTERN", field.Key, field.Key, "صيغة النص غير صالحة.", "Error"));
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

    private static void ValidateNumber(FormFieldSchema field, object? value, List<FormResponseValidationIssueDto> issues)
    {
        if (value is not decimal number
            && !(value is string s && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out number)))
        {
            issues.Add(Issue("TYPE_MISMATCH", field.Key, field.Key, "قيمة رقمية غير صالحة.", "Error"));
            return;
        }

        var settings = field.Number;
        if (settings?.Min is decimal min && number < min)
        {
            issues.Add(Issue("MIN", field.Key, field.Key, $"الحد الأدنى {min.ToString(CultureInfo.InvariantCulture)}.", "Error"));
        }

        if (settings?.Max is decimal max && number > max)
        {
            issues.Add(Issue("MAX", field.Key, field.Key, $"الحد الأقصى {max.ToString(CultureInfo.InvariantCulture)}.", "Error"));
        }

        if (field.Type == FormFieldType.Percentage && (number < 0 || number > 100))
        {
            issues.Add(Issue("PERCENTAGE_RANGE", field.Key, field.Key, "النسبة يجب أن تكون بين 0 و100.", "Error"));
        }

        if (settings?.DecimalPlaces is int places)
        {
            var scaled = decimal.Round(number, places, MidpointRounding.AwayFromZero);
            if (scaled != number)
            {
                issues.Add(Issue("DECIMAL_PLACES", field.Key, field.Key, $"عدد الخانات العشرية المسموح {places}.", "Error"));
            }
        }
    }

    private static void ValidateChoice(FormFieldSchema field, object? value, List<FormResponseValidationIssueDto> issues)
    {
        var options = field.Choice?.Options?.Where(o => o.IsActive).Select(o => o.Value).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        if (field.Type == FormFieldType.SingleChoice)
        {
            var selected = value?.ToString() ?? string.Empty;
            if (!options.Contains(selected) && !(field.Choice?.AllowOther == true))
            {
                issues.Add(Issue("INVALID_OPTION", field.Key, field.Key, "خيار غير صالح.", "Error"));
            }

            return;
        }

        var list = value as IReadOnlyList<string> ?? [];
        if (field.Choice?.MinSelections is int minSel && list.Count < minSel)
        {
            issues.Add(Issue("MIN_SELECTIONS", field.Key, field.Key, $"الحد الأدنى للاختيارات {minSel}.", "Error"));
        }

        if (field.Choice?.MaxSelections is int maxSel && list.Count > maxSel)
        {
            issues.Add(Issue("MAX_SELECTIONS", field.Key, field.Key, $"الحد الأقصى للاختيارات {maxSel}.", "Error"));
        }

        foreach (var item in list.Where(item => !options.Contains(item) && field.Choice?.AllowOther != true))
        {
            issues.Add(Issue("INVALID_OPTION", field.Key, field.Key, "خيار غير صالح.", "Error"));
        }
    }

    private static void ValidateAttachments(
        FormFieldSchema field,
        object? value,
        IReadOnlyDictionary<Guid, Attachment>? attachmentsById,
        List<FormResponseValidationIssueDto> issues)
    {
        var ids = value as IReadOnlyList<Guid> ?? [];
        var settings = field.File;
        if (settings is not null && ids.Count > settings.MaxFiles)
        {
            issues.Add(Issue("MAX_FILES", field.Key, field.Key, $"الحد الأقصى للملفات {settings.MaxFiles}.", "Error"));
        }

        if (attachmentsById is null) return;
        foreach (var id in ids)
        {
            if (!attachmentsById.TryGetValue(id, out var attachment))
            {
                issues.Add(Issue("ATTACHMENT_UNAUTHORIZED", field.Key, field.Key, "مرفق غير مصرح.", "Error"));
                continue;
            }

            if (attachment.ScanStatus is AttachmentScanStatus.PendingScan or AttachmentScanStatus.Quarantined or AttachmentScanStatus.Rejected)
            {
                issues.Add(Issue("ATTACHMENT_SCAN", field.Key, field.Key, "حالة فحص المرفق غير مقبولة للإرسال.", "Error"));
            }
        }
    }

    private static void ValidateRepeating(
        FormFieldSchema field,
        object? value,
        FormResponseValidationMode mode,
        List<FormResponseValidationIssueDto> issues)
    {
        var rows = value as IReadOnlyList<Dictionary<string, object?>> ?? [];
        var settings = field.RepeatingTable;
        if (settings is not null && mode == FormResponseValidationMode.FullSubmit && rows.Count < settings.MinRows)
        {
            issues.Add(Issue("MIN_ROWS", field.Key, field.Key, $"الحد الأدنى للصفوف {settings.MinRows}.", "Error"));
        }

        if (settings is not null && rows.Count > settings.MaxRows)
        {
            issues.Add(Issue("MAX_ROWS", field.Key, field.Key, $"الحد الأقصى للصفوف {settings.MaxRows}.", "Error"));
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
}
