# Mizan

Mizan logs meals, workouts, and body measurements. **Today** brings those three logs together, **History** opens previous days, and **Progress** shows nutrition, bodyweight, and exercise trends. The global **Log entry** action keeps meal and measurement entry over the current page; workouts have a session view with saved drafts.

Recipes, meal plans, shopping lists, trainers, households, messaging, billing, and settings live under **More**. A meal with two or more items can become a recipe, and foods and recipes share one logging picker. MCP clients and Telegram use the same backend commands and permissions as the app.

## Stack

- Next.js 16, React 19, TypeScript, Tailwind CSS, and Bun.
- ASP.NET Core 10, MediatR, FluentValidation, and EF Core.
- PostgreSQL 18 owns all application and identity data. Next.js has no database connection.
- Redis provides distributed caching and the SignalR backplane.
- S3-compatible storage supports MinIO and Cloudflare R2.
- Backend session cookies authenticate browsers. User MCP tokens authenticate external MCP clients.

## Run with Docker

Install Docker with Compose, then:

```sh
cp .env.example .env
```

Set `DB_PASSWORD`, `REDIS_PASSWORD`, `MCP_SERVICE_KEY`, `MCP_ADMIN_SERVICE_KEY`, `STORAGE_ACCESS_KEY_ID`, and `STORAGE_SECRET_ACCESS_KEY` to local development values. Set `PUBLIC_APP_URL=http://localhost:3000`. These service credentials are required by Compose; SMTP, OAuth, Paddle, AI, and Telegram are optional for basic logging.

```sh
docker compose up -d
docker compose ps
```

| Service | Development address |
| --- | --- |
| App | http://localhost:3000 |
| API and Swagger | http://localhost:5000/swagger |
| MCP | http://localhost:5001/mcp |
| PostgreSQL | localhost:5432 |
| Redis | localhost:6379 |
| MinIO console | http://localhost:9001 |

An untracked `docker-compose.override.yml` may change local ports; `docker compose ps` shows actual addresses. Development startup applies EF migrations. Production configuration lives in `docker-compose.prod.yml`.

Register through the app and confirm your email. With SMTP unset in Development, the backend prints the verification message to its console. There is no default administrator password. Verification links are credentials; keep them out of shared reports.

```sh
docker compose logs -f backend frontend
docker compose down
```

## Run on the host

Install Bun and .NET 10. Start PostgreSQL and Redis, then configure the backend through environment variables or ignored `backend/Mizan.Api/appsettings.Development.json`:

```text
ConnectionStrings__PostgreSQL=Host=localhost;Database=mizan;Username=mizan;Password=<local password>
ConnectionStrings__Redis=localhost:6379,password=<local password>
App__PublicUrl=http://localhost:3000
```

```sh
dotnet run --project backend/Mizan.Api
```

In a separate terminal, create `frontend/.env.local` with `API_URL=http://localhost:5000` and `NEXT_PUBLIC_API_URL=http://localhost:5000`, then:

```sh
cd frontend
bun install --frozen-lockfile
bun run dev
```

Server components forward the session cookie to `API_URL`. Browser requests include credentials at `NEXT_PUBLIC_API_URL`. Production serves the app, `/api`, `/hubs`, and `/mcp` on one public origin through the reverse proxy.

## Verify changes

```sh
cd frontend
bun run lint
bun run test
bun run build
bunx playwright install chromium
bun run test:e2e
```

If Windows fork workers fail to start, use `bunx vitest run --pool=threads --maxWorkers=1`. Playwright starts the real Next.js UI on port 3100 and a synthetic API on port 5100. It checks browser navigation, forms, persisted fixture writes, keyboard interaction, and layout. **It does not verify the .NET API, PostgreSQL, or external services.**

Set `PLAYWRIGHT_BASE_URL` and `PLAYWRIGHT_API_URL` to change ports. `PLAYWRIGHT_EXTERNAL=1` disables service startup; the suite still needs its fixture session endpoints. With the fixture services running, capture a synthetic screenshot using `bun e2e/capture.ts /today`; add `--mobile --empty` or `--dark` as needed. Reports and screenshots are Git-ignored.

Run backend tests separately:

```sh
docker compose --profile test run --rm test
```

The integration fixture truncates its database between tests. Use only a disposable database whose name contains `test`; `TEST_DB_CONNECTION` can select one for a host run. With no connection configured, the fixture creates a PostgreSQL Testcontainer. Never target a development or production database.

After API contract changes, run the API and `bun run codegen` in `frontend` to regenerate `types/api.generated.ts`. Zod form validation is maintained separately; there is no `codegen:zod` script.

## Maintain the database

Keep the current EF migration history and add migrations for schema changes. Do not regenerate or reset `InitialCreate`. After entity or DbContext changes, check model agreement from `backend`:

```sh
dotnet ef migrations has-pending-model-changes --project Mizan.Infrastructure --startup-project Mizan.Api
```

## Releases

Release Please opens the first release pull request for **v3.0.0**, then proposes later versions from Conventional Commits: `fix:` increments patch, `feat:` increments minor, and `!` or a `BREAKING CHANGE:` footer increments major. Keep these prefixes in commit messages or squash-merge titles. Release PRs maintain the changelog, `version.txt`, the release manifest, and the frontend package version together.

Merge a release PR to publish it. After all three images build, the workflow creates a draft release, promotes their exact digests to Docker tags `vX.Y.Z` and `X.Y.Z`, and publishes the release with deployment instructions. Existing commit and `latest` image tags continue on each eligible `master` push. If publication fails, choose **Re-run failed jobs** on that workflow run to retain the successful builds' original digests. Pending drafts resume and completed releases stay unchanged.

The workflow uses the existing organization secrets `RELEASE_APP_ID` and `RELEASE_APP_PRIVATE_KEY` to create a token scoped only to this repository. The GitHub App needs Contents and Pull requests write permissions, allowing release PRs to run CI. The same App can serve other repositories without sharing their versions, tags, or release PRs.

Production Compose currently follows `latest`. To pin a release, use its version tag for all three application images before running the existing deployment helper. GitHub release notes include the image digests and deployment commands.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Design](DESIGN.md)
- [MCP setup and tools](docs/MCP.md)
- [AI consent, limits, and administration](docs/AI.md)
- [Telegram setup](docs/TELEGRAM.md)
- [Contributor and agent guidance](CLAUDE.md)
- [Security policy](SECURITY.md)

## License

MIT.
