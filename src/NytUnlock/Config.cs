using Nytimes;
using Paypal;
using Paypal.Core.Authentication.OAuth2.ClientCredentials;
using NytEnvironment = Nytimes.Servers.ServerEnvironment;
using PaypalEnvironment = Paypal.Servers.ServerEnvironment;

namespace NytUnlock;

/// <summary>
/// Environment loading, client construction and the startup banner.
///
/// Credentials are validated per command rather than all at once: the NYT-only read
/// path must not be gated on PayPal credentials it never uses (BUGS.md BUG-20).
/// </summary>
internal static class Config
{
    private static readonly HttpClient Http = new();

    public static string RepoRoot { get; } = FindRepoRoot();
    public static string LedgerPath => Path.Combine(RepoRoot, "ledger.json");
    public static string CacheDir => Path.Combine(RepoRoot, ".cache");
    public static string CachePath => Path.Combine(CacheDir, "search.json");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    public static void LoadEnv() => Env.Load(Path.Combine(RepoRoot, ".env"));

    private static string? Get(string key)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static string Require(string key)
        => Get(key) ?? throw new ConfigError(
            $"Missing required environment variable {key}. Copy .env.example to .env and fill it in.");

    /// <summary>
    /// Resolves PAYPAL_ENV case-insensitively and refuses anything unrecognised.
    ///
    /// BUGS.md BUG-23: the SDK's wire values are mixed-case ("production" vs "Sandbox"),
    /// so comparing a user-supplied string against a literal silently mis-resolves —
    /// PAYPAL_ENV=Production would fail an equality check against "production" and fall
    /// through to a permissive default. Normalising and rejecting unknown values closes that.
    /// </summary>
    public static PaypalEnvironment ResolvePaypalEnvironment()
    {
        var raw = Get("PAYPAL_ENV") ?? "Sandbox";

        switch (raw.ToLowerInvariant())
        {
            case "sandbox":
                return PaypalEnvironment.Sandbox;

            case "production":
            case "live":
                if (!string.Equals(Get("ALLOW_PRODUCTION"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConfigError(
                        $"PAYPAL_ENV={raw} requires ALLOW_PRODUCTION=true. Refusing to continue.");
                }
                return PaypalEnvironment.Production;

            default:
                throw new ConfigError(
                    $"PAYPAL_ENV='{raw}' is not recognised. Use 'Sandbox' or 'production'.");
        }
    }

    public static PaypalClientOptions PaypalOptions()
    {
        return new PaypalClientOptions
        {
            Environment = ResolvePaypalEnvironment(),
            Oauth2 = new OAuth2ClientCredentials
            {
                ClientId = Require("PAYPAL_CLIENT_ID"),
                ClientSecret = Require("PAYPAL_CLIENT_SECRET"),
            },
        };
    }

    public static NytimesClientOptions NytOptions()
    {
        return new NytimesClientOptions
        {
            Environment = NytEnvironment.Production,
            Apikey = Require("NYT_API_KEY"),
        };
    }

    public static PaypalClient Paypal(PaypalClientOptions options) => new(Http, options);

    public static NytimesClient Nyt(NytimesClientOptions options) => new(Http, options);

    public static string SigningSecret() => Require("UNLOCK_SIGNING_SECRET");

    public static string ReturnUrl() => Get("RETURN_URL") ?? "https://example.com/unlock/return";
    public static string CancelUrl() => Get("CANCEL_URL") ?? "https://example.com/unlock/cancel";

    /// <summary>
    /// Hackathon ground rule 2 — confirm the host before the first write. Base URLs are
    /// read off the options object, never hardcoded.
    /// </summary>
    public static void Banner(NytimesClientOptions? nyt, PaypalClientOptions? paypal)
    {
        Console.WriteLine("nyt-unlock — resolved hosts");

        if (nyt is not null)
        {
            // Article Search is served by the Default1 server group (map/operations/Search.md).
            Console.WriteLine($"  NYT     {nyt.Server.Default1.Production.BaseUrl}  (env: {nyt.Environment})");
        }

        if (paypal is not null)
        {
            var env = paypal.Environment;
            var url = env == PaypalEnvironment.Sandbox
                ? paypal.Server.Default.Sandbox.BaseUrl
                : paypal.Server.Default.Production.BaseUrl;

            Console.WriteLine($"  PayPal  {url}  (env: {env})");

            // The SDK ships Production.BaseUrl == Sandbox.BaseUrl. Surface that rather than
            // let a reader assume "production" means live. Recorded in FINDINGS.md.
            if (paypal.Server.Default.Production.BaseUrl == paypal.Server.Default.Sandbox.BaseUrl)
            {
                Console.WriteLine("  note    SDK maps BOTH PayPal environments to the sandbox host.");
            }
        }

        Console.WriteLine();
    }
}
