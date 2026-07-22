using System.Text.Json;
using Baseera.Application.Forms.Schema;
using Baseera.Domain.Forms.Schema;

namespace Baseera.UnitTests.Forms.Schema;

public sealed class FormSchemaCanonicalizerTests
{
    private readonly FormSchemaCanonicalizer _canonicalizer = new();

    [Fact]
    public void Hash_is_deterministic_regardless_of_input_property_order()
    {
        var pageId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var sectionId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var fieldId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

        var doc = new FormSchemaDocument
        {
            SchemaFormatVersion = 1,
            Pages =
            [
                new FormPageSchema
                {
                    Id = pageId,
                    Key = "page1",
                    TitleAr = "صفحة",
                    Order = 2,
                    Sections =
                    [
                        new FormSectionSchema
                        {
                            Id = sectionId,
                            Key = "section1",
                            TitleAr = "قسم",
                            Order = 5,
                            Fields =
                            [
                                new FormFieldSchema
                                {
                                    Id = fieldId,
                                    Key = "field1",
                                    Type = FormFieldType.ShortText,
                                    LabelAr = "اسم",
                                    Order = 9
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var jsonA = JsonSerializer.Serialize(doc, FormSchemaCanonicalizer.SerializerOptions);
        var jsonB = """{"pages":[{"sections":[{"fields":[{"labelAr":"اسم","type":0,"key":"field1","id":"cccccccc-cccc-4ccc-8ccc-cccccccccccc","order":9}],"titleAr":"قسم","key":"section1","id":"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb","order":5}],"titleAr":"صفحة","key":"page1","id":"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa","order":2}],"schemaFormatVersion":1}""";

        var a = _canonicalizer.Canonicalize(jsonA, requireMinimumContent: true);
        var b = _canonicalizer.Canonicalize(jsonB, requireMinimumContent: true);
        Assert.Equal(a.SchemaHash, b.SchemaHash);
        Assert.Equal(a.CanonicalJson, b.CanonicalJson);
        Assert.True(a.IsValid);
    }

    [Fact]
    public void Detects_dependency_cycle_in_formulas()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var doc = new FormSchemaDocument
        {
            SchemaFormatVersion = 1,
            Pages =
            [
                new FormPageSchema
                {
                    Id = pageId,
                    Key = "p",
                    TitleAr = "ص",
                    Order = 0,
                    Sections =
                    [
                        new FormSectionSchema
                        {
                            Id = sectionId,
                            Key = "s",
                            TitleAr = "ق",
                            Order = 0,
                            Fields =
                            [
                                new FormFieldSchema
                                {
                                    Id = aId,
                                    Key = "a",
                                    Type = FormFieldType.CalculatedNumber,
                                    LabelAr = "أ",
                                    Order = 0,
                                    IsCalculated = true,
                                    Formula = new FormFieldReferenceNode { FieldKey = "b" }
                                },
                                new FormFieldSchema
                                {
                                    Id = bId,
                                    Key = "b",
                                    Type = FormFieldType.CalculatedNumber,
                                    LabelAr = "ب",
                                    Order = 1,
                                    IsCalculated = true,
                                    Formula = new FormFieldReferenceNode { FieldKey = "a" }
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var result = _canonicalizer.Canonicalize(doc, requireMinimumContent: true);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code is "DependencyCycle" or "SelfReference");
    }

    [Fact]
    public void Rejects_nested_repeating_tables()
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
                                                Key = "nested",
                                                Type = FormFieldType.RepeatingTable,
                                                LabelAr = "متداخل",
                                                RepeatingTable = new FormRepeatingTableSettings()
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

        var result = _canonicalizer.Canonicalize(doc, requireMinimumContent: true);
        Assert.Contains(result.Issues, i => i.Code == "NestedRepeatingTable");
    }
}
