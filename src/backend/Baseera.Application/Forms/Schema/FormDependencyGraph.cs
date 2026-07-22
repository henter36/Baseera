namespace Baseera.Application.Forms.Schema;

using Baseera.Domain.Forms.Schema;

public static class FormDependencyGraph
{
    public static IReadOnlyList<FormSchemaValidationIssue> DetectCyclesAndMissingRefs(
        FormSchemaDocument document,
        IReadOnlyDictionary<string, FormFieldSchema> fieldsByKey)
    {
        var issues = new List<FormSchemaValidationIssue>();
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in fieldsByKey.Keys)
        {
            graph.TryAdd(key, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        WalkDocument(document, fieldsByKey, graph, issues);
        DetectCycles(graph, issues);
        return issues;
    }

    private static void WalkDocument(
        FormSchemaDocument document,
        IReadOnlyDictionary<string, FormFieldSchema> fieldsByKey,
        Dictionary<string, HashSet<string>> graph,
        List<FormSchemaValidationIssue> issues)
    {
        foreach (var page in document.Pages)
        {
            var owner = $"page:{page.Key}";
            graph.TryAdd(owner, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            WalkCondition(owner, page.VisibilityCondition, $"pages[{page.Key}].visibility", page.Id, fieldsByKey, graph, issues);
            foreach (var section in page.Sections)
            {
                var sectionOwner = $"section:{section.Key}";
                graph.TryAdd(sectionOwner, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                WalkCondition(sectionOwner, section.VisibilityCondition, $"sections[{section.Key}].visibility", section.Id, fieldsByKey, graph, issues);
                foreach (var field in section.Fields)
                {
                    WalkFieldDependencies(field, fieldsByKey, graph, issues);
                }
            }
        }
    }

    private static void WalkFieldDependencies(
        FormFieldSchema field,
        IReadOnlyDictionary<string, FormFieldSchema> fieldsByKey,
        Dictionary<string, HashSet<string>> graph,
        List<FormSchemaValidationIssue> issues)
    {
        WalkCondition(field.Key, field.VisibilityCondition, $"fields[{field.Key}].visibility", field.Id, fieldsByKey, graph, issues);
        WalkCondition(field.Key, field.RequiredCondition, $"fields[{field.Key}].required", field.Id, fieldsByKey, graph, issues);
        WalkFormula(field.Key, field.Formula, $"fields[{field.Key}].formula", field.Id, fieldsByKey, graph, issues);
        if (field.RepeatingTable is null)
        {
            return;
        }

        foreach (var col in field.RepeatingTable.Columns)
        {
            var colPath = $"fields[{field.Key}].columns[{col.Key}]";
            WalkCondition(col.Key, col.VisibilityCondition, $"{colPath}.visibility", col.Id, fieldsByKey, graph, issues);
            WalkCondition(col.Key, col.RequiredCondition, $"{colPath}.required", col.Id, fieldsByKey, graph, issues);
            WalkFormula(col.Key, col.Formula, $"{colPath}.formula", col.Id, fieldsByKey, graph, issues);
        }
    }

    private static void AddEdge(
        string from,
        string to,
        string path,
        Guid? entityId,
        IReadOnlyDictionary<string, FormFieldSchema> fieldsByKey,
        Dictionary<string, HashSet<string>> graph,
        List<FormSchemaValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            return;
        }

        if (!fieldsByKey.ContainsKey(to))
        {
            issues.Add(new FormSchemaValidationIssue
            {
                Code = "MissingFieldReference",
                Path = path,
                EntityId = entityId,
                FieldKey = to,
                MessageAr = $"مرجع الحقل '{to}' غير موجود."
            });
            return;
        }

        graph.TryAdd(from, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        graph[from].Add(to);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new FormSchemaValidationIssue
            {
                Code = "SelfReference",
                Path = path,
                EntityId = entityId,
                FieldKey = from,
                MessageAr = "لا يُسمح بالمرجع الذاتي."
            });
        }
    }

    private static void WalkFormula(
        string ownerKey,
        FormFormulaNode? node,
        string path,
        Guid? entityId,
        IReadOnlyDictionary<string, FormFieldSchema> fieldsByKey,
        Dictionary<string, HashSet<string>> graph,
        List<FormSchemaValidationIssue> issues)
    {
        if (node is null)
        {
            return;
        }

        switch (node)
        {
            case FormFieldReferenceNode fr:
                AddEdge(ownerKey, fr.FieldKey, path, entityId, fieldsByKey, graph, issues);
                break;
            case FormBinaryOperationNode bin:
                WalkFormula(ownerKey, bin.Left, path, entityId, fieldsByKey, graph, issues);
                WalkFormula(ownerKey, bin.Right, path, entityId, fieldsByKey, graph, issues);
                break;
            case FormFunctionCallNode fn:
                foreach (var arg in fn.Arguments)
                {
                    WalkFormula(ownerKey, arg, path, entityId, fieldsByKey, graph, issues);
                }

                break;
        }
    }

    private static void WalkCondition(
        string ownerKey,
        FormConditionGroup? group,
        string path,
        Guid? entityId,
        IReadOnlyDictionary<string, FormFieldSchema> fieldsByKey,
        Dictionary<string, HashSet<string>> graph,
        List<FormSchemaValidationIssue> issues)
    {
        if (group is null)
        {
            return;
        }

        foreach (var predicate in group.Predicates)
        {
            AddEdge(ownerKey, predicate.FieldKey, path, entityId, fieldsByKey, graph, issues);
        }

        foreach (var nested in group.Groups)
        {
            WalkCondition(ownerKey, nested, path, entityId, fieldsByKey, graph, issues);
        }
    }

    private static void DetectCycles(
        Dictionary<string, HashSet<string>> graph,
        List<FormSchemaValidationIssue> issues)
    {
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Keys)
        {
            VisitNode(node, graph, visiting, visited, reported, [], issues);
        }
    }

    private static void VisitNode(
        string node,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visiting,
        HashSet<string> visited,
        HashSet<string> reported,
        List<string> path,
        List<FormSchemaValidationIssue> issues)
    {
        if (visited.Contains(node))
        {
            return;
        }

        if (visiting.Contains(node))
        {
            var cycleStart = path.FindIndex(p => string.Equals(p, node, StringComparison.OrdinalIgnoreCase));
            if (cycleStart >= 0)
            {
                var cyclePath = path.Skip(cycleStart).Append(node).ToList();
                var cycleKey = string.Join("|", cyclePath, StringComparer.OrdinalIgnoreCase);
                if (reported.Add(cycleKey))
                {
                    issues.Add(new FormSchemaValidationIssue
                    {
                        Code = "DependencyCycle",
                        Path = "dependencies",
                        FieldKey = node,
                        MessageAr = $"حلقة اعتماد مكتشفة: {string.Join(" → ", cyclePath)}"
                    });
                }
            }

            return;
        }

        visiting.Add(node);
        path.Add(node);
        if (graph.TryGetValue(node, out var edges))
        {
            foreach (var next in edges)
            {
                VisitNode(next, graph, visiting, visited, reported, path, issues);
            }
        }

        path.RemoveAt(path.Count - 1);
        visiting.Remove(node);
        visited.Add(node);
    }
}
