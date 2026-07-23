namespace Baseera.Application.Forms.Responses;

using System.Text.Json;
using System.Text.Json.Nodes;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms;
using Baseera.Domain.Forms.Schema;

public interface IFormResponseProjectionService
{
    (string? AnswersJson, IReadOnlyDictionary<string, bool> Visibility, IReadOnlyDictionary<string, bool> Redacted)
        ProjectAnswers(
            FormSchemaDocument schema,
            ClassificationLevel formClassification,
            string? answersJson,
            bool canViewSensitive,
            bool isOwnerRespondent);
}

public sealed class FormResponseProjectionService : IFormResponseProjectionService
{
    public (string? AnswersJson, IReadOnlyDictionary<string, bool> Visibility, IReadOnlyDictionary<string, bool> Redacted)
        ProjectAnswers(
            FormSchemaDocument schema,
            ClassificationLevel formClassification,
            string? answersJson,
            bool canViewSensitive,
            bool isOwnerRespondent)
    {
        var visibility = new Dictionary<string, bool>(StringComparer.Ordinal);
        var redacted = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(answersJson))
        {
            return (answersJson, visibility, redacted);
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(answersJson);
        }
        catch (JsonException)
        {
            return ("{}", visibility, redacted);
        }

        if (root is not JsonObject obj)
        {
            return ("{}", visibility, redacted);
        }

        foreach (var field in schema.Pages.SelectMany(p => p.Sections).SelectMany(s => s.Fields))
        {
            var classification = field.ClassificationOverride ?? formClassification;
            var sensitive = FormAccessHelper.RequiresSensitive(classification);
            var allow = isOwnerRespondent || !sensitive || canViewSensitive;
            visibility[field.Key] = allow;
            redacted[field.Key] = sensitive && !allow;
            if (!allow && obj.ContainsKey(field.Key))
            {
                obj[field.Key] = "***";
            }
        }

        return (obj.ToJsonString(), visibility, redacted);
    }
}
