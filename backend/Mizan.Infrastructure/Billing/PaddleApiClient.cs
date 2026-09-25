using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Billing;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Billing;

public class PaddleApiClient : IPaddleApiClient
{
    public const string HttpClientName = "paddle-api";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaddleOptions _options;
    private readonly ILogger<PaddleApiClient> _logger;

    public PaddleApiClient(
        IHttpClientFactory httpClientFactory, IOptions<PaddleOptions> options, ILogger<PaddleApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PaddlePortalSession?> CreatePortalSessionAsync(
        string customerId, string? subscriptionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return null;
        }

        var client = CreateClient();

        object body = subscriptionId is null
            ? new { }
            : new { subscription_ids = new[] { subscriptionId } };

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"customers/{Uri.EscapeDataString(customerId)}/portal-sessions", body, Json, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Never the customer id or the response body: both are billing
                // identifiers a log line has no business holding.
                _logger.LogWarning("Paddle portal session request failed with {Status}", (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<PortalSessionResponse>(Json, cancellationToken);
            var overview = payload?.Data?.Urls?.General?.Overview;
            if (string.IsNullOrWhiteSpace(overview))
            {
                return null;
            }

            var subscription = payload!.Data!.Urls!.Subscriptions?.FirstOrDefault();

            return new PaddlePortalSession(
                overview,
                subscription?.CancelSubscription,
                subscription?.UpdateSubscriptionPaymentMethod);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Paddle portal session request could not reach Paddle");
            return null;
        }
    }

    public async Task<string> EnsureProductAsync(string plan, string name, string description, CancellationToken cancellationToken)
    {
        using var list = await SendAsync(HttpMethod.Get, "products?status=active&per_page=200", null, cancellationToken);
        foreach (var product in list.RootElement.GetProperty("data").EnumerateArray())
        {
            if (product.TryGetProperty("custom_data", out var custom)
                && custom.ValueKind == JsonValueKind.Object
                && PaddleSubscriptionState.GetString(custom, "plan") == plan
                && PaddleSubscriptionState.GetString(custom, "product") is null or PaddleSubscriptionState.ProductTag
                && PaddleSubscriptionState.GetString(product, "id") is { } existing)
            {
                return existing;
            }
        }

        var body = new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["tax_category"] = "standard",
            ["custom_data"] = new JsonObject { ["plan"] = plan, ["product"] = PaddleSubscriptionState.ProductTag }
        };
        using var created = await SendAsync(HttpMethod.Post, "products", body, cancellationToken);
        return created.RootElement.GetProperty("data").GetProperty("id").GetString()!;
    }

    public async Task<PaddlePrice> CreatePriceAsync(PaddlePriceRequest request, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["product_id"] = request.ProductId,
            ["name"] = request.Name,
            // Paddle requires an internal description; the plan name serves.
            ["description"] = string.IsNullOrWhiteSpace(request.Description) ? request.Name : request.Description,
            ["billing_cycle"] = new JsonObject { ["interval"] = request.Interval, ["frequency"] = 1 },
            ["unit_price"] = new JsonObject
            {
                ["amount"] = request.AmountCents.ToString(CultureInfo.InvariantCulture),
                ["currency_code"] = request.Currency
            },
            // One subscription per account: a quantity above one would be a second seat nobody can use.
            ["quantity"] = new JsonObject { ["minimum"] = 1, ["maximum"] = 1 }
        };
        if (request.TrialDays is > 0)
        {
            body["trial_period"] = new JsonObject
            {
                ["interval"] = "day",
                ["frequency"] = request.TrialDays.Value,
                ["requires_payment_method"] = true
            };
        }

        using var created = await SendAsync(HttpMethod.Post, "prices", body, cancellationToken);
        return ParsePrice(created.RootElement.GetProperty("data"));
    }

    public async Task ArchivePriceAsync(string priceId, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["status"] = "archived" };
        using var _ = await SendAsync(HttpMethod.Patch, $"prices/{Uri.EscapeDataString(priceId)}", body, cancellationToken);
    }

    public async Task<IReadOnlyList<PaddlePrice>> ListPricesAsync(string productId, CancellationToken cancellationToken)
    {
        using var list = await SendAsync(
            HttpMethod.Get,
            $"prices?product_id={Uri.EscapeDataString(productId)}&status=active&recurring=true&per_page=200",
            null,
            cancellationToken);
        return list.RootElement.GetProperty("data").EnumerateArray().Select(ParsePrice).ToList();
    }

    public async Task<string> CreateDiscountAsync(PaddleDiscountRequest request, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["description"] = request.Label,
            ["type"] = request.Type,
            ["amount"] = request.Type == "flat"
                ? ((int)request.Amount).ToString(CultureInfo.InvariantCulture)
                : request.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            ["enabled_for_checkout"] = true,
            ["recur"] = request.Recurring
        };
        if (request.Type == "flat")
        {
            body["currency_code"] = request.Currency;
        }
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            body["code"] = request.Code;
        }
        if (request.Recurring && request.MaximumRecurringIntervals is > 0)
        {
            body["maximum_recurring_intervals"] = request.MaximumRecurringIntervals.Value;
        }
        if (request.ExpiresAt is not null)
        {
            body["expires_at"] = request.ExpiresAt.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
        if (request.RestrictToPriceIds.Count > 0)
        {
            body["restrict_to"] = new JsonArray(request.RestrictToPriceIds.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
        }

        using var created = await SendAsync(HttpMethod.Post, "discounts", body, cancellationToken);
        return created.RootElement.GetProperty("data").GetProperty("id").GetString()!;
    }

    public async Task ArchiveDiscountAsync(string discountId, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["status"] = "archived" };
        using var _ = await SendAsync(HttpMethod.Patch, $"discounts/{Uri.EscapeDataString(discountId)}", body, cancellationToken);
    }

    public async Task<PaddleChangePreview> PreviewPriceChangeAsync(string subscriptionId, string priceId, CancellationToken cancellationToken)
    {
        using var preview = await SendAsync(
            HttpMethod.Patch,
            $"subscriptions/{Uri.EscapeDataString(subscriptionId)}/preview",
            ChangeBody(priceId),
            cancellationToken);
        var data = preview.RootElement.GetProperty("data");

        var currency = PaddleSubscriptionState.GetString(data, "currency_code") ?? "USD";
        var dueNow = 0;
        if (data.TryGetProperty("update_summary", out var summary) && summary.ValueKind == JsonValueKind.Object
            && summary.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
        {
            var amount = ParseCents(PaddleSubscriptionState.GetString(result, "amount"));
            dueNow = PaddleSubscriptionState.GetString(result, "action") == "credit" ? -amount : amount;
            currency = PaddleSubscriptionState.GetString(result, "currency_code") ?? currency;
        }

        int? nextAmount = null;
        if (data.TryGetProperty("recurring_transaction_details", out var recurring) && recurring.ValueKind == JsonValueKind.Object
            && recurring.TryGetProperty("totals", out var totals))
        {
            nextAmount = ParseCents(PaddleSubscriptionState.GetString(totals, "grand_total")
                ?? PaddleSubscriptionState.GetString(totals, "total"));
        }

        return new PaddleChangePreview(dueNow, currency, PaddleSubscriptionState.GetDateTime(data, "next_billed_at"), nextAmount);
    }

    public async Task<PaddleSubscriptionState> ChangePriceAsync(string subscriptionId, string priceId, CancellationToken cancellationToken)
    {
        var body = ChangeBody(priceId);
        // A declined proration charge leaves the plan as it was rather than
        // switching someone onto a price they have not paid for.
        body["on_payment_failure"] = "prevent_change";
        using var updated = await SendAsync(
            HttpMethod.Patch, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}", body, cancellationToken);
        return PaddleSubscriptionState.Parse(updated.RootElement.GetProperty("data"));
    }

    public async Task<PaddleSubscriptionState> CancelAtPeriodEndAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["effective_from"] = "next_billing_period" };
        using var canceled = await SendAsync(
            HttpMethod.Post, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}/cancel", body, cancellationToken);
        return PaddleSubscriptionState.Parse(canceled.RootElement.GetProperty("data"));
    }

    public async Task<PaddleSubscriptionState> RemoveScheduledChangeAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["scheduled_change"] = null };
        using var updated = await SendAsync(
            HttpMethod.Patch, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}", body, cancellationToken);
        return PaddleSubscriptionState.Parse(updated.RootElement.GetProperty("data"));
    }

    public async Task<IReadOnlyList<PaddleTransaction>> ListTransactionsAsync(string customerId, CancellationToken cancellationToken)
    {
        using var list = await SendAsync(
            HttpMethod.Get,
            $"transactions?customer_id={Uri.EscapeDataString(customerId)}&status=billed,paid,completed,past_due&order_by=billed_at[DESC]&per_page=24",
            null,
            cancellationToken);

        var transactions = new List<PaddleTransaction>();
        foreach (var t in list.RootElement.GetProperty("data").EnumerateArray())
        {
            var totals = t.TryGetProperty("details", out var details) && details.TryGetProperty("totals", out var tt)
                ? tt
                : default;
            transactions.Add(new PaddleTransaction(
                PaddleSubscriptionState.GetString(t, "id")!,
                PaddleSubscriptionState.GetString(t, "status") ?? "completed",
                PaddleSubscriptionState.GetDateTime(t, "billed_at") ?? PaddleSubscriptionState.GetDateTime(t, "created_at"),
                ParseCents(PaddleSubscriptionState.GetString(totals, "grand_total") ?? PaddleSubscriptionState.GetString(totals, "total")),
                PaddleSubscriptionState.GetString(t, "currency_code") ?? "USD",
                PaddleSubscriptionState.GetString(t, "invoice_number")));
        }

        return transactions;
    }

    public async Task<string?> GetInvoiceUrlAsync(string transactionId, string customerId, CancellationToken cancellationToken)
    {
        var path = $"transactions/{Uri.EscapeDataString(transactionId)}";
        using var transaction = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        if (PaddleSubscriptionState.GetString(transaction.RootElement.GetProperty("data"), "customer_id") != customerId)
        {
            return null;
        }

        using var invoice = await SendAsync(HttpMethod.Get, $"{path}/invoice", null, cancellationToken);
        return invoice.RootElement.GetProperty("data").TryGetProperty("url", out var url) ? url.GetString() : null;
    }

    private static JsonObject ChangeBody(string priceId) => new()
    {
        ["items"] = new JsonArray(new JsonObject { ["price_id"] = priceId, ["quantity"] = 1 }),
        // Moving to yearly charges the difference now; moving to monthly
        // leaves a credit that pays toward the next bills.
        ["proration_billing_mode"] = "prorated_immediately"
    };

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(BaseUrl());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return client;
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new PaddleRequestException(null, "not_configured", "Billing is not configured on this server.");
        }

        var client = CreateClient();
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Paddle {Method} request could not reach Paddle", method.Method);
            throw new PaddleRequestException(null, null, "Could not reach Paddle. Try again in a moment.");
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return JsonDocument.Parse(content);
            }

            // Paddle's error code and detail describe the request, never the
            // customer, so they are safe to log and to show an admin.
            string? code = null, detail = null;
            try
            {
                using var error = JsonDocument.Parse(content);
                if (error.RootElement.TryGetProperty("error", out var e))
                {
                    code = PaddleSubscriptionState.GetString(e, "code");
                    detail = PaddleSubscriptionState.GetString(e, "detail");
                }
            }
            catch (JsonException)
            {
            }

            _logger.LogWarning("Paddle {Method} request failed with {Status} {Code}", method.Method, (int)response.StatusCode, code);
            throw new PaddleRequestException((int)response.StatusCode, code, detail ?? "Paddle refused the request.");
        }
    }

    private static PaddlePrice ParsePrice(JsonElement p)
    {
        string? interval = null;
        if (p.TryGetProperty("billing_cycle", out var cycle) && cycle.ValueKind == JsonValueKind.Object)
        {
            var frequency = cycle.TryGetProperty("frequency", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 1;
            interval = frequency == 1 ? PaddleSubscriptionState.GetString(cycle, "interval") : null;
        }

        int? trialDays = null;
        if (p.TryGetProperty("trial_period", out var trial) && trial.ValueKind == JsonValueKind.Object
            && trial.TryGetProperty("frequency", out var tf) && tf.ValueKind == JsonValueKind.Number)
        {
            var unit = PaddleSubscriptionState.GetString(trial, "interval");
            trialDays = unit switch
            {
                "day" => tf.GetInt32(),
                "week" => tf.GetInt32() * 7,
                _ => null
            };
        }

        var unitPrice = p.GetProperty("unit_price");
        return new PaddlePrice(
            PaddleSubscriptionState.GetString(p, "id")!,
            PaddleSubscriptionState.GetString(p, "product_id")!,
            PaddleSubscriptionState.GetString(p, "name") ?? PaddleSubscriptionState.GetString(p, "description") ?? "Pro",
            PaddleSubscriptionState.GetString(p, "description"),
            interval,
            ParseCents(PaddleSubscriptionState.GetString(unitPrice, "amount")),
            PaddleSubscriptionState.GetString(unitPrice, "currency_code") ?? "USD",
            trialDays);
    }

    private static int ParseCents(string? amount) =>
        long.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cents)
            ? (int)Math.Clamp(cents, int.MinValue, int.MaxValue)
            : 0;

    private string BaseUrl() => _options.Environment.Equals("production", StringComparison.OrdinalIgnoreCase)
        ? "https://api.paddle.com/"
        : "https://sandbox-api.paddle.com/";

    private sealed record PortalSessionResponse
    {
        [JsonPropertyName("data")] public PortalSessionData? Data { get; init; }
    }

    private sealed record PortalSessionData
    {
        [JsonPropertyName("urls")] public PortalUrls? Urls { get; init; }
    }

    private sealed record PortalUrls
    {
        [JsonPropertyName("general")] public GeneralUrls? General { get; init; }
        [JsonPropertyName("subscriptions")] public List<SubscriptionUrls>? Subscriptions { get; init; }
    }

    private sealed record GeneralUrls
    {
        [JsonPropertyName("overview")] public string? Overview { get; init; }
    }

    private sealed record SubscriptionUrls
    {
        [JsonPropertyName("cancel_subscription")] public string? CancelSubscription { get; init; }
        [JsonPropertyName("update_subscription_payment_method")] public string? UpdateSubscriptionPaymentMethod { get; init; }
    }
}
