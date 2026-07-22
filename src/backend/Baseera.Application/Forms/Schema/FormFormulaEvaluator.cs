namespace Baseera.Application.Forms.Schema;

using System.Globalization;
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
            FormBinaryOperationNode bin => EvaluateBinary(bin.Operator, Evaluate(bin.Left, values), Evaluate(bin.Right, values)),
            FormFunctionCallNode fn => EvaluateFunction(fn.Function, fn.Arguments.Select(a => Evaluate(a, values)).ToList()),
            _ => null
        };
    }

    private static object? EvaluateBinary(FormFormulaBinaryOperator op, object? leftVal, object? rightVal)
    {
        var left = ToDecimal(leftVal);
        var right = ToDecimal(rightVal);
        if (left is null || right is null) return null;
        return op switch
        {
            FormFormulaBinaryOperator.Add => left + right,
            FormFormulaBinaryOperator.Subtract => left - right,
            FormFormulaBinaryOperator.Multiply => left * right,
            FormFormulaBinaryOperator.Divide => right == 0 ? null : left / right,
            FormFormulaBinaryOperator.Modulo => right == 0 ? null : left % right,
            _ => null
        };
    }

    private static object? EvaluateFunction(FormFormulaFunction function, IReadOnlyList<object?> arguments) =>
        function switch
        {
            FormFormulaFunction.Coalesce => EvaluateCoalesce(arguments),
            FormFormulaFunction.Concat => EvaluateConcat(arguments),
            _ => EvaluateNumericFunction(function, GetNumericArguments(arguments))
        };

    private static IReadOnlyList<decimal> GetNumericArguments(IReadOnlyList<object?> arguments) =>
        arguments.Select(ToDecimal).Where(x => x.HasValue).Select(x => x!.Value).ToList();

    private static object? EvaluateNumericFunction(FormFormulaFunction function, IReadOnlyList<decimal> numbers) =>
        function switch
        {
            FormFormulaFunction.Min => numbers.Count == 0 ? null : numbers.Min(),
            FormFormulaFunction.Max => numbers.Count == 0 ? null : numbers.Max(),
            FormFormulaFunction.Sum => numbers.Sum(),
            FormFormulaFunction.Average => numbers.Count == 0 ? null : numbers.Average(),
            FormFormulaFunction.Round => FirstOrNull(numbers) is { } round ? Math.Round(round) : null,
            FormFormulaFunction.Floor => FirstOrNull(numbers) is { } floor ? Math.Floor(floor) : null,
            FormFormulaFunction.Ceiling => FirstOrNull(numbers) is { } ceiling ? Math.Ceiling(ceiling) : null,
            FormFormulaFunction.Abs => FirstOrNull(numbers) is { } abs ? Math.Abs(abs) : null,
            _ => null
        };

    private static object? EvaluateCoalesce(IReadOnlyList<object?> arguments) =>
        arguments.FirstOrDefault(a => a is not null and not "");

    private static string EvaluateConcat(IReadOnlyList<object?> arguments) =>
        string.Concat(arguments.Select(ToSafeString));

    private static decimal? FirstOrNull(IReadOnlyList<decimal> numbers) =>
        numbers.Count == 0 ? null : numbers[0];

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

    private static string ToSafeString(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string s)
        {
            return s;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }
}
