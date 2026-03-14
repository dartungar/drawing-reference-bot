public static class DrawingSubjectParser
{
    public static bool TryExtractSubject(string text, out string? subject)
    {
        subject = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lowered = text.Trim();

        static string? After(string input, string needle)
        {
            var idx = input.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            return input[(idx + needle.Length)..].Trim();
        }

        var tail = After(lowered, "drawing reference for ")
                   ?? After(lowered, "reference for ")
                   ?? After(lowered, "draw ")
                   ?? After(lowered, "drawing ")
                   ?? After(lowered, "sketch ");

        if (string.IsNullOrWhiteSpace(tail))
        {
            return false;
        }

        var cleaned = tail.Trim().Trim('.', '!', '?', ':', ';', ',', '"', '\'', ')', '(', '[', ']', '{', '}');
        foreach (var stop in new[] { "some ", "a ", "an ", "the ", "my ", "any " })
        {
            if (cleaned.StartsWith(stop, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[stop.Length..].Trim();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return false;
        }

        subject = cleaned;
        return true;
    }
}
