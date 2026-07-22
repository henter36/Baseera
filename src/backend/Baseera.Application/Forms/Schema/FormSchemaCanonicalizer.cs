namespace Baseera.Application.Forms.Schema;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Baseera.Domain.Forms.Schema;

public interface IFormSchemaCanonicalizer
{
    FormSchemaCanonicalResult Canonicalize(string schemaJson, bool requireMinimumContent = false);
    FormSchemaCanonicalResult Canonicalize(FormSchemaDocument document, bool requireMinimumContent = false);
}

public sealed class FormSchemaCanonicalizer : IFormSchemaCanonicalizer
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public FormSchemaCanonicalResult Canonicalize(string schemaJson, bool requireMinimumContent = false)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            throw new ArgumentException("مخطط النموذج فارغ.");
        }

        var utf8 = Encoding.UTF8.GetByteCount(schemaJson);
        if (utf8 > FormSchemaValidator.MaxSchemaBytes)
        {
            throw new ArgumentException("حجم مخطط النموذج يتجاوز الحد المسموح.");
        }

        FormSchemaDocument document;
        try
        {
            document = JsonSerializer.Deserialize<FormSchemaDocument>(schemaJson, SerializerOptions)
                       ?? throw new ArgumentException("مخطط النموذج غير صالح.");
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("مخطط النموذج غير صالح.", ex);
        }

        return Canonicalize(document, requireMinimumContent);
    }

    public FormSchemaCanonicalResult Canonicalize(FormSchemaDocument document, bool requireMinimumContent = false)
    {
        var normalized = Normalize(document);
        var issues = FormSchemaValidator.Validate(normalized, requireMinimumContent);
        var canonicalJson = JsonSerializer.Serialize(normalized, SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(canonicalJson);
        if (bytes.Length > FormSchemaValidator.MaxSchemaBytes)
        {
            issues.Add(new FormSchemaValidationIssue
            {
                Code = "SchemaTooLarge",
                Path = "$",
                MessageAr = "حجم مخطط النموذج يتجاوز الحد المسموح."
            });
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var pageCount = normalized.Pages.Count;
        var sectionCount = normalized.Pages.Sum(p => p.Sections.Count);
        var fields = normalized.Pages.SelectMany(p => p.Sections).SelectMany(s => s.Fields).ToList();
        var fieldCount = CountFields(fields);
        var calculatedCount = CountCalculated(fields);
        var conditionCount = CountConditions(normalized);

        return new FormSchemaCanonicalResult
        {
            Document = normalized,
            CanonicalJson = canonicalJson,
            SchemaHash = hash,
            SchemaSizeBytes = bytes.Length,
            PageCount = pageCount,
            SectionCount = sectionCount,
            FieldCount = fieldCount,
            CalculatedFieldCount = calculatedCount,
            ConditionCount = conditionCount,
            Issues = issues
        };
    }

    private static FormSchemaDocument Normalize(FormSchemaDocument document)
    {
        document.Pages = document.Pages
            .OrderBy(p => p.Order).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select((p, i) =>
            {
                p.Order = i;
                p.VisibilityCondition = NormalizeConditionGroup(p.VisibilityCondition);
                p.Sections = p.Sections
                    .OrderBy(s => s.Order).ThenBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
                    .Select((s, si) =>
                    {
                        s.Order = si;
                        s.VisibilityCondition = NormalizeConditionGroup(s.VisibilityCondition);
                        s.Fields = s.Fields
                            .OrderBy(f => f.Order).ThenBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
                            .Select((f, fi) => NormalizeField(f, fi))
                            .ToList();
                        return s;
                    }).ToList();
                return p;
            }).ToList();
        return document;
    }

    private static FormFieldSchema NormalizeField(FormFieldSchema field, int order)
    {
        field.Order = order;
        field.VisibilityCondition = NormalizeConditionGroup(field.VisibilityCondition);
        field.RequiredCondition = NormalizeConditionGroup(field.RequiredCondition);
        if (field.Choice?.Options is { Count: > 0 })
        {
            field.Choice.Options = field.Choice.Options
                .OrderBy(o => o.Order).ThenBy(o => o.Value, StringComparer.OrdinalIgnoreCase)
                .Select((o, i) => { o.Order = i; return o; })
                .ToList();
        }

        if (field.RepeatingTable?.Columns is { Count: > 0 })
        {
            field.RepeatingTable.Columns = field.RepeatingTable.Columns
                .OrderBy(c => c.Order).ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .Select((c, i) => NormalizeField(c, i))
                .ToList();
        }

        return field;
    }

    private static FormConditionGroup? NormalizeConditionGroup(FormConditionGroup? group)
    {
        if (group is null)
        {
            return null;
        }

        group.Predicates = group.Predicates
            .OrderBy(p => p.FieldKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Operator)
            .ThenBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Values is null ? string.Empty : string.Join('\u001f', p.Values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        group.Groups = group.Groups
            .Select(NormalizeConditionGroup)
            .Where(g => g is not null)
            .OrderBy(g => ConditionGroupSortKey(g!))
            .ToList()!;
        return group;
    }

    private static string ConditionGroupSortKey(FormConditionGroup group) =>
        JsonSerializer.Serialize(group, SerializerOptions);

    private static int CountFields(IEnumerable<FormFieldSchema> fields) =>
        fields.Sum(f => 1 + (f.RepeatingTable?.Columns.Count ?? 0));

    private static int CountCalculated(IEnumerable<FormFieldSchema> fields) =>
        fields.Sum(f =>
            (f.IsCalculated ? 1 : 0) +
            (f.RepeatingTable?.Columns.Count(c => c.IsCalculated) ?? 0));

    private static int CountConditions(FormSchemaDocument document)
    {
        var count = 0;
        void Walk(FormConditionGroup? g)
        {
            if (g is null) return;
            count += g.Predicates.Count;
            foreach (var n in g.Groups) Walk(n);
        }

        foreach (var page in document.Pages)
        {
            Walk(page.VisibilityCondition);
            foreach (var section in page.Sections)
            {
                Walk(section.VisibilityCondition);
                foreach (var field in section.Fields)
                {
                    Walk(field.VisibilityCondition);
                    Walk(field.RequiredCondition);
                    if (field.RepeatingTable is not null)
                    {
                        foreach (var col in field.RepeatingTable.Columns)
                        {
                            Walk(col.VisibilityCondition);
                            Walk(col.RequiredCondition);
                        }
                    }
                }
            }
        }

        return count;
    }
}
