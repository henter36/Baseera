namespace Baseera.Application.Forms.Schema;

using Baseera.Domain.Forms.Schema;

public static class FormFormulaEvaluator
{
    public static object? Evaluate(FormFormulaNode? node, IReadOnlyDictionary<string, object?> values)
    {
        if (node is null) return null;
        return node switch
        {
            FormConstantNumberNode n => n.Value,
            FormConstantTextNode t => t.Value,
            FormFieldReferenceNode fr => values.TryGetValue(fr.FieldKey, out var v) ? v : null,
            FormBinaryOperationNode bin => EvaluateBinary(bin, values),
            FormFunctionCallNode fn => EvaluateFunction(fn, values),
            _ => null
        };
    }

    private static object? EvaluateBinary(FormBinaryOperationNode bin, IReadOnlyDictionary<string, object?> values)
    {
        var left = ToDecimal(Evaluate(bin.Left, values));
        var right = ToDecimal(Evaluate(bin.Right, values));
        if (left is null || right is null) return null;
        return bin.Operator switch
        {
            FormFormulaBinaryOperator.Add => left + right,
            FormFormulaBinaryOperator.Subtract => left - right,
            FormFormulaBinaryOperator.Multiply => left * right,
            FormFormulaBinaryOperator.Divide => right == 0 ? null : left / right,
            FormFormulaBinaryOperator.Modulo => right == 0 ? null : left % right,
            _ => null
        };
    }

    private static object? EvaluateFunction(FormFunctionCallNode fn, IReadOnlyDictionary<string, object?> values)
    {
        var args = fn.Arguments.Select(a => Evaluate(a, values)).ToList();
        var nums = args.Select(ToDecimal).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return fn.Function switch
        {
            FormFormulaFunction.Min => nums.Count == 0 ? null : nums.Min(),
            FormFormulaFunction.Max => nums.Count == 0 ? null : nums.Max(),
            FormFormulaFunction.Sum => nums.Sum(),
            FormFormulaFunction.Average => nums.Count == 0 ? null : nums.Average(),
            FormFormulaFunction.Round => nums.Count == 0 ? null : Math.Round(nums[0]),
            FormFormulaFunction.Floor => nums.Count == 0 ? null : Math.Floor(nums[0]),
            FormFormulaFunction.Ceiling => nums.Count == 0 ? null : Math.Ceiling(nums[0]),
            FormFormulaFunction.Abs => nums.Count == 0 ? null : Math.Abs(nums[0]),
            FormFormulaFunction.Coalesce => args.FirstOrDefault(a => a is not null and not ""),
            FormFormulaFunction.Concat => string.Concat(args.Select(a => a?.ToString() ?? string.Empty)),
            _ => null
        };
    }

    private static decimal? ToDecimal(object? value) => value switch
    {
        null => null,
        decimal d => d,
        int i => i,
        long l => l,
        double db => (decimal)db,
        float f => (decimal)f,
        string s when decimal.TryParse(s, out var parsed) => parsed,
        _ => null
    };
}
