using System;
using System.IO;

namespace MTC;

/// <summary>
/// Resolves agent-supplied script paths against the scripts root directory and ensures the
/// resolved location never escapes that root. Every path handed in by an agent through the
/// script file tools (<c>write_script</c>, <c>edit_script</c>, <c>read_script</c>) is
/// interpreted RELATIVE to the scripts root (for example <c>Pack2/2_Find.ts</c> or
/// <c>include/header.ts</c>), and any attempt to traverse out of the root is rejected.
/// </summary>
internal static class ScriptPathGuard
{
    /// <summary>
    /// Resolves a relative path against <paramref name="scriptsRoot"/> and verifies the
    /// full path stays inside (or equals) the root.
    /// </summary>
    /// <param name="relativePath">Path relative to the scripts root; forward slashes are accepted.</param>
    /// <param name="scriptsRoot">Absolute path of the scripts root directory.</param>
    /// <param name="fullPath">The resolved absolute path when the method returns <c>true</c>.</param>
    /// <returns><c>true</c> when the path is a valid relative path contained in the root</returns>
    public static bool TryResolve(string? relativePath, string scriptsRoot, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(scriptsRoot))
            return false;

        if (relativePath.Contains('\0') || scriptsRoot.Contains('\0'))
            return false;

        // Backslashes are rejected outright: on POSIX they would otherwise become literal
        // filename characters, and agents are instructed to use forward slashes.
        if (relativePath.Contains('\\'))
            return false;

        string root = NormalizeRoot(scriptsRoot);
        if (string.IsNullOrWhiteSpace(root))
            return false;

        string normalizedRelative = relativePath.Trim().Replace('/', Path.DirectorySeparatorChar);

        // Reject rooted input outright (drive letters, leading separators, absolute paths)
        // before it can be combined with the root.
        if (Path.IsPathRooted(normalizedRelative))
            return false;

        string combinedPath;
        try
        {
            combinedPath = Path.GetFullPath(Path.Combine(root, normalizedRelative));
        }
        catch (Exception)
        {
            // Path.Combine/GetFullPath throw on malformed input; nothing is allowed through.
            return false;
        }

        if (!IsInsideRoot(combinedPath, root))
            return false;

        fullPath = combinedPath;
        return true;
    }

    /// <summary>
    /// Determines whether a full path is the root itself or located inside it.
    /// </summary>
    /// <param name="fullPath">Absolute path to test</param>
    /// <param name="root">Absolute root directory</param>
    /// <returns><c>true</c> when <paramref name="fullPath"/> equals or is inside <paramref name="root"/></returns>
    public static bool IsInsideRoot(string fullPath, string root)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(root))
            return false;

        string normalizedRoot = NormalizeRoot(root);
        string normalizedFull = fullPath.Trim();

        if (OperatingSystem.IsWindows())
        {
            // Windows path comparison is case-insensitive.
            int comparison = string.Compare(normalizedFull, normalizedRoot, StringComparison.OrdinalIgnoreCase);
            if (comparison == 0)
                return true;

            return normalizedFull.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(normalizedFull, normalizedRoot, StringComparison.Ordinal))
            return true;

        return normalizedFull.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string NormalizeRoot(string root)
    {
        try
        {
            string full = Path.GetFullPath(root.Trim());
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
