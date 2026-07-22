using System.Text.Json;
using Baseera.Domain.Forms.Schema;

namespace Baseera.UnitTests.Forms.Schema;

public sealed class FormSchemaEnumSerializationTests
{
    [Theory]
    [InlineData(FormFieldType.ShortText, 0)]
    [InlineData(FormConditionOperator.Before, 14)]
    [InlineData(FormFormulaFunction.Concat, 9)]
    public void Enum_values_round_trip_as_stable_numbers(Enum value, int expected) =>
        Assert.Equal(expected, Convert.ToInt32(value));

    [Fact]
    public void Serialized_schema_preserves_enum_numeric_values()
    {
        var json = JsonSerializer.Serialize(new
        {
            type = FormFieldType.Number,
            operatorValue = FormConditionOperator.GreaterThan,
            function = FormFormulaFunction.Sum
        });

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("type").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("operatorValue").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("function").GetInt32());
    }
}
