using Baseera.Application.Forms.Schema;
using Baseera.Domain.Forms.Schema;

namespace Baseera.UnitTests.Forms.Schema;

public sealed class FormFormulaEvaluatorTests
{
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

        var result = FormFormulaEvaluator.Evaluate(node, new Dictionary<string, object?> { ["n"] = 3m });
        Assert.Equal(6m, result);
    }
}
