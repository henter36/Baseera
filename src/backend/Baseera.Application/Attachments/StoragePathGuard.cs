namespace Baseera.Application.Attachments;

/// <summary>
/// Canonical path containment checks for attachment storage (Windows + Unix).
/// </summary>
public static class StoragePathGuard
{
    public static string NormalizeRoot(string rootPath) =>
        Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static bool IsPathInsideRoot(string rootPath, string candidatePath)
    {
        var root = NormalizeRoot(rootPath);
        var candidate = Path.GetFullPath(candidatePath);
        var relative = Path.GetRelativePath(root, candidate);

        if (string.IsNullOrWhiteSpace(relative) || relative == ".")
        {
            return true;
        }

        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        if (relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) ||
            relative.StartsWith("../", StringComparison.Ordinal) ||
            relative.StartsWith("..\\", StringComparison.Ordinal))
        {
            return false;
        }

        var separators = new[]
        {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            '/',
            '\\'
        }.Distinct().ToArray();

        var segments = relative.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        return !segments.Contains("..", StringComparer.Ordinal);
    }

    public static void EnsureInsideRoot(string rootPath, string candidatePath)
    {
        if (!IsPathInsideRoot(rootPath, candidatePath))
        {
            throw new UnauthorizedAccessException("مسار الملف خارج مساحة التخزين المسموحة.");
        }
    }
}
