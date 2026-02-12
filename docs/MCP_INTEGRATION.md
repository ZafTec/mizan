# MCP Integration Guide

**Last Updated:** 2026-01-08
**Status:** in-progress
**Version:** 1.1.0

## Overview

MacroChef exposes a Model Context Protocol (MCP) server so assistants like Claude can securely read and write your nutrition data: ingredients, recipes, shopping lists, and daily tracking. The MCP server sits beside the main API and uses bearer tokens you generate from the web app.

## Table of Contents
- [What is MCP?](#what-is-mcp)
- [Architecture](#architecture)
- [User Setup Guide](#user-setup-guide)
- [Available MCP Tools](#available-mcp-tools)
- [Security](#security)
- [Development Guide](#development-guide)
- [Deployment](#deployment)
- [Troubleshooting](#troubleshooting)
- [Monitoring](#monitoring)
- [Roadmap](#roadmap)
- [Support](#support)
- [References](#references)

## What is MCP?
MCP is an open standard that lets an AI call tools over a simple transport. Instead of hand-writing API prompts, Claude can directly invoke typed tools for MacroChef.

**Example interactions:**
- "Show me my meals for today"
- "Add chicken breast to my ingredients"
- "Create a new recipe using the ingredients I have"
- "Log 2 servings of pasta for dinner"
- "What's my protein intake this week?"

## Architecture
### Components
```
Claude for Desktop
        |
        |  npx @modelcontextprotocol/server-sse
        |  connects to https://mcp.yourdomain.com/mcp (SSE + Bearer token)
        v
Mizan MCP Server (ASP.NET Core)
    - Port: 5001
    - TokenAuthenticationMiddleware (MCP token -> user)
    - MCP tools: ingredients, recipes, shopping, tracking, meals
    - Serilog logs -> /app/logs
        |
        v
PostgreSQL
    - mcp_tokens, mcp_usage_logs
    - foods, recipes, meals, measurements
```

### Authentication Flow
1. User generates MCP token via web UI
2. Token hash stored in database
3. Claude config includes `AUTHORIZATION: Bearer <token>`
4. MCP server validates token on each request
5. UserId cached for the request lifetime
6. Tools execute with authenticated user context

### Transport Protocol
- **Protocol:** Server-Sent Events (SSE) over HTTP
- **Port:** 5001 (configurable)
- **Path:** `/mcp`
- **Authentication:** Bearer token in `Authorization` header
- **Logging:** Serilog to `/app/logs`

## User Setup Guide
### Step 1: Generate MCP Token
1. Log into MacroChef web interface
2. Navigate to **Profile -> MCP Integration**
3. Click **Generate New MCP Token**
4. Name it ("Home Laptop", "Work PC")
5. Copy the token (it is shown once)

### Step 2: Configure Claude for Desktop
1. Locate your config file:
   - macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
   - Windows: `%APPDATA%\Claude\claude_desktop_config.json`

2. Add the MCP server entry.

**Local development**
```json
{
  "mcpServers": {
    "mizan": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sse", "http://localhost:5001/mcp"],
      "env": { "AUTHORIZATION": "Bearer mcp_your_token_here" }
    }
  }
}
```

**Production**
```json
{
  "mcpServers": {
    "mizan": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sse", "https://mcp.yourdomain.com/mcp"],
      "env": { "AUTHORIZATION": "Bearer mcp_your_token_here" }
    }
  }
}
```

3. Save and fully restart Claude for Desktop.
   - Windows: Right-click tray icon -> Quit
   - macOS: Cmd+Q

### Step 3: Test the connection
Run in Claude:
- "List my ingredients"
- "Show my recipes"
- "What did I eat today?"

## Available MCP Tools
### 1. list_ingredients
Search and list ingredients.
- `searchTerm` (optional)
- `includePublic` (optional, default: true)

### 2. add_ingredient
Create a new ingredient.
- `name` (required)
- `servingSize` (required)
- `servingUnit` (required)
- `caloriesPer100g`, `proteinPer100g`, `carbsPer100g`, `fatPer100g` (required)
- `brand`, `barcode` (optional)
- `isPublic` (optional, default: false)

### 3. get_shopping_list
Fetch your latest shopping list.
- `includeCompleted` (default: false)

### 4. get_nutrition_tracking
Get nutrition totals across a date range.
- `startDate`, `endDate` (YYYY-MM-DD)
- `includeBodyMeasurements` (default: true)

### 5. list_recipes
Search recipes.
- `searchTerm`, `tags`, `includePublic`, `favoritesOnly`

### 6. add_recipe
Create a recipe from ingredients and instructions.
- `title`, `servings` (required)
- `description`, `prepTimeMinutes`, `cookTimeMinutes`, `tags`, `isPublic` (optional)
- `ingredients` array of `{ foodId, quantity, unit }`
- `instructions` array of `{ stepNumber, instruction }`

### 7. log_meal
Log a diary entry.
- `mealType` (breakfast|lunch|dinner|snack)
- `foodId` or `recipeId` (optional)
- `servings` (required)
- `entryDate` (optional, default today)
- `name` (optional when no food/recipe)

## Security
### Token Management
- Format: `mcp_` + 64 random chars (68 total)
- Stored as SHA256 hash
- Valid only if `is_active` true and not expired
- `last_used_at` is updated on successful calls
- Users can revoke tokens in the UI

### Rate Limiting (planned)
- 100 req/min and 1000 req/hour per token

### Audit Logging
Every tool call logs: timestamp, user, token, tool name, parameters, success/failure, execution time. Logs live in `/app/logs/mcp-YYYY-MM-DD.log` and the `mcp_usage_logs` table.

## Development Guide
### Project Structure
```
backend/Mizan.Mcp.Server/
├── Program.cs                        # MCP host with SSE
├── Middleware/
│   └── TokenAuthenticationMiddleware.cs
├── Services/
│   └── McpUsageLogger.cs             # Writes mcp_usage_logs
├── Tools/
│   ├── IngredientTools.cs
│   ├── RecipeTools.cs
│   ├── MealTools.cs
│   ├── ShoppingTools.cs
│   └── TrackingTools.cs
├── Mizan.Mcp.Server.csproj
├── Dockerfile.mcp
├── appsettings.json
└── appsettings.Production.json
```

### Adding a new MCP tool
1) Create a static method in a `[McpServerToolType]` class and decorate with `[McpServerTool]`.
2) Inject required services (e.g., `IMediator`) and add parameter descriptions.
3) Log usage with `IMcpUsageLogger`.
4) Run `dotnet build` to verify registration (tools auto-registered via `WithToolsFromAssembly`).

### Running locally
```bash
cd backend/Mizan.Mcp.Server
dotnet run
```

### Environment Variables
**Development** (appsettings.Development.json)
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=mizan;Username=mizan;Password=mizan_dev_password"
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Debug" }
  }
}
```

**Production** (docker-compose)
```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - ASPNETCORE_URLS=http://+:5001
  - ConnectionStrings__PostgreSQL=Host=postgres;Port=5432;Database=mizan;Username=mizan;Password=${POSTGRES_PASSWORD}
```

## Deployment
### Docker
```bash
# Build MCP server image
docker-compose build mcp
# Start MCP server
docker-compose up -d mcp
# Health check
curl http://localhost:5001/health
# Logs
docker logs -f mizan-mcp
```

### Production checklist
- ASPNETCORE_ENVIRONMENT=Production
- Strong PostgreSQL password
- HTTPS termination (nginx/Traefik)
- Log rotation (30d)
- Health monitoring
- Backup `mcp_tokens` and `mcp_usage_logs`

### Reverse proxy (nginx)
```nginx
location /mcp {
    proxy_pass http://localhost:5001;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_buffering off;
    proxy_cache off;
    proxy_read_timeout 86400;
}
location /health {
    proxy_pass http://localhost:5001;
    access_log off;
}
```

## Troubleshooting
**Claude cannot connect**
- Check `curl http://localhost:5001/health`
- Validate claude_desktop_config.json syntax
- Confirm token format and length
- Tail logs: `docker logs -f mizan-mcp`
- Fully restart Claude (tray icon -> Quit)

**401 Unauthorized**
- Token revoked/expired
- Wrong token value
- Regenerate token and update config

**Server crashes**
- Check database connectivity
- Apply migrations (`dotnet ef database update`)
- Review logs in `/app/logs`

**Slow responses**
- Inspect database indexes
- Keep date ranges small
- Watch execution_time_ms in usage logs

## Monitoring
- Health endpoint: `GET /health`
- Planned: Prometheus metrics (token counts, latency, error rate)

## Roadmap
- Rate limiting per token
- Token usage analytics dashboard
- Webhook notifications for token usage
- Prometheus metrics and OpenTelemetry
- Token scopes/permissions

## Support
- GitHub Issues: [macro_chef/issues](https://github.com/yourusername/macro_chef/issues)
- Documentation: [docs/](../docs/)
- API Reference: [docs/API_REFERENCE.md](./API_REFERENCE.md)

## References
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [MCP Server Quickstart](https://modelcontextprotocol.io/quickstart)
- [MacroChef Architecture](./ARCHITECTURE.md)
- [Security Documentation](./SECURITY.md)
