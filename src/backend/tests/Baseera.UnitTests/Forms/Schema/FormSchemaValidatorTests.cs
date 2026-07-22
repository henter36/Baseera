using Baseera.Application.Forms.Schema;
using Baseera.Domain.Forms.Schema;

namespace Baseera.UnitTests.Forms.Schema;

public sealed class FormSchemaValidatorTests
{
    [Theory]
    [InlineData(FormFieldType.YesNo, FormConditionOperator.IsTrue, true)]
    [InlineData(FormFieldType.YesNo, FormConditionOperator.Before, false)]
    [InlineData(FormFieldType.Number, FormConditionOperator.GreaterThan, true)]
    [InlineData(FormFieldType.Number, FormConditionOperator.Contains, false)]
    [InlineData(FormFieldType.Date, FormConditionOperator.After, true)]
    [InlineData(FormFieldType.Date, FormConditionOperator.In, false)]
    [InlineData(FormFieldType.ShortText, FormConditionOperator.Contains, true)]
    [InlineData(FormFieldType.ShortText, FormConditionOperator.Before, false)]
    [InlineData(FormFieldType.SingleChoice, FormConditionOperator.In, true)]
    [InlineData(FormFieldType.SingleChoice, FormConditionOperator.GreaterThan, false)]
    public void Operator_compatibility_matrix(FormFieldType type, FormConditionOperator op, bool expected) =>
        Assert.Equal(expected, FormSchemaValidator.IsOperatorCompatible(type, op));

    [Fact]
    public void Rejects_undefined_operator_enum_value()
    {
        var invalid = (FormConditionOperator)999;
        Assert.False(FormSchemaValidator.IsOperatorCompatible(FormFieldType.ShortText, invalid));
    }

    [Fact]
    public void Rejects_binary_formula_without_operands()
    {
        var doc = MinimalDoc(new FormFieldSchema
        {
            Id = Guid.NewGuid(),
            Key = "calc",
            Type = FormFieldType.CalculatedNumber,
            LabelAr = "حساب",
            IsCalculated = true,
            Formula = new FormBinaryOperationNode
            {
                Operator = FormFormulaBinaryOperator.Add,
                Left = null!,
                Right = new FormConstantNumberNode { Value = 1 }
            }
        });

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);
        Assert.Contains(issues, i => i.Code == "MissingFormulaOperand");
    }

    [Fact]
    public void Rejects_field_reference_with_missing_field_key()
    {
        var doc = MinimalDoc(new FormFieldSchema
        {
            Id = Guid.NewGuid(),
            Key = "calc",
            Type = FormFieldType.CalculatedNumber,
            LabelAr = "حساب",
            IsCalculated = true,
            Formula = new FormFieldReferenceNode { FieldKey = "" }
        });

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);
        Assert.Contains(issues, i => i.Code == "MissingFormulaFieldKey");
    }

    [Fact]
    public void Rejects_formula_reference_to_unknown_field()
    {
        var doc = MinimalDoc(new FormFieldSchema
        {
            Id = Guid.NewGuid(),
            Key = "calc",
            Type = FormFieldType.CalculatedNumber,
            LabelAr = "حساب",
            IsCalculated = true,
            Formula = new FormFieldReferenceNode { FieldKey = "ghost" }
        });

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);
        Assert.Contains(issues, i => i.Code == "MissingFieldReference" && i.FieldKey == "ghost");
    }

    [Fact]
    public void Validates_repeating_column_required_condition_operator()
    {
        var doc = new FormSchemaDocument
        {
            SchemaFormatVersion = 1,
            Pages =
            [
                new FormPageSchema
                {
                    Id = Guid.NewGuid(),
                    Key = "p",
                    TitleAr = "ص",
                    Sections =
                    [
                        new FormSectionSchema
                        {
                            Id = Guid.NewGuid(),
                            Key = "s",
                            TitleAr = "ق",
                            Fields =
                            [
                                new FormFieldSchema
                                {
                                    Id = Guid.NewGuid(),
                                    Key = "yn",
                                    Type = FormFieldType.YesNo,
                                    LabelAr = "نعم/لا"
                                },
                                new FormFieldSchema
                                {
                                    Id = Guid.NewGuid(),
                                    Key = "table",
                                    Type = FormFieldType.RepeatingTable,
                                    LabelAr = "جدول",
                                    RepeatingTable = new FormRepeatingTableSettings
                                    {
                                        Columns =
                                        [
                                            new FormFieldSchema
                                            {
                                                Id = Guid.NewGuid(),
                                                Key = "col1",
                                                Type = FormFieldType.ShortText,
                                                LabelAr = "عمود",
                                                RequiredCondition = new FormConditionGroup
                                                {
                                                    Predicates =
                                                    [
                                                        new FormConditionPredicate
                                                        {
                                                            FieldKey = "yn",
                                                            Operator = FormConditionOperator.Before,
                                                            Value = "2020-01-01"
                                                        }
                                                    ]
                                                }
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

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);
        Assert.Contains(issues, i => i.Code == "OperatorTypeMismatch");
    }

    [Fact]
    public void Page_visibility_condition_is_counted_once()
    {
        var predicateCount = 3;
        var doc = DocWithPageVisibility(BuildFlatPredicates(predicateCount));

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);

        Assert.DoesNotContain(issues, i => i.Code == "TooManyConditionNodes");
        Assert.Equal(predicateCount, CountReportedConditionNodes(doc));
    }

    [Fact]
    public void Section_visibility_condition_is_counted_once()
    {
        var predicateCount = 3;
        var doc = DocWithSectionVisibility(BuildFlatPredicates(predicateCount));

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);

        Assert.DoesNotContain(issues, i => i.Code == "TooManyConditionNodes");
        Assert.Equal(predicateCount, CountReportedConditionNodes(doc));
    }

    [Fact]
    public void Allows_exactly_MaxConditionNodes()
    {
        var doc = DocWithPageVisibility(BuildFlatPredicates(FormSchemaValidator.MaxConditionNodes));

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);

        Assert.DoesNotContain(issues, i => i.Code == "TooManyConditionNodes");
    }

    [Fact]
    public void Rejects_MaxConditionNodes_plus_one()
    {
        var doc = DocWithPageVisibility(BuildFlatPredicates(FormSchemaValidator.MaxConditionNodes + 1));

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);

        Assert.Contains(issues, i => i.Code == "TooManyConditionNodes");
    }

    [Fact]
    public void Condition_depth_exceeded_reported_once()
    {
        var doc = DocWithPageVisibility(BuildNestedConditionDepth(FormSchemaValidator.MaxConditionDepth + 1));

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);

        Assert.Equal(1, issues.Count(i => i.Code == "ConditionDepthExceeded"));
    }

    [Fact]
    public void Enforces_page_limit()
    {
        var doc = new FormSchemaDocument
        {
            SchemaFormatVersion = 1,
            Pages = Enumerable.Range(0, FormSchemaValidator.MaxPages + 1)
                .Select(i => new FormPageSchema
                {
                    Id = Guid.NewGuid(),
                    Key = $"p{i}",
                    TitleAr = $"صفحة {i}",
                    Sections =
                    [
                        new FormSectionSchema
                        {
                            Id = Guid.NewGuid(),
                            Key = "s",
                            TitleAr = "قسم",
                            Fields =
                            [
                                new FormFieldSchema
                                {
                                    Id = Guid.NewGuid(),
                                    Key = "f",
                                    Type = FormFieldType.ShortText,
                                    LabelAr = "حقل"
                                }
                            ]
                        }
                    ]
                }).ToList()
        };

        var issues = FormSchemaValidator.Validate(doc, requireMinimumContent: true);
        Assert.Contains(issues, i => i.Code == "TooManyPages");
    }

    private static int CountReportedConditionNodes(FormSchemaDocument doc) =>
        new FormSchemaCanonicalizer().Canonicalize(doc, requireMinimumContent: true).ConditionCount;

    private static FormConditionGroup BuildFlatPredicates(int count) => new()
    {
        Predicates = Enumerable.Range(0, count)
            .Select(i => new FormConditionPredicate
            {
                FieldKey = "f",
                Operator = FormConditionOperator.Equals,
                Value = $"v{i}"
            })
            .ToList()
    };

    private static FormConditionGroup BuildNestedConditionDepth(int depth)
    {
        if (depth <= 0)
        {
            return new FormConditionGroup
            {
                Predicates =
                [
                    new FormConditionPredicate
                    {
                        FieldKey = "f",
                        Operator = FormConditionOperator.Equals,
                        Value = "x"
                    }
                ]
            };
        }

        return new FormConditionGroup
        {
            Groups = [BuildNestedConditionDepth(depth - 1)]
        };
    }

    private static FormSchemaDocument DocWithPageVisibility(FormConditionGroup condition)
    {
        var doc = MinimalDoc(new FormFieldSchema
        {
            Id = Guid.NewGuid(),
            Key = "f",
            Type = FormFieldType.ShortText,
            LabelAr = "حقل"
        });
        doc.Pages[0].VisibilityCondition = condition;
        return doc;
    }

    private static FormSchemaDocument DocWithSectionVisibility(FormConditionGroup condition)
    {
        var doc = MinimalDoc(new FormFieldSchema
        {
            Id = Guid.NewGuid(),
            Key = "f",
            Type = FormFieldType.ShortText,
            LabelAr = "حقل"
        });
        doc.Pages[0].Sections[0].VisibilityCondition = condition;
        return doc;
    }

    private static FormSchemaDocument MinimalDoc(FormFieldSchema field) => new()
    {
        SchemaFormatVersion = 1,
        Pages =
        [
            new FormPageSchema
            {
                Id = Guid.NewGuid(),
                Key = "p",
                TitleAr = "ص",
                Sections =
                [
                    new FormSectionSchema
                    {
                        Id = Guid.NewGuid(),
                        Key = "s",
                        TitleAr = "ق",
                        Fields = [field]
                    }
                ]
            }
        ]
    };
}
