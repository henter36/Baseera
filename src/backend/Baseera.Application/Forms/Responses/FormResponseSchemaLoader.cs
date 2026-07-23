namespace Baseera.Application.Forms.Responses;

using System.Text.Json;
using Baseera.Domain.Forms;
using Baseera.Domain.Forms.Schema;

public static class FormResponseSchemaLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static FormSchemaDocument Parse(string schemaJson)
    {
        var doc = JsonSerializer.Deserialize<FormSchemaDocument>(schemaJson, Options);
        return doc ?? throw new InvalidOperationException("تعذر قراءة مخطط النموذج المثبت.");
    }

    public static void EnsureSchemaHashMatches(FormResponse response, FormCycle cycle)
    {
        if (!string.Equals(response.SchemaHash, cycle.SchemaHash, StringComparison.Ordinal)
            || response.FormSchemaSnapshotId != cycle.FormSchemaSnapshotId)
        {
            throw new InvalidOperationException("تعارض سلامة مخطط الرد مع الدورة. تم تسجيل الحادثة.");
        }
    }
}
