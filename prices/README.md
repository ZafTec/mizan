# Prices worker

`Mizan.Prices` scrapes shop prices for the price book and hands every observation to the API. It owns no tables and no pricing rules: the API resolves product names to foods, stores prices, and records runs. Read [planning](../docs/PLANNING.md#prices) for the design.

## Configuration

Section `Prices`, as environment variables `Prices__<Key>`:

| Key | Default | Meaning |
| --- | --- | --- |
| `ApiUrl` | | The API on the internal network, for example `http://backend:8080`. Empty keeps the worker idle. |
| `ServiceApiKey` | | The non-admin service key (`MCP_SERVICE_KEY`). Sent as `X-Api-Key`; also required on `POST /run/{sourceId}`. |
| `Redis` | | StackExchange.Redis connection string for the per-source run lock. Empty uses an in-process lock, which is only safe with one instance. |
| `UserAgent` | `MizanPriceBot/1.0 (+https://mizan.zaftech.co; prices@zaftech.co)` | Sent on every request to a shop and matched against its `robots.txt`. |
| `RequestDelayMs` | `1000` | Minimum gap between requests to one host. A longer `Crawl-delay` wins. |
| `MaxProductsPerRun` | `500` | Product pages one run may fetch. |

HTTP surface, port 5003: `GET /health` returns `{ status, configured, sources }`; `POST /run/{sourceId}` with the service key in `X-Api-Key` starts a run now and answers `202`, `401` without the key, `404` for a source the API does not list as enabled, and `502` when the API cannot be reached.

## Run locally

```bash
cd prices/Mizan.Prices
Prices__ApiUrl=http://localhost:5000 Prices__ServiceApiKey=<MCP_SERVICE_KEY> Prices__Redis=localhost:6379,password=<local password> dotnet run
curl -s localhost:5003/health
curl -s -X POST -H "X-Api-Key: <MCP_SERVICE_KEY>" localhost:5003/run/<sourceId>
```

With Compose, `docker compose up prices` starts it beside the API. Production enables it with `COMPOSE_PROFILES=prices`.

Tests: `dotnet test prices/Mizan.Prices.Tests`. They run against recorded pages in `Mizan.Prices.Tests/Fixtures`; the one Redis test passes without a Redis and exercises the lock when `localhost:6379` answers (`TEST_REDIS_CONNECTION` overrides the address).

## Add a scraper

1. Implement `IPriceScraper` in `Mizan.Prices/Scraping/`. `Key` is what the administrator types into the source form. `ScrapeAsync` fetches every page through `ScrapeContext.GetStringAsync`, calls `ctx.TryReserveProduct()` before each product page, and yields a `PriceObservation` per priced product. Keep the parsing in static methods so the tests can feed it recorded HTML.
2. Register it in `Program.cs`: `builder.Services.AddSingleton<IPriceScraper, YourScraper>();`.
3. Record a sitemap and one product page into `Mizan.Prices.Tests/Fixtures` (trim the page to the metadata the parser reads, under 20 KB) and add a test like `AradaMartScraperTests`.
4. Note the site's terms and its `robots.txt` in the targets table in [planning](../docs/PLANNING.md#service).

`ScrapeContext` already handles `robots.txt`, request spacing, the User-Agent, the 30-second timeout and the per-run cap. A scraper that needs a browser (asbeza.net) or is refused by the site (numbeo.com) is not a scraper for this worker; its prices are entered by hand.
