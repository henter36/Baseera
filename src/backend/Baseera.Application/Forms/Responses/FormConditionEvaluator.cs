namespace Baseera.Application.Forms.Responses;

using System.Collections;
using System.Globalization;
using System.Text.Json;
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

    private static IReadOnlyList<object?>? TryNormalizeCollection(object? value)
    {
        if (value is null || value is string)
        {
            return null;
        }

        if (value is JsonElement je)
        {
            if (je.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return je.EnumerateArray().Select(NormalizeJsonElement).Cast<object?>().ToList();
        }

        if (value is IEnumerable enumerable and not IEnumerable<char>)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(item);
            }

            return list;
        }

        return null;
    }

    private static object? NormalizeJsonElement(JsonElement je) => je.ValueKind switch
    {
        JsonValueKind.String => je.GetString(),
        JsonValueKind.Number => je.TryGetDecimal(out var d) ? d : je.GetRawText(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => je.GetRawText()
    };

    private static bool IsEmpty(object? value)
    {
        if (value is null) return true;
        if (value is string s) return string.IsNullOrWhiteSpace(s);
        if (value is JsonElement je)
        {
            return je.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                || (je.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(je.GetString()))
                || (je.ValueKind == JsonValueKind.Array && je.GetArrayLength() == 0);
        }

        var collection = TryNormalizeCollection(value);
        return collection is { Count: 0 };
    }

    private static bool IsTruthy(object? value) => value switch
    {
        bool b => b,
        string s => bool.TryParse(s, out var parsed) && parsed,
        JsonElement je when je.ValueKind == JsonValueKind.True => true,
        JsonElement je when je.ValueKind == JsonValueKind.False => false,
        _ => false
    };

    private static bool EqualsValue(object? raw, string? expected)
    {
        var collection = TryNormalizeCollection(raw);
        if (collection is not null)
        {
            return collection.Any(x => string.Equals(Normalize(x), expected ?? string.Empty, StringComparison.Ordinal));
        }

        return string.Equals(Normalize(raw), expected ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool ContainsValue(object? raw, string? expected)
    {
        if (expected is null) return false;
        var collection = TryNormalizeCollection(raw);
        if (collection is not null)
        {
            return collection.Any(x => string.Equals(Normalize(x), expected, StringComparison.Ordinal));
        }

        return Normalize(raw).Contains(expected, StringComparison.Ordinal);
    }

    private static bool InValues(object? raw, List<string>? values)
    {
        if (values is null || values.Count == 0) return false;
        var collection = TryNormalizeCollection(raw);
        if (collection is not null)
        {
            return collection.Any(item => values.Any(v => string.Equals(v, Normalize(item), StringComparison.Ordinal)));
        }

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
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.String => je.GetString() ?? string.Empty,
            JsonValueKind.Number => je.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => je.ToString()
        },
        _ => value.ToString() ?? string.Empty
    };
}
