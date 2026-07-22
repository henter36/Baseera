using Baseera.Application.Forms.Schema;
using Baseera.Domain.Forms.Schema;

namespace Baseera.UnitTests.Forms.Schema;

public sealed class FormSchemaCanonicalizerConditionTests
{
    private readonly FormSchemaCanonicalizer _canonicalizer = new();

    [Fact]
    public void Predicate_order_does_not_change_hash()
    {
        var pageId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var fieldA = Guid.NewGuid();
        var fieldB = Guid.NewGuid();

        var docA = BuildConditionDoc(pageId, sectionId, fieldA, fieldB, ["b", "a"]);
        var docB = BuildConditionDoc(pageId, sectionId, fieldA, fieldB, ["a", "b"]);

        var a = _canonicalizer.Canonicalize(docA, requireMinimumContent: true);
        var b = _canonicalizer.Canonicalize(docB, requireMinimumContent: true);

        Assert.Equal(a.SchemaHash, b.SchemaHash);
    }

    [Fact]
    public void Operator_change_changes_hash()
    {
        var pageId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var fieldA = Guid.NewGuid();
        var fieldB = Guid.NewGuid();

        var equalsDoc = BuildConditionDoc(pageId, sectionId, fieldA, fieldB, ["a"], FormConditionOperator.Equals);
        var notEqualsDoc = BuildConditionDoc(pageId, sectionId, fieldA, fieldB, ["a"], FormConditionOperator.NotEquals);

        var equals = _canonicalizer.Canonicalize(equalsDoc, requireMinimumContent: true);
        var notEquals = _canonicalizer.Canonicalize(notEqualsDoc, requireMinimumContent: true);

        Assert.NotEqual(equals.SchemaHash, notEquals.SchemaHash);
    }

    [Fact]
    public void Repeating_column_required_condition_counts_toward_hash()
    {
        var withRequired = BuildRepeatingRequiredDoc(true);
        var withoutRequired = BuildRepeatingRequiredDoc(false);

        var a = _canonicalizer.Canonicalize(withRequired, requireMinimumContent: true);
        var b = _canonicalizer.Canonicalize(withoutRequired, requireMinimumContent: true);

        Assert.NotEqual(a.SchemaHash, b.SchemaHash);
        Assert.True(a.ConditionCount > b.ConditionCount);
    }

    private static FormSchemaDocument BuildConditionDoc(
        Guid pageId,
        Guid sectionId,
        Guid fieldA,
        Guid fieldB,
        IReadOnlyList<string> predicateOrder,
        FormConditionOperator op = FormConditionOperator.Equals)
    {
        return new FormSchemaDocument
        {
            SchemaFormatVersion = 1,
            Pages =
            [
                new FormPageSchema
                {
                    Id = pageId,
                    Key = "p",
                    TitleAr = "ص",
                    Sections =
                    [
                        new FormSectionSchema
                        {
                            Id = sectionId,
                            Key = "s",
                            TitleAr = "ق",
                            Fields =
                            [
                                new FormFieldSchema
                                {
                                    Id = fieldA,
                                    Key = "a",
                                    Type = FormFieldType.ShortText,
                                    LabelAr = "أ"
                                },
                                new FormFieldSchema
                                {
                                    Id = fieldB,
                                    Key = "b",
                                    Type = FormFieldType.ShortText,
                                    LabelAr = "ب",
                                    VisibilityCondition = new FormConditionGroup
                                    {
                                        Predicates = predicateOrder.Select(k => new FormConditionPredicate
                                        {
                                            FieldKey = k,
                                            Operator = op,
                                            Value = "x"
                                        }).ToList()
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static FormSchemaDocument BuildRepeatingRequiredDoc(bool includeRequired)
    {
        return new FormSchemaDocument
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
                                    Key = "src",
                                    Type = FormFieldType.YesNo,
                                    LabelAr = "مصدر"
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
                                                RequiredCondition = includeRequired
                                                    ? new FormConditionGroup
                                                    {
                                                        Predicates =
                                                        [
                                                            new FormConditionPredicate
                                                            {
                                                                FieldKey = "src",
                                                                Operator = FormConditionOperator.IsTrue
                                                            }
                                                        ]
                                                    }
                                                    : null
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
}
