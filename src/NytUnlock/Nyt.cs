using System.Text.Json;
using Nytimes;
using Nytimes.Core.Exceptions;
using Nytimes.Errors;
using Nytimes.Models.Enums;

namespace NytUnlock;

internal sealed class CachedArticle
{
    public string Uri { get; set; } = "";
    public string? Headline { get; set; }
    public string? WebUrl { get; set; }
    public string? PubDate { get; set; }
    public string? Snippet { get; set; }
}

internal sealed class SearchCache
{
    public string Query { get; set; } = "";
    public int Page { get; set; }
    public string? SavedAt { get; set; }
    public List<CachedArticle> Docs { get; set; } = [];
}

internal static class Nyt
{
    /// <summary>Maps --sort onto real SDK enum members (Sort: Best/Newest/Oldest/Relevance).</summary>
    public static Sort? ParseSort(string? value) => value?.ToLowerInvariant() switch
    {
        null => null,
        "newest" => Sort.Newest,
        "oldest" => Sort.Oldest,
        "relevance" => Sort.Relevance,
        "best" => Sort.Best,
        _ => throw new ConfigError($"--sort '{value}' is not valid. Use newest, oldest, relevance or best."),
    };

    public static async Task<int> Search(NytimesClient client, string query, int? page, string? sort)
    {
        var sortEnum = ParseSort(sort);

        try
        {
            // All six leading parameters are nullable-with-no-default, so they must be passed
            // explicitly; null means "omit". Named arguments keep that unambiguous.
            var response = await client.Search.ReturnsAnArrayOfArticles(
                beginDate: null,
                endDate: null,
                fq: null,
                page: page,
                q: query,
                sort: sortEnum);

            var docs = response.Response?.Docs ?? [];
            var hits = response.Response?.Meta?.Hits;

            if (docs.Count == 0)
            {
                Console.WriteLine($"No articles matched \"{query}\".");
                return 0;
            }

            var cache = new SearchCache
            {
                Query = query,
                Page = page ?? 0,
                SavedAt = DateTimeOffset.UtcNow.ToString("u"),
                Docs = docs.Select(d => new CachedArticle
                {
                    // ArticleSearchArticle exposes Uri but no Id — Uri is the stable identifier.
                    Uri = d.Uri ?? "",
                    Headline = d.Headline?.Main,
                    WebUrl = d.WebUrl,
                    PubDate = d.PubDate,
                    Snippet = d.Snippet,
                }).ToList(),
            };

            Directory.CreateDirectory(Config.CacheDir);
            File.WriteAllText(Config.CachePath,
                JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"{docs.Count} of {hits?.ToString() ?? "?"} hits for \"{query}\" (page {page ?? 0})");
            Console.WriteLine();
            Format.SearchTable(cache.Docs);
            Console.WriteLine($"Cached to {Path.GetRelativePath(Config.RepoRoot, Config.CachePath)} — buy with: nyt-unlock buy <index>");
            return 0;
        }
        catch (SdkException<ReturnsAnArrayOfArticlesError> ex)
        {
            // One accessor covers 400, 401 and 429, so the status has to come off RawError.
            if (ex.Error.TryGetNoContent(out var raw))
            {
                var status = (int)raw.StatusCode;
                switch (status)
                {
                    case 429:
                        Console.Error.WriteLine("NYT rate limit hit - wait 60s and retry.");
                        return 3;
                    case 401:
                        Console.Error.WriteLine("NYT rejected the key (401). Check NYT_API_KEY and that Article Search is enabled for the app.");
                        return 4;
                    case 400:
                        Console.Error.WriteLine($"NYT rejected the query (400): {raw.ReadAsString()}");
                        return 8;
                    default:
                        Console.Error.WriteLine($"NYT error {status}: {raw.ReadAsString()}");
                        return 8;
                }
            }

            if (ex.Error.TryGetRawError(out var fallback))
            {
                Console.Error.WriteLine($"NYT error {(int)fallback.StatusCode}: {fallback.ReadAsString()}");
            }
            return 8;
        }
    }

    public static SearchCache LoadCache()
    {
        if (!File.Exists(Config.CachePath))
        {
            throw new ConfigError("No cached search. Run: nyt-unlock search \"<query>\"");
        }

        var cache = JsonSerializer.Deserialize<SearchCache>(File.ReadAllText(Config.CachePath))
                    ?? throw new ConfigError("Search cache is unreadable. Re-run search.");
        return cache;
    }

    /// <summary>
    /// Resolves an index against the cache. BUGS.md BUG-13: an index alone is not a stable
    /// identifier, so the resolved article uri is what every downstream command keys on, and
    /// the query that produced it is echoed so a stale cache is visible rather than silent.
    /// </summary>
    public static CachedArticle Resolve(int index)
    {
        var cache = LoadCache();
        if (index < 0 || index >= cache.Docs.Count)
        {
            throw new ConfigError(
                $"Index {index} is out of range — the cached search for \"{cache.Query}\" (page {cache.Page}) has {cache.Docs.Count} results.");
        }

        var article = cache.Docs[index];
        if (string.IsNullOrEmpty(article.Uri))
        {
            throw new ConfigError($"Cached article {index} has no uri and cannot be identified.");
        }

        Console.WriteLine($"index {index} -> {Format.Truncate(article.Headline, 70)}");
        Console.WriteLine($"           {article.Uri}   (from search \"{cache.Query}\", saved {cache.SavedAt})");
        Console.WriteLine();
        return article;
    }
}
