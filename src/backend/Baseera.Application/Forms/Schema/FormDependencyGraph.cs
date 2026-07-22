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

        void EnsureNode(string key) => graph.TryAdd(key, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        foreach (var key in fieldsByKey.Keys)
        {
            EnsureNode(key);
        }

        void AddEdge(string from, string to, string path, Guid? entityId)
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

            EnsureNode(from);
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

        void WalkFormula(string ownerKey, FormFormulaNode? node, string path, Guid? entityId)
        {
            if (node is null) return;
            switch (node)
            {
                case FormFieldReferenceNode fr:
                    AddEdge(ownerKey, fr.FieldKey, path, entityId);
                    break;
                case FormBinaryOperationNode bin:
                    WalkFormula(ownerKey, bin.Left, path, entityId);
                    WalkFormula(ownerKey, bin.Right, path, entityId);
                    break;
                case FormFunctionCallNode fn:
                    foreach (var arg in fn.Arguments)
                    {
                        WalkFormula(ownerKey, arg, path, entityId);
                    }
                    break;
            }
        }

        void WalkCondition(string ownerKey, FormConditionGroup? group, string path, Guid? entityId)
        {
            if (group is null) return;
            foreach (var predicate in group.Predicates)
            {
                AddEdge(ownerKey, predicate.FieldKey, path, entityId);
            }

            foreach (var nested in group.Groups)
            {
                WalkCondition(ownerKey, nested, path, entityId);
            }
        }

        foreach (var page in document.Pages)
        {
            var owner = $"page:{page.Key}";
            EnsureNode(owner);
            WalkCondition(owner, page.VisibilityCondition, $"pages[{page.Key}].visibility", page.Id);
            foreach (var section in page.Sections)
            {
                var sectionOwner = $"section:{section.Key}";
                EnsureNode(sectionOwner);
                WalkCondition(sectionOwner, section.VisibilityCondition, $"sections[{section.Key}].visibility", section.Id);
                foreach (var field in section.Fields)
                {
                    WalkCondition(field.Key, field.VisibilityCondition, $"fields[{field.Key}].visibility", field.Id);
                    WalkCondition(field.Key, field.RequiredCondition, $"fields[{field.Key}].required", field.Id);
                    WalkFormula(field.Key, field.Formula, $"fields[{field.Key}].formula", field.Id);
                    if (field.RepeatingTable is not null)
                    {
                        foreach (var col in field.RepeatingTable.Columns)
                        {
                            WalkCondition(col.Key, col.VisibilityCondition, $"fields[{field.Key}].columns[{col.Key}].visibility", col.Id);
                            WalkFormula(col.Key, col.Formula, $"fields[{field.Key}].columns[{col.Key}].formula", col.Id);
                        }
                    }
                }
            }
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Dfs(string node, Stack<string> stack)
        {
            if (visiting.Contains(node))
            {
                var cycle = string.Join(" → ", stack.Reverse().SkipWhile(x => !string.Equals(x, node, StringComparison.OrdinalIgnoreCase)).Append(node));
                issues.Add(new FormSchemaValidationIssue
                {
                    Code = "DependencyCycle",
                    Path = "dependencies",
                    FieldKey = node,
                    MessageAr = $"حلقة اعتماد مكتشفة: {cycle}"
                });
                return true;
            }

            if (visited.Contains(node)) return false;
            visiting.Add(node);
            stack.Push(node);
            if (graph.TryGetValue(node, out var edges))
            {
                foreach (var next in edges)
                {
                    if (Dfs(next, stack)) return true;
                }
            }

            stack.Pop();
            visiting.Remove(node);
            visited.Add(node);
            return false;
        }

        foreach (var node in graph.Keys.ToList())
        {
            Dfs(node, new Stack<string>());
        }

        return issues;
    }
}
