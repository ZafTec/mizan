# AI platform

Mizan's assistant, food-photo analysis, onboarding, trainer questions, and prompt evaluations use one backend AI platform. Web, MCP, and Telegram call that platform rather than calling a model provider independently.

## Configuration

The provider speaks the OpenAI-compatible chat-completions protocol. Set the Compose variables `AI_BASE_URL`, `AI_API_KEY`, and `AI_MODEL`, or the equivalent backend `Ai:BaseUrl`, `Ai:ApiKey`, and `Ai:Model` configuration. An empty endpoint disables AI while basic logging remains available.

`AI_SUPPORTS_TEMPERATURE=false` omits temperature for providers that reject it. `AI_GLOBAL_DAILY_TOKENS` and `AI_GLOBAL_DAILY_COST_MICROS` cap provider use across the application. Configure provider prices accurately when relying on the cost ceiling. Token and cost ceilings are enforced by code, not prompt text.

## Consent

`GET` and `PUT /api/Ai/consent` expose the current user's consent. Three read axes start off: nutrition, training, and body measurements. Write permission is separately controlled by the global `allowWrites` switch plus per-axis write flags.

`IDataAccessPolicy` determines which axes may be fetched into AI context. A trainer must have both an active client grant for the axis and that client's AI consent. Missing permission means the data is absent from context. Access is re-evaluated at execution, including allowlisted AI tools, so a revoked grant is not extended by an old conversation.

Normal chat and food analysis produce responses or proposals for review. Onboarding is a distinct tool-enabled flow: it can perform allowlisted setup writes when the user granted the relevant write permission. Its tools run existing MediatR commands with normal validation and audit behavior, and cannot supply arbitrary user IDs or call arbitrary endpoints. Do not describe all AI as read-only, and do not broaden the allowlist through editable prompts.

Weight writes require body-axis permission and belong to the measurement tool; goal tools cannot bypass that consent. Render model-supplied Markdown images as links so a reply cannot trigger automatic requests to external image hosts.

## Metering

Every provider call reserves capacity through `IAiQuotaService`, then settles actual usage. The ledger counts failures and administrative evaluations appropriately. There are separate daily allowance lines:

| Line | Default requests | Default tokens |
| --- | ---: | ---: |
| Free personal | 5 | 20,000 |
| Pro personal | 200 | 500,000 |
| Onboarding | 60 | 60,000 |
| Trainer-client | 300 | 600,000 |
| Evaluation | 400 | 400,000 |

Defaults come from `AiOptions` and are configurable. Every line shares the global default ceilings of 5,000,000 tokens and 20,000,000 cost micros per UTC day. Trainer usage belongs to the trainer, so a coach cannot consume a client's allowance. Chat is quota-limited on Free; it is not uniformly Pro-only. Image analysis has its own entitlement gate.

`GET /api/Ai/usage` reports personal usage. Administrators use `GET /api/Ai/usage/global`. Reservation, provider call, and settlement belong together; adding a direct unmetered provider call is a billing defect.

## Surfaces

| Endpoint | Purpose |
| --- | --- |
| `POST /api/Ai/chat` | Shared persistent chat |
| `POST /api/Ai/chat/image` | Image in a chat turn |
| `GET /api/Ai/threads`, `/threads/{id}` | Conversation history |
| `POST /api/Ai/suggestions` | Structured meal suggestions |
| `GET/POST /api/Ai/onboarding` | Setup conversation and state |
| `GET /api/Ai/onboarding/tools` | Setup allowlist |
| `POST /api/Ai/clients/{clientId}/ask` | Trainer question under intersected grants |
| `POST /api/Nutrition/ai/analyze-image` | Food-photo proposal |

Structured responses must pass their declared schema before use. A malformed provider result is an error, not something to recover by scraping prose. Photo proposals show quantities and nutrition for confirmation before logging.

## Prompt administration

The administrator console at `/admin/ai` manages draft versions, synthetic evaluation cases, comparison, publishing, and rollback. A published version remains selected until another eligible version is published. The publish gate requires the current draft's evaluation evidence; editing a draft invalidates older passing evidence.

Editable instructions control tone and task guidance. Code-owned constraints control data access, write tools, output validation, and quota. The console displays these constraints read-only. Evaluations run synthetic cases, consume the evaluation allowance and global ceiling, and execute through the durable outbox. Administrator access does not make arbitrary users' private data available for prompt testing.

## Verification

PostgreSQL integration tests cover consent, trainer intersections, quota concurrency, chat/onboarding, and publish gates. Provider fakes in those tests allow deterministic error and quota scenarios; they do not prove a deployment's external endpoint or model compatibility. A configured provider requires a separate live request, with usage inspected afterward. Browser fixture tests likewise establish interface behavior without spending provider quota.
