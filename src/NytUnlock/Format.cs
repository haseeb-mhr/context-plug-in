namespace NytUnlock;

internal static class Format
{
    public static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s[..max];
    }

    public static void SearchTable(IReadOnlyList<CachedArticle> docs)
    {
        for (var i = 0; i < docs.Count; i++)
        {
            var d = docs[i];
            Console.WriteLine($"[{i}] {Truncate(d.Headline, 96)}");
            Console.WriteLine($"    {d.PubDate?[..Math.Min(10, d.PubDate.Length)]}  {d.WebUrl}");
            if (!string.IsNullOrWhiteSpace(d.Snippet))
            {
                Console.WriteLine($"    {Truncate(d.Snippet, 110)}");
            }
            Console.WriteLine();
        }
    }

    public static void Field(string label, string? value)
        => Console.WriteLine($"  {label,-14} {value ?? "-"}");
}
