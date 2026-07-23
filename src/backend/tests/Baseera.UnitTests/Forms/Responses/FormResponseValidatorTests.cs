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

    [Theory]
    [InlineData("""{"name":"أ","ok":null}""")]
    [InlineData("""{"name":"أ","ok":true}""")]
    [InlineData("""{"name":"أ","ok":false}""")]
    public void YesNo_null_and_boolean_coercion(string json)
    {
        var schema = SimpleSchema();
        var answers = JsonDocument.Parse(json).RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.DraftPartial);
        Assert.DoesNotContain(result.Issues, i => i.Code == "TYPE_MISMATCH" && i.FieldKey == "ok");
    }

    [Theory]
    [InlineData("""{"name":"أ","ok":"yes"}""")]
    [InlineData("""{"name":"أ","ok":1}""")]
    public void YesNo_wrong_type_reports_type_mismatch(string json)
    {
        var schema = SimpleSchema();
        var answers = JsonDocument.Parse(json).RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.DraftPartial);
        Assert.Contains(result.Issues, i => i.Code == "TYPE_MISMATCH" && i.FieldKey == "ok");
    }

    [Theory]
    [InlineData("""{"name":"أ","score":null}""")]
    [InlineData("""{"name":null,"score":1}""")]
    public void Nullable_string_fields_accept_null(string json)
    {
        var schema = SimpleSchema();
        var answers = JsonDocument.Parse(json).RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.DraftPartial);
        Assert.DoesNotContain(result.Issues, i => i.Code == "TYPE_MISMATCH");
    }

    [Fact]
    public void Repeating_row_id_non_string_reports_type_mismatch()
    {
        var schema = RepeatingSchema();
        var answers = JsonDocument.Parse("""{"rows":[{"_rowId":1,"col1":"x"}]}""").RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.DraftPartial);
        Assert.Contains(result.Issues, i => i.Code == "TYPE_MISMATCH" && i.Path.Contains("_rowId"));
    }

    [Fact]
    public void Repeating_row_id_empty_reports_invalid_row_id()
    {
        var schema = RepeatingSchema();
        var answers = JsonDocument.Parse("""{"rows":[{"_rowId":"","col1":"x"}]}""").RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.DraftPartial);
        Assert.Contains(result.Issues, i => i.Code == "INVALID_ROW_ID");
    }

    [Fact]
    public void Repeating_duplicate_row_id_reports_duplicate_row()
    {
        var schema = RepeatingSchema();
        var answers = JsonDocument.Parse("""
            {"rows":[{"_rowId":"r1","col1":"a"},{"_rowId":"r1","col1":"b"}]}
            """).RootElement;
        var result = _sut.Validate(schema, answers, FormResponseValidationMode.DraftPartial);
        Assert.Contains(result.Issues, i => i.Code == "DUPLICATE_ROW");
    }

    private static FormSchemaDocument RepeatingSchema() => new()
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
                                Key = "rows",
                                Type = FormFieldType.RepeatingTable,
                                LabelAr = "جدول",
                                RepeatingTable = new FormRepeatingTableSettings
                                {
                                    Columns =
                                    [
                                        new FormFieldSchema
                                        {
                                            Key = "col1",
                                            Type = FormFieldType.ShortText,
                                            LabelAr = "عمود"
                                        }
                                    ]
                                }
                            }
                        ]
                    }
                ]
            }
        ]
    };
}
