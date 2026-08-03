using System.Security.Cryptography;
using System.Text;
using Paypal;
using Paypal.Core.Exceptions;
using Paypal.Errors;
using Paypal.Models;
using Paypal.Models.Enums;

namespace NytUnlock;

internal static class Checkout
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    private static string ShortHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    /// <summary>
    /// PayPal requires 2-decimal precision for USD; "1" or "0.999" is a 400.
    /// BUGS.md BUG-19 — the plan left --price unvalidated.
    /// </summary>
    private static string NormalisePrice(string raw)
    {
        if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new ConfigError($"--price '{raw}' is not a positive decimal amount.");
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new ConfigError($"--price '{raw}' has more than 2 decimal places; USD requires exactly 2.");
        }

        return value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? MockHeader(string? code)
        => code is null ? null : $"{{\"mock_application_codes\":\"{code}\"}}";

    /// <summary>
    /// Extracts the first issue and debug id from a typed PayPal error payload.
    /// Branching is on the error TYPE and the issue CODE, never on message text.
    /// </summary>
    private static (string? Issue, string? Description, string? DebugId) Detail(Error? error)
    {
        var first = error?.Details?.FirstOrDefault();
        return (first?.Issue, first?.Description ?? error?.Message, error?.DebugId);
    }

    private static int ReportUnhandled(Error? error, int status)
    {
        var (issue, description, debugId) = Detail(error);
        Console.Error.WriteLine($"PayPal error{(status > 0 ? $" {status}" : "")}: {issue ?? "unknown"}");
        if (!string.IsNullOrWhiteSpace(description)) Console.Error.WriteLine($"  {description}");
        if (!string.IsNullOrWhiteSpace(debugId)) Console.Error.WriteLine($"  debug_id: {debugId}");
        return 7;
    }

    // ---- buy ---------------------------------------------------------------

    public static async Task<int> Buy(PaypalClient client, int index, string priceRaw)
    {
        var price = NormalisePrice(priceRaw);
        var article = Nyt.Resolve(index);

        var existing = Ledger.Find(article.Uri);
        if (existing is { Status: "GRANTED" or "PARTIALLY_REFUNDED", Token: not null })
        {
            Console.WriteLine("Already unlocked — no PayPal call made.");
            Format.Field("token", existing.Token);
            Format.Field("article", existing.WebUrl);
            return 0;
        }

        // BUGS.md BUG-10: the plan paired a per-UTC-day idempotency key with an invoiceId that
        // changed every attempt, so a retry sent a different body under the same key. Both are
        // now derived from the same inputs, making the whole request genuinely idempotent.
        var fingerprint = $"{article.Uri}|{price}|{DateTime.UtcNow:yyyy-MM-dd}";
        var requestId = $"unlock-{ShortHash(fingerprint)}";
        var invoiceId = $"unlock-{ShortHash(fingerprint)}";

        var headline = Format.Truncate(article.Headline ?? "NYT article", 127);

        var body = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Capture,
            PurchaseUnits =
            [
                new PurchaseUnitRequest
                {
                    Amount = new AmountWithBreakdown
                    {
                        CurrencyCode = "USD",
                        Value = price,
                        // Breakdown must sum exactly to Amount.Value; itemTotal is the only component.
                        Breakdown = new AmountBreakdown
                        {
                            ItemTotal = new Money { CurrencyCode = "USD", Value = price },
                        },
                    },
                    Description = headline,
                    CustomId = Format.Truncate(article.Uri, 255),
                    InvoiceId = Format.Truncate(invoiceId, 127),
                    Items =
                    [
                        new ItemRequest
                        {
                            Name = headline,
                            UnitAmount = new Money { CurrencyCode = "USD", Value = price },
                            Quantity = "1", // string, per ItemRequest — not an int
                            Category = ItemCategory.DigitalGoods,
                            Url = article.WebUrl,
                        },
                    ],
                },
            ],
            // Experience settings belong on paymentSource.paypal.experienceContext; the
            // top-level applicationContext equivalents are deprecated.
            PaymentSource = new PaymentSource
            {
                Paypal = new PayPalWallet
                {
                    ExperienceContext = new PayPalWalletExperienceContext
                    {
                        BrandName = "NYT Unlock",
                        ShippingPreference = ApplicationContextShippingPreference.NoShipping,
                        UserAction = PayPalExperienceUserAction.PayNow,
                        ReturnUrl = Config.ReturnUrl(),
                        CancelUrl = Config.CancelUrl(),
                    },
                },
            },
        };

        try
        {
            var order = await client.Orders.CreateOrder(
                payPalMockResponse: null,
                payPalRequestId: requestId,
                payPalPartnerAttributionId: null,
                payPalClientMetadataId: null,
                payPalAuthAssertion: null,
                body: body,
                prefer: "return=representation");

            Ledger.Upsert(new LedgerEntry
            {
                ArticleUri = article.Uri,
                Headline = article.Headline,
                WebUrl = article.WebUrl,
                OrderId = order.Id,
                InvoiceId = invoiceId,
                AmountValue = price,
                Status = "CREATED",
                CreatedAt = DateTimeOffset.UtcNow.ToString("u"),
            });

            // Never hardcode a checkout URL — resolve it from the HATEOAS links.
            var approve = order.Links?.FirstOrDefault(l => l.Rel == "payer-action")
                          ?? order.Links?.FirstOrDefault(l => l.Rel == "approve");

            Console.WriteLine($"Order created — {order.Status}");
            Format.Field("order id", order.Id);
            Format.Field("invoice id", invoiceId);
            Format.Field("amount", $"USD {price}");
            Format.Field("approve", approve?.Href ?? "(no payer-action/approve link returned)");
            Console.WriteLine();
            Console.WriteLine($"Approve in the browser as your sandbox buyer, then run: nyt-unlock claim {index}");
            return 0;
        }
        catch (SdkException<CreateOrderError> ex)
        {
            if (ex.Error.TryGetError(out var error)) return ReportUnhandled(error, 0);
            if (ex.Error.TryGetRawError(out var raw))
            {
                Console.Error.WriteLine($"PayPal error {(int)raw.StatusCode}: {raw.ReadAsString()}");
            }
            return 8;
        }
    }

    // ---- claim -------------------------------------------------------------

    public static async Task<int> Claim(PaypalClient client, int index, string? mockCode)
    {
        var article = Nyt.Resolve(index);
        var entry = Ledger.Find(article.Uri)
                    ?? throw new ConfigError($"No order for this article. Run: nyt-unlock buy {index}");

        if (entry.OrderId is null)
        {
            throw new ConfigError($"Ledger entry has no order id. Re-run: nyt-unlock buy {index}");
        }

        try
        {
            // BUGS.md BUG-11: the plan reused the create-order idempotency key here, which turns
            // a second claim into an idempotent replay of the original success — silently
            // deleting the ORDER_ALREADY_CAPTURED path that the demo and the acceptance
            // criterion both depend on. A fresh id per attempt keeps the 422 reachable.
            var order = await client.Orders.CaptureOrder(
                id: entry.OrderId,
                payPalMockResponse: MockHeader(mockCode),
                payPalRequestId: Guid.NewGuid().ToString(),
                payPalClientMetadataId: null,
                payPalAuthAssertion: null,
                body: null,
                prefer: "return=representation");

            var captureId = order.PurchaseUnits?.FirstOrDefault()?.Payments?.Captures?.FirstOrDefault()?.Id;
            return Grant(entry, captureId, order.Id, "captured");
        }
        catch (SdkException<CaptureOrderError> ex)
        {
            if (ex.Error.TryGetError(out var error))
            {
                var (issue, description, debugId) = Detail(error);

                switch (issue)
                {
                    case "ORDER_ALREADY_CAPTURED":
                        // Recovery: re-read the order and restore access from the existing capture.
                        return await RestoreFromOrder(client, entry);

                    case "ORDER_NOT_APPROVED":
                        Console.Error.WriteLine("Approve the order first — open the approve link from `buy` and pay as your sandbox buyer.");
                        return 5;

                    case "INSTRUMENT_DECLINED":
                        Console.Error.WriteLine("The buyer's instrument was declined. PayPal's guidance is to restart the order: run `buy` again to create a fresh one.");
                        return 6;

                    default:
                        return ReportUnhandled(error, 0);
                }
            }

            if (ex.Error.TryGetRawError(out var raw))
            {
                Console.Error.WriteLine($"PayPal error {(int)raw.StatusCode}: {raw.ReadAsString()}");
            }
            return 8;
        }
    }

    private static async Task<int> RestoreFromOrder(PaypalClient client, LedgerEntry entry)
    {
        try
        {
            var order = await client.Orders.GetOrder(
                id: entry.OrderId!,
                fields: null,
                payPalMockResponse: null,
                payPalAuthAssertion: null);

            var captureId = order.PurchaseUnits?.FirstOrDefault()?.Payments?.Captures?.FirstOrDefault()?.Id;
            if (captureId is null)
            {
                Console.Error.WriteLine("Order reports already captured, but no capture id is present on it.");
                return 7;
            }

            Console.WriteLine($"already captured - access restored from order {order.Id}, capture {captureId}");
            return Grant(entry, captureId, order.Id, "restored");
        }
        catch (SdkException<GetOrderError> ex)
        {
            if (ex.Error.TryGetError(out var error)) return ReportUnhandled(error, 0);
            return 8;
        }
    }

    private static int Grant(LedgerEntry entry, string? captureId, string? orderId, string verb)
    {
        var (token, exp) = Ledger.Mint(entry.ArticleUri, TokenLifetime);

        entry.Status = "GRANTED";
        entry.CaptureId = captureId;
        entry.OrderId = orderId ?? entry.OrderId;
        entry.CapturedAt = DateTimeOffset.UtcNow.ToString("u");
        entry.Token = token;
        entry.ExpiresAtUnix = exp;
        Ledger.Upsert(entry);

        Console.WriteLine($"Access {verb}.");
        Format.Field("token", token);
        Format.Field("article", entry.WebUrl);
        Format.Field("capture id", captureId);
        Format.Field("expires", DateTimeOffset.FromUnixTimeSeconds(exp).ToString("u"));
        return 0;
    }

    // ---- status ------------------------------------------------------------

    public static async Task<int> Status(PaypalClient client, int index)
    {
        var article = Nyt.Resolve(index);
        var entry = Ledger.Find(article.Uri)
                    ?? throw new ConfigError($"No ledger entry for this article. Run: nyt-unlock buy {index}");

        try
        {
            var order = await client.Orders.GetOrder(
                id: entry.OrderId!,
                fields: null,
                payPalMockResponse: null,
                payPalAuthAssertion: null);

            var unit = order.PurchaseUnits?.FirstOrDefault();

            Console.WriteLine("Order");
            Format.Field("order id", order.Id);
            Format.Field("status", order.Status?.ToString());
            Format.Field("custom_id", unit?.CustomId);
            Format.Field("invoice_id", unit?.InvoiceId);
            Format.Field("amount", unit?.Amount is null ? null : $"{unit.Amount.CurrencyCode} {unit.Amount.Value}");
            Format.Field("create time", order.CreateTime);

            // BUGS.md BUG-18: captureId only exists after a successful claim, so calling
            // GetCapturedPayment unconditionally crashes on a CREATED entry. Guard it and
            // report a partial reconciliation instead.
            if (entry.CaptureId is null)
            {
                Console.WriteLine();
                Console.WriteLine("No capture yet — order is not claimed. Partial reconciliation only.");
            }
            else
            {
                var capture = await client.Payments.GetCapturedPayment(
                    captureId: entry.CaptureId,
                    payPalMockResponse: null);

                Console.WriteLine();
                Console.WriteLine("Capture");
                Format.Field("capture id", capture.Id);
                Format.Field("status", capture.Status?.ToString());
                Format.Field("amount", capture.Amount is null ? null : $"{capture.Amount.CurrencyCode} {capture.Amount.Value}");
                Format.Field("custom_id", capture.CustomId);
                Format.Field("invoice_id", capture.InvoiceId);
                Format.Field("create time", capture.CreateTime);
            }

            Console.WriteLine();
            Console.WriteLine("Ledger");
            Format.Field("status", entry.Status);
            Format.Field("refund id", entry.RefundId);

            var echoed = unit?.CustomId;
            if (echoed is not null && echoed != entry.ArticleUri)
            {
                Console.WriteLine();
                Console.WriteLine($"MISMATCH: PayPal echoed custom_id '{echoed}' but the ledger holds '{entry.ArticleUri}'.");
            }

            return 0;
        }
        catch (SdkException<GetOrderError> ex)
        {
            if (ex.Error.TryGetError(out var error)) return ReportUnhandled(error, 0);
            return 8;
        }
        catch (SdkException<GetCapturedPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var error)) return ReportUnhandled(error, 0);
            return 8;
        }
    }

    // ---- refund ------------------------------------------------------------

    public static async Task<int> Refund(PaypalClient client, int index, string? amountRaw, string? note)
    {
        var article = Nyt.Resolve(index);
        var entry = Ledger.Find(article.Uri)
                    ?? throw new ConfigError($"No ledger entry for this article. Run: nyt-unlock buy {index}");

        if (entry.CaptureId is null)
        {
            throw new ConfigError($"Nothing to refund — no capture recorded. Run: nyt-unlock claim {index}");
        }

        var isPartial = amountRaw is not null;
        RefundRequest? body = null;

        if (isPartial)
        {
            var amount = NormalisePrice(amountRaw!);
            body = new RefundRequest
            {
                Amount = new Money { CurrencyCode = "USD", Value = amount },
                NoteToPayer = note,
                InvoiceId = entry.InvoiceId,
            };
        }
        else if (note is not null)
        {
            body = new RefundRequest { NoteToPayer = note, InvoiceId = entry.InvoiceId };
        }
        // Full refund with no note sends no body at all, per the operation's notes.

        try
        {
            var refund = await client.Payments.RefundCapturedPayment(
                captureId: entry.CaptureId,
                payPalMockResponse: null,
                payPalRequestId: Guid.NewGuid().ToString(),
                payPalAuthAssertion: null,
                body: body,
                prefer: "return=representation");

            // BUGS.md BUG-16: the plan revoked all access on any refund, so a 50c refund of a
            // 99c purchase removed everything. A partial refund keeps access and is recorded
            // distinctly; only a full refund revokes.
            entry.Status = isPartial ? "PARTIALLY_REFUNDED" : "REVOKED";
            entry.RefundId = refund.Id;
            entry.RefundedAt = DateTimeOffset.UtcNow.ToString("u");
            Ledger.Upsert(entry);

            Console.WriteLine($"Refund {refund.Status?.ToString() ?? "issued"} — ledger now {entry.Status}");
            Format.Field("refund id", refund.Id);
            Format.Field("amount", refund.Amount is null ? null : $"{refund.Amount.CurrencyCode} {refund.Amount.Value}");
            if (!isPartial)
            {
                Console.WriteLine();
                Console.WriteLine("Access revoked — the token will no longer validate. Check with: nyt-unlock verify <token>");
            }
            return 0;
        }
        catch (SdkException<RefundCapturedPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var error))
            {
                var (issue, _, _) = Detail(error);

                switch (issue)
                {
                    case "CAPTURE_FULLY_REFUNDED":
                        Console.Error.WriteLine("This capture is already fully refunded — nothing left to refund.");
                        return 7;

                    case "REFUND_AMOUNT_EXCEEDED":
                        Console.Error.WriteLine("Refund amount exceeds what remains on the capture.");
                        return 7;

                    default:
                        return ReportUnhandled(error, 0);
                }
            }

            if (ex.Error.TryGetRawError(out var raw))
            {
                Console.Error.WriteLine($"PayPal error {(int)raw.StatusCode}: {raw.ReadAsString()}");
            }
            return 8;
        }
    }
}
