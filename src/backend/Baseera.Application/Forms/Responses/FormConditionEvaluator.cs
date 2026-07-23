namespace Baseera.Application.Forms.Responses;

using System.Globalization;
using Baseera.Domain.Forms.Schema;

public static class FormConditionEvaluator
{
    public static bool Evaluate(FormConditionGroup? group, IReadOnlyDictionary<string, object?> values)
    {
        if (group is null)
        {
            return true;
        }

        var predicateResults = group.Predicates.Select(p => EvaluatePredicate(p, values));
        var groupResults = group.Groups.Select(g => Evaluate(g, values));
        var results = predicateResults.Concat(groupResults).ToList();
        if (results.Count == 0)
        {
            return true;
        }

        return group.Combinator == FormConditionCombinator.All
            ? results.All(x => x)
            : results.Any(x => x);
    }

    private static bool EvaluatePredicate(FormConditionPredicate predicate, IReadOnlyDictionary<string, object?> values)
    {
        values.TryGetValue(predicate.FieldKey, out var raw);
        return predicate.Operator switch
        {
            FormConditionOperator.IsEmpty => IsEmpty(raw),
            FormConditionOperator.IsNotEmpty => !IsEmpty(raw),
            FormConditionOperator.IsTrue => IsTruthy(raw),
            FormConditionOperator.IsFalse => !IsTruthy(raw) && !IsEmpty(raw),
            FormConditionOperator.Equals => EqualsValue(raw, predicate.Value),
            FormConditionOperator.NotEquals => !EqualsValue(raw, predicate.Value),
            FormConditionOperator.Contains => ContainsValue(raw, predicate.Value),
            FormConditionOperator.NotContains => !ContainsValue(raw, predicate.Value),
            FormConditionOperator.In => InValues(raw, predicate.Values),
            FormConditionOperator.NotIn => !InValues(raw, predicate.Values),
            FormConditionOperator.GreaterThan => Compare(raw, predicate.Value) > 0,
            FormConditionOperator.GreaterThanOrEqual => Compare(raw, predicate.Value) >= 0,
            FormConditionOperator.LessThan => Compare(raw, predicate.Value) < 0,
            FormConditionOperator.LessThanOrEqual => Compare(raw, predicate.Value) <= 0,
            FormConditionOperator.Before => Compare(raw, predicate.Value) < 0,
            FormConditionOperator.After => Compare(raw, predicate.Value) > 0,
            _ => false
        };
    }

    private static bool IsEmpty(object? value) =>
        value is null
        || (value is string s && string.IsNullOrWhiteSpace(s))
        || (value is IEnumerable<object?> e && !e.Any())
        || (value is System.Text.Json.JsonElement je && IsJsonEmpty(je));

    private static bool IsJsonEmpty(System.Text.Json.JsonElement je) =>
        je.ValueKind is System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined
        || (je.ValueKind == System.Text.Json.JsonValueKind.String && string.IsNullOrWhiteSpace(je.GetString()))
        || (je.ValueKind == System.Text.Json.JsonValueKind.Array && je.GetArrayLength() == 0);

    private static bool IsTruthy(object? value) => value switch
    {
        bool b => b,
        string s => bool.TryParse(s, out var parsed) && parsed,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.True => true,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.False => false,
        _ => false
    };

    private static bool EqualsValue(object? raw, string? expected) =>
        string.Equals(Normalize(raw), expected ?? string.Empty, StringComparison.Ordinal);

    private static bool ContainsValue(object? raw, string? expected)
    {
        if (expected is null) return false;
        if (raw is IEnumerable<object?> list && raw is not string)
        {
            return list.Any(x => string.Equals(Normalize(x), expected, StringComparison.Ordinal));
        }

        return Normalize(raw).Contains(expected, StringComparison.Ordinal);
    }

    private static bool InValues(object? raw, List<string>? values)
    {
        if (values is null || values.Count == 0) return false;
        var normalized = Normalize(raw);
        return values.Any(v => string.Equals(v, normalized, StringComparison.Ordinal));
    }

    private static int Compare(object? raw, string? expected)
    {
        if (decimal.TryParse(Normalize(raw), NumberStyles.Number, CultureInfo.InvariantCulture, out var left)
            && decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var right))
        {
            return left.CompareTo(right);
        }

        if (DateTimeOffset.TryParse(Normalize(raw), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var leftDt)
            && DateTimeOffset.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var rightDt))
        {
            return leftDt.CompareTo(rightDt);
        }

        return string.CompareOrdinal(Normalize(raw), expected ?? string.Empty);
    }

    private static string Normalize(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        System.Text.Json.JsonElement je => je.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => je.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Number => je.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            _ => je.ToString()
        },
        _ => value.ToString() ?? string.Empty
    };
}
