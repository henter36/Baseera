using System.Text.Json;
using Baseera.Application.Forms.Responses;
using Baseera.Domain.Forms.Schema;

namespace Baseera.UnitTests.Forms.Responses;

public sealed class FormConditionEvaluatorTests
{
    private static FormConditionGroup Group(
        FormConditionOperator op,
        string? value = null,
        List<string>? values = null) =>
        new()
        {
            Predicates =
            [
                new FormConditionPredicate
                {
                    FieldKey = "tags",
                    Operator = op,
                    Value = value,
                    Values = values ?? (value is not null && op is FormConditionOperator.In or FormConditionOperator.NotIn
                        ? [value]
                        : null)
                }
            ]
        };

    [Theory]
    [InlineData(FormConditionOperator.Contains, true)]
    [InlineData(FormConditionOperator.NotContains, false)]
    [InlineData(FormConditionOperator.In, true)]
    [InlineData(FormConditionOperator.NotIn, false)]
    public void List_string_multi_select_operators(FormConditionOperator op, bool expected)
    {
        var values = new Dictionary<string, object?> { ["tags"] = new List<string> { "أ", "ب" } };
        Assert.Equal(expected, FormConditionEvaluator.Evaluate(Group(op, "أ"), values));
    }

    [Theory]
    [InlineData(FormConditionOperator.Contains, true)]
    [InlineData(FormConditionOperator.In, true)]
    public void String_array_multi_select_operators(FormConditionOperator op, bool expected)
    {
        var values = new Dictionary<string, object?> { ["tags"] = new[] { "x", "y" } };
        Assert.Equal(expected, FormConditionEvaluator.Evaluate(Group(op, "y"), values));
    }

    [Fact]
    public void JsonElement_array_supports_contains_and_in()
    {
        using var doc = JsonDocument.Parse("""{"tags":["one","two"]}""");
        var values = new Dictionary<string, object?> { ["tags"] = doc.RootElement.GetProperty("tags") };
        Assert.True(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.Contains, "two"), values));
        Assert.True(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.In, "one"), values));
        Assert.False(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.NotContains, "one"), values));
    }

    [Fact]
    public void Empty_collection_is_empty_for_is_empty_and_fails_contains()
    {
        var values = new Dictionary<string, object?> { ["tags"] = Array.Empty<string>() };
        Assert.True(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.IsEmpty), values));
        Assert.False(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.Contains, "x"), values));
        Assert.False(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.In, "x", ["x"]), values));
    }

    [Fact]
    public void Null_field_is_empty_and_not_in()
    {
        var values = new Dictionary<string, object?> { ["tags"] = null };
        Assert.True(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.IsEmpty), values));
        Assert.False(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.In, "x", ["x"]), values));
    }

    [Fact]
    public void Single_string_uses_substring_semantics_for_contains()
    {
        var values = new Dictionary<string, object?> { ["tags"] = "alpha-beta" };
        Assert.True(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.Contains, "beta"), values));
        Assert.False(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.NotContains, "alpha"), values));
        Assert.True(FormConditionEvaluator.Evaluate(Group(FormConditionOperator.In, "alpha-beta"), values));
    }
}
