using Baseera.Application.Forms.Schema;
using Baseera.Domain.Forms.Schema;

namespace Baseera.UnitTests.Forms.Schema;

public sealed class FormFormulaEvaluatorTests
{
    private static readonly Dictionary<string, object?> Values = new()
    {
        ["n"] = 3m,
        ["a"] = 10m,
        ["b"] = 4m,
        ["t"] = "hello",
        ["u"] = " world"
    };

    [Fact]
    public void Evaluates_binary_and_functions()
    {
        var node = new FormBinaryOperationNode
        {
            Operator = FormFormulaBinaryOperator.Add,
            Left = new FormConstantNumberNode { Value = 2 },
            Right = new FormFunctionCallNode
            {
                Function = FormFormulaFunction.Sum,
                Arguments =
                [
                    new FormFieldReferenceNode { FieldKey = "n" },
                    new FormConstantNumberNode { Value = 1 }
                ]
            }
        };

        var result = FormFormulaEvaluator.Evaluate(node, Values);
        Assert.Equal(6m, result);
    }

    [Theory]
    [InlineData(FormFormulaBinaryOperator.Add, 10, 4, 14)]
    [InlineData(FormFormulaBinaryOperator.Subtract, 10, 4, 6)]
    [InlineData(FormFormulaBinaryOperator.Multiply, 10, 4, 40)]
    [InlineData(FormFormulaBinaryOperator.Divide, 10, 4, 2.5)]
    [InlineData(FormFormulaBinaryOperator.Modulo, 10, 4, 2)]
    public void Evaluates_all_binary_operators(FormFormulaBinaryOperator op, decimal left, decimal right, decimal expected)
    {
        var node = new FormBinaryOperationNode
        {
            Operator = op,
            Left = new FormConstantNumberNode { Value = left },
            Right = new FormConstantNumberNode { Value = right }
        };

        var result = FormFormulaEvaluator.Evaluate(node, Values);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(FormFormulaFunction.Min, 4)]
    [InlineData(FormFormulaFunction.Max, 10)]
    [InlineData(FormFormulaFunction.Sum, 14)]
    [InlineData(FormFormulaFunction.Average, 7)]
    public void Evaluates_aggregate_functions(FormFormulaFunction fn, decimal expected)
    {
        var node = new FormFunctionCallNode
        {
            Function = fn,
            Arguments =
            [
                new FormFieldReferenceNode { FieldKey = "a" },
                new FormFieldReferenceNode { FieldKey = "b" }
            ]
        };

        var result = FormFormulaEvaluator.Evaluate(node, Values);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(FormFormulaFunction.Round, 3.6, 4)]
    [InlineData(FormFormulaFunction.Floor, 3.9, 3)]
    [InlineData(FormFormulaFunction.Ceiling, 3.1, 4)]
    [InlineData(FormFormulaFunction.Abs, -7, 7)]
    public void Evaluates_unary_numeric_functions(FormFormulaFunction fn, decimal input, decimal expected)
    {
        var node = new FormFunctionCallNode
        {
            Function = fn,
            Arguments = [new FormConstantNumberNode { Value = input }]
        };

        var result = FormFormulaEvaluator.Evaluate(node, Values);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Coalesce_returns_first_non_empty_value()
    {
        var node = new FormFunctionCallNode
        {
            Function = FormFormulaFunction.Coalesce,
            Arguments =
            [
                new FormConstantTextNode { Value = "" },
                new FormConstantTextNode { Value = null! },
                new FormFieldReferenceNode { FieldKey = "t" }
            ]
        };

        var result = FormFormulaEvaluator.Evaluate(node, Values);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Concat_joins_values_safely()
    {
        var node = new FormFunctionCallNode
        {
            Function = FormFormulaFunction.Concat,
            Arguments =
            [
                new FormFieldReferenceNode { FieldKey = "t" },
                new FormFieldReferenceNode { FieldKey = "u" },
                new FormConstantNumberNode { Value = 42 }
            ]
        };

        var result = FormFormulaEvaluator.Evaluate(node, Values);
        Assert.Equal("hello world42", result);
    }

    [Fact]
    public void Null_node_returns_null()
    {
        Assert.Null(FormFormulaEvaluator.Evaluate(null, Values));
    }

    [Fact]
    public void Binary_with_null_operand_returns_null()
    {
        var node = new FormBinaryOperationNode
        {
            Operator = FormFormulaBinaryOperator.Add,
            Left = new FormFieldReferenceNode { FieldKey = "missing" },
            Right = new FormConstantNumberNode { Value = 1 }
        };

        Assert.Null(FormFormulaEvaluator.Evaluate(node, Values));
    }

    [Fact]
    public void Divide_by_zero_returns_null()
    {
        var node = new FormBinaryOperationNode
        {
            Operator = FormFormulaBinaryOperator.Divide,
            Left = new FormConstantNumberNode { Value = 10 },
            Right = new FormConstantNumberNode { Value = 0 }
        };

        Assert.Null(FormFormulaEvaluator.Evaluate(node, Values));
    }

    [Fact]
    public void Modulo_by_zero_returns_null()
    {
        var node = new FormBinaryOperationNode
        {
            Operator = FormFormulaBinaryOperator.Modulo,
            Left = new FormConstantNumberNode { Value = 10 },
            Right = new FormConstantNumberNode { Value = 0 }
        };

        Assert.Null(FormFormulaEvaluator.Evaluate(node, Values));
    }

    [Fact]
    public void Aggregate_functions_with_no_numeric_args_return_null()
    {
        var node = new FormFunctionCallNode
        {
            Function = FormFormulaFunction.Min,
            Arguments = [new FormConstantTextNode { Value = "x" }]
        };

        Assert.Null(FormFormulaEvaluator.Evaluate(node, Values));
    }
}
