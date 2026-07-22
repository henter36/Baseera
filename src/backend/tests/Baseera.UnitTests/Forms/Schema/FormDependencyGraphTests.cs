using Baseera.Application.Forms.Schema;
using Baseera.Domain.Forms.Schema;

namespace Baseera.UnitTests.Forms.Schema;

public sealed class FormDependencyGraphTests
{
    [Fact]
    public void Reports_cycle_for_a_to_b_to_a_but_not_unrelated_dependent_c()
    {
        var doc = BuildDoc(
            ("a", "b"),
            ("b", "a"),
            ("c", "a"));

        var fields = IndexFields(doc);
        var issues = FormDependencyGraph.DetectCyclesAndMissingRefs(doc, fields);

        Assert.Contains(issues, i => i.Code == "DependencyCycle");
        Assert.DoesNotContain(issues, i => i.Code == "DependencyCycle" && string.Equals(i.FieldKey, "c", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Detects_missing_reference_in_repeating_column_required_condition()
    {
        var tableId = Guid.NewGuid();
        var colId = Guid.NewGuid();
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
                                    Id = tableId,
                                    Key = "table",
                                    Type = FormFieldType.RepeatingTable,
                                    LabelAr = "جدول",
                                    RepeatingTable = new FormRepeatingTableSettings
                                    {
                                        Columns =
                                        [
                                            new FormFieldSchema
                                            {
                                                Id = colId,
                                                Key = "col1",
                                                Type = FormFieldType.ShortText,
                                                LabelAr = "عمود",
                                                RequiredCondition = new FormConditionGroup
                                                {
                                                    Predicates =
                                                    [
                                                        new FormConditionPredicate
                                                        {
                                                            FieldKey = "missing",
                                                            Operator = FormConditionOperator.Equals,
                                                            Value = "x"
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

        var issues = FormDependencyGraph.DetectCyclesAndMissingRefs(doc, IndexFields(doc));
        Assert.Contains(issues, i => i.Code == "MissingFieldReference" && i.FieldKey == "missing");
    }

    private static FormSchemaDocument BuildDoc(params (string Key, string Ref)[] fields)
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
                            Fields = fields.Select((f, i) => Calc(f.Key, f.Ref, i)).ToList()
                        }
                    ]
                }
            ]
        };
    }

    private static FormFieldSchema Calc(string key, string reference, int order = 0) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Type = FormFieldType.CalculatedNumber,
        LabelAr = key,
        Order = order,
        IsCalculated = true,
        Formula = new FormFieldReferenceNode { FieldKey = reference }
    };

    private static Dictionary<string, FormFieldSchema> IndexFields(FormSchemaDocument doc) =>
        doc.Pages.SelectMany(p => p.Sections).SelectMany(s => s.Fields)
            .ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
}
