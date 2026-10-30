namespace Parchwell.Kafka.Authentication;

/// <summary>Resolves a projected token path by substituting links in component order.</summary>
internal static class ProjectedTokenPathResolver
{
    internal const int MaximumPathCharacters = 4096;
    internal const int MaximumComponentVisits = 256;
    internal const int MaximumLinkExpansions = 40;

    /// <summary>Resolves file and directory links while preserving left-to-right path traversal.</summary>
    /// <param name="path">The configured path to resolve.</param>
    /// <param name="readLinkTarget">Reads the raw target of one path component, or returns null.</param>
    /// <param name="requireDirectory">Requires a non-link intermediate component to be a directory.</param>
    /// <returns>The fully traversed lexical path of the final non-link component.</returns>
    /// <exception cref="IOException">A path is unsupported or exceeds a resolver policy bound.</exception>
    internal static string Resolve(
        string path,
        Func<string, string?> readLinkTarget,
        Action<string> requireDirectory)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(readLinkTarget);
        ArgumentNullException.ThrowIfNull(requireDirectory);

        if (path.Length == 0 || path.Contains('\0'))
            throw new ArgumentException("The path is invalid.", nameof(path));
        if (path.Length > MaximumPathCharacters)
            throw new IOException();

        string configured = path;
        if (!Path.IsPathRooted(configured))
        {
            configured = Path.Combine(Directory.GetCurrentDirectory(), configured);
            if (configured.Length > MaximumPathCharacters)
                throw new IOException();
        }

        ValidateRoot(configured);
        string root = Path.GetPathRoot(configured) ?? throw new IOException();
        var pending = new LinkedList<string>(SplitComponents(configured.AsSpan(root.Length)));
        if (pending.Count == 0)
            throw new IOException();

        string prefix = root;
        int visits = 0;
        int expansions = 0;

        while (pending.First is not null)
        {
            if (visits == MaximumComponentVisits)
                throw new IOException();

            visits++;
            string component = pending.First.Value;
            pending.RemoveFirst();

            if (component == ".")
                continue;

            if (component == "..")
            {
                prefix = ParentOrRoot(prefix, root);
                continue;
            }

            string candidate = Path.Combine(prefix, component);
            if (candidate.Length > MaximumPathCharacters)
                throw new IOException();

            string? rawTarget = readLinkTarget(candidate);
            if (rawTarget is not null)
            {
                if (rawTarget.Length == 0 || rawTarget.Length > MaximumPathCharacters || rawTarget.Contains('\0'))
                    throw new IOException();
                if (expansions == MaximumLinkExpansions)
                    throw new IOException();

                bool targetIsRooted = Path.IsPathRooted(rawTarget);
                if (targetIsRooted)
                    ValidateRoot(rawTarget);

                string newPrefix = prefix;
                string targetComponentsText = rawTarget;
                if (targetIsRooted)
                {
                    newPrefix = Path.GetPathRoot(rawTarget) ?? throw new IOException();
                    targetComponentsText = rawTarget.AsSpan(newPrefix.Length).ToString();
                }

                EnsureExpansionLength(newPrefix, targetComponentsText, pending);

                expansions++;
                var expanded = new LinkedList<string>(SplitComponents(targetComponentsText.AsSpan()));
                foreach (string suffix in pending)
                    expanded.AddLast(suffix);
                pending = expanded;
                prefix = newPrefix;
                continue;
            }

            if (pending.Count == 0)
                return candidate;

            requireDirectory(candidate);
            prefix = candidate;
        }

        return prefix;
    }

    private static void ValidateRoot(string path)
    {
        if (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
            throw new IOException();
    }

    private static string ParentOrRoot(string path, string root)
    {
        if (string.Equals(path, root, StringComparison.Ordinal))
            return root;

        string? parent = Path.GetDirectoryName(path);
        return parent is null || parent.Length < root.Length ? root : parent;
    }

    private static List<string> SplitComponents(ReadOnlySpan<char> path)
    {
        var components = new List<string>();
        int start = 0;
        for (int index = 0; index <= path.Length; index++)
        {
            bool atEnd = index == path.Length;
            if (!atEnd && path[index] != Path.DirectorySeparatorChar && path[index] != Path.AltDirectorySeparatorChar)
                continue;

            if (index > start)
                components.Add(path[start..index].ToString());
            start = index + 1;
        }

        return components;
    }

    private static void EnsureExpansionLength(string prefix, string rawTarget, LinkedList<string> suffix)
    {
        int length = prefix.Length;
        bool endsInSeparator = length > 0 && IsSeparator(prefix[^1]);
        if (rawTarget.Length > 0)
        {
            if (length > 0 && !endsInSeparator && !IsSeparator(rawTarget[0]))
                length = checked(length + 1);
            length = checked(length + rawTarget.Length);
            endsInSeparator = IsSeparator(rawTarget[^1]);
        }

        foreach (string component in suffix)
        {
            if (length > 0 && !endsInSeparator)
                length = checked(length + 1);
            length = checked(length + component.Length);
            endsInSeparator = false;
            if (length > MaximumPathCharacters)
                throw new IOException();
        }

        if (length > MaximumPathCharacters)
            throw new IOException();
    }

    private static bool IsSeparator(char value) =>
        value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;
}
