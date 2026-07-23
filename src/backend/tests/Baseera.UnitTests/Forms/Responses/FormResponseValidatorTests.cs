using System.Text.Json;
using Baseera.Application.Forms.Responses;
using Baseera.Domain.Forms.Schema;

namespace Baseera.UnitTests.Forms.Responses;

public sealed class FormResponseValidatorTests
{
    private readonly FormResponseValidator _sut = new();

    private static FormSchemaDocument SimpleSchema() => new()
    {
        Pages =
        [
            new FormPageSchema
            {
                Key = "p1",
                TitleAr = "صفحة",
                Sections =
                [
                    new FormSectionSchema
                    {
                        Key = "s1",
                        TitleAr = "قسم",
                        Fields =
                        [
                            new FormFieldSchema
                            {
                                Key = "name",
                                Type = FormFieldType.ShortText,
                                LabelAr = "الاسم",
                                IsRequired = true,
                                Text = new FormTextFieldSettings { MinLength = 2, MaxLength = 50 }
                            },
                            new FormFieldSchema
                            {
                                Key = "score",
                                Type = FormFieldType.Number,
                                LabelAr = "درجة",
                                Number = new FormNumberFieldSettings { Min = 0, Max = 100 }
                            },
                            new FormFieldSchema
                            {
                                Key = "ok",
                                Type = FormFieldType.YesNo,
                                LabelAr = "موافق"
                            },
                            new FormFieldSchema
                            {
                                Key = "total",
                                Type = FormFieldType.CalculatedNumber,
                                LabelAr = "المجموع",
                                IsCalculated = true,
                                Formula = new FormFieldReferenceNode { FieldKey = "score" }
                            }
                        ]
                    }
                ]
            }
        ]
    };

    [Fact]
    public void Draft_allows_partial_but_blocks_unknown_and_calculated_write()
    {
        var schema = SimpleSchema();
        var answers = JsonDocument.Parse("""{"name":"أ","unknown":1,"total":9}""").RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.DraftPartial);
        Assert.Contains(result.Issues, i => i.Code == "UNKNOWN_FIELD");
        Assert.Contains(result.Issues, i => i.Code == "CALCULATED_WRITE");
    }

    [Fact]
    public void Submit_requires_visible_required_fields_and_computes_hash()
    {
        var schema = SimpleSchema();
        var answers = JsonDocument.Parse("""{"name":"أحمد","score":10,"ok":true}""").RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.FullSubmit);
        Assert.True(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.AnswersHash));
        Assert.Contains("name", result.VisibleFieldKeys);
        Assert.Equal(10m, Assert.IsType<decimal>(result.CalculatedValues["total"]));
    }

    [Fact]
    public void Submit_fails_when_required_missing()
    {
        var schema = SimpleSchema();
        var answers = JsonDocument.Parse("""{"score":5}""").RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.FullSubmit);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "REQUIRED" && i.FieldKey == "name");
    }

    [Fact]
    public void Conditional_visibility_hides_required()
    {
        var schema = SimpleSchema();
        schema.Pages[0].Sections[0].Fields[0].VisibilityCondition = new FormConditionGroup
        {
            Predicates = [new FormConditionPredicate { FieldKey = "ok", Operator = FormConditionOperator.IsTrue }]
        };
        var answers = JsonDocument.Parse("""{"ok":false,"score":1}""").RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.FullSubmit);
        Assert.True(result.IsValid);
        Assert.DoesNotContain("name", result.RequiredFieldKeys);
    }

    [Fact]
    public void Division_by_zero_yields_null_calculated()
    {
        var schema = SimpleSchema();
        schema.Pages[0].Sections[0].Fields[3].Formula = new FormBinaryOperationNode
        {
            Operator = FormFormulaBinaryOperator.Divide,
            Left = new FormConstantNumberNode { Value = 10 },
            Right = new FormConstantNumberNode { Value = 0 }
        };
        var answers = JsonDocument.Parse("""{"name":"أحمد","score":1,"ok":true}""").RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.FullSubmit);
        Assert.True(result.IsValid);
        Assert.Null(result.CalculatedValues["total"]);
    }

    [Fact]
    public void Arabic_labels_used_in_required_message()
    {
        var schema = SimpleSchema();
        var answers = JsonDocument.Parse("{}").RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.FullSubmit);
        var issue = Assert.Single(result.Issues, i => i.Code == "REQUIRED");
        Assert.Contains("الاسم", issue.MessageAr);
    }
}
