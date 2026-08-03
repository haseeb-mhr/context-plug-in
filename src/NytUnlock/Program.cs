using NytUnlock;

return await Cli.Run(args);

internal static class Cli
{
    public static async Task<int> Run(string[] args)
    {
        try
        {
            Config.LoadEnv();

            if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
            {
                Help();
                return 0;
            }

            var command = args[0];
            var rest = args.Skip(1).ToArray();

            switch (command)
            {
                case "search":
                {
                    var query = Positional(rest, 0)
                                ?? throw new ConfigError("search needs a query: nyt-unlock search \"<query>\"");
                    var page = Flag(rest, "--page") is { } p ? int.Parse(p) : (int?)null;
                    var sort = Flag(rest, "--sort");

                    var options = Config.NytOptions();
                    Config.Banner(options, null);
                    return await Nyt.Search(Config.Nyt(options), query, page, sort);
                }

                case "buy":
                {
                    var index = Index(rest);
                    var price = Flag(rest, "--price") ?? "0.99";

                    var options = Config.PaypalOptions();
                    Config.Banner(null, options);
                    return await Checkout.Buy(Config.Paypal(options), index, price);
                }

                case "claim":
                {
                    var index = Index(rest);
                    var mock = Flag(rest, "--mock");

                    var options = Config.PaypalOptions();
                    Config.Banner(null, options);
                    return await Checkout.Claim(Config.Paypal(options), index, mock);
                }

                case "status":
                {
                    var index = Index(rest);
                    var options = Config.PaypalOptions();
                    Config.Banner(null, options);
                    return await Checkout.Status(Config.Paypal(options), index);
                }

                case "refund":
                {
                    var index = Index(rest);
                    var amount = Flag(rest, "--amount");
                    var note = Flag(rest, "--note");

                    var options = Config.PaypalOptions();
                    Config.Banner(null, options);
                    return await Checkout.Refund(Config.Paypal(options), index, amount, note);
                }

                case "verify":
                {
                    var token = Positional(rest, 0)
                                ?? throw new ConfigError("verify needs a token: nyt-unlock verify <token>");
                    var (ok, reason) = Ledger.Verify(token);
                    Console.WriteLine(ok ? $"VALID — {reason}" : $"INVALID — {reason}");
                    return ok ? 0 : 1;
                }

                default:
                    Console.Error.WriteLine($"Unknown command '{command}'.");
                    Help();
                    return 2;
            }
        }
        catch (ConfigError ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ex.ExitCode;
        }
    }

    private static int Index(string[] args)
    {
        var raw = Positional(args, 0)
                  ?? throw new ConfigError("This command needs an article index from the last search.");
        if (!int.TryParse(raw, out var index)) throw new ConfigError($"'{raw}' is not a valid index.");
        return index;
    }

    /// <summary>Nth argument that is neither a flag nor a flag's value.</summary>
    private static string? Positional(string[] args, int n)
    {
        var seen = 0;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                i++; // skip the flag's value
                continue;
            }
            if (seen == n) return args[i];
            seen++;
        }
        return null;
    }

    private static string? Flag(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }

    private static void Help()
    {
        Console.WriteLine("""
            nyt-unlock — pay USD 0.99 through PayPal sandbox to unlock one NYT article.

            Commands
              search "<query>" [--page N] [--sort newest|oldest|relevance|best]
                  Search NYT Article Search and cache the results.

              buy <index> [--price 0.99]
                  Create a PayPal order for the cached article at <index>.

              claim <index> [--mock <CODE>]
                  Capture the order and mint a 24h access token.
                  --mock sends PayPal's negative-testing header, e.g. --mock INSTRUMENT_DECLINED.

              status <index>
                  Reconcile the order and capture against the local ledger.

              refund <index> [--amount 0.50] [--note "..."]
                  Refund. A partial refund keeps access; a full refund revokes it.

              verify <token>
                  Check a token's signature, expiry and ledger status.

            Environment (see .env.example)
              PAYPAL_CLIENT_ID, PAYPAL_CLIENT_SECRET   sandbox REST app credentials
              PAYPAL_ENV                               Sandbox (default) | production
              NYT_API_KEY                              NYT key with Article Search enabled
              UNLOCK_SIGNING_SECRET                    signs the access token
              RETURN_URL, CANCEL_URL                   optional
              ALLOW_PRODUCTION                         must be true to leave sandbox

            Exit codes
              0 ok · 1 invalid token · 2 usage/config · 3 NYT rate limit · 4 NYT auth
              5 order not approved · 6 instrument declined · 7 handled PayPal error · 8 other
            """);
    }
}
