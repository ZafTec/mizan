# MCP Server Local Testing Guide

## Overview
This guide helps you test the MCP (Model Context Protocol) server locally using Docker.

## Current Status
✅ **MCP Server is fully implemented and running**

### What's Implemented:
- ✅ SSE (Server-Sent Events) transport at `/mcp/sse`
- ✅ JSON-RPC message endpoint at `/mcp/messages`
- ✅ 7 MCP Tools:
  1. `list_ingredients` - Search food ingredients
  2. `add_ingredient` - Add new food items
  3. `get_shopping_list` - Get user's shopping lists
  4. `get_nutrition_tracking` - Get daily nutrition summary
  5. `list_recipes` - Search recipes
  6. `add_recipe` - Create new recipes
  7. `log_meal` - Log food diary entries
- ✅ Token-based authentication
- ✅ Serilog logging to `/app/logs`

## Running Locally with Docker

### 1. Start All Services
```bash
docker-compose up -d postgres backend mcp
```

### 2. Verify Services are Running
```bash
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

Expected output:
```
NAMES            STATUS         PORTS
mizan-mcp        Up X minutes   0.0.0.0:5001->5001/tcp
mizan-backend    Up X minutes   0.0.0.0:5000->8080/tcp
mizan-redis      Up X minutes   0.0.0.0:6379->6379/tcp
mizan-postgres   Up X minutes   0.0.0.0:5432->5432/tcp
```

### 3. View MCP Server Logs
```bash
docker-compose logs -f mcp
```

## Testing the MCP Server

### Option 1: Using MCP Inspector (Recommended)

Install and run MCP Inspector:
```bash
npx @modelcontextprotocol/inspector
```

Then configure it to connect to:
- **SSE URL**: `http://localhost:5001/mcp/sse`
- **Token**: You'll need a valid MCP token from the database

### Option 2: Manual Testing with curl

#### Step 1: Create a test user and token
You'll need to:
1. Start the frontend: `cd frontend && bun run dev`
2. Register/login at http://localhost:3000
3. Navigate to Profile → MCP Integration
4. Generate a token
5. Copy the token (format: `mcp_xxxxxxxx...`)

#### Step 2: Test SSE Connection
```bash
curl -N -H "Authorization: Bearer YOUR_MCP_TOKEN" \
  http://localhost:5001/mcp/sse
```

#### Step 3: Test JSON-RPC Initialize
In another terminal, get the session ID from the SSE output, then:
```bash
curl -X POST \
  "http://localhost:5001/mcp/messages?sessionId=YOUR_SESSION_ID" \
  -H "Authorization: Bearer YOUR_MCP_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": {
        "name": "test-client",
        "version": "1.0.0"
      }
    }
  }'
```

#### Step 4: List Tools
```bash
curl -X POST \
  "http://localhost:5001/mcp/messages?sessionId=YOUR_SESSION_ID" \
  -H "Authorization: Bearer YOUR_MCP_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list"
  }'
```

#### Step 5: Call a Tool
```bash
curl -X POST \
  "http://localhost:5001/mcp/messages?sessionId=YOUR_SESSION_ID" \
  -H "Authorization: Bearer YOUR_MCP_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "list_ingredients",
      "arguments": {
        "search": "chicken",
        "limit": 10
      }
    }
  }'
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Claude for Desktop                      │
│                           │                                  │
│                           │ SSE + Bearer Token              │
│                           ▼                                  │
│  ┌─────────────────────────────────────────────────────┐    │
│  │            Mizan MCP Server (Port 5001)             │    │
│  │  ┌───────────────────────────────────────────────┐  │    │
│  │  │  McpController                                │  │    │
│  │  │  ├── POST /mcp/messages (JSON-RPC)           │  │    │
│  │  │  └── GET /mcp/sse (Server-Sent Events)       │  │    │
│  │  └───────────────────────────────────────────────┘  │    │
│  │  ┌───────────────────────────────────────────────┐  │    │
│  │  │  McpToolHandler                               │  │    │
│  │  │  ├── list_ingredients()                      │  │    │
│  │  │  ├── add_ingredient()                        │  │    │
│  │  │  ├── get_shopping_list()                     │  │    │
│  │  │  ├── get_nutrition_tracking()                │  │    │
│  │  │  ├── list_recipes()                          │  │    │
│  │  │  ├── add_recipe()                            │  │    │
│  │  │  └── log_meal()                              │  │    │
│  │  └───────────────────────────────────────────────┘  │    │
│  └─────────────────────────────────────────────────────┘    │
│                           │                                  │
│                           │ HTTP + Service API Key           │
│                           ▼                                  │
│  ┌─────────────────────────────────────────────────────┐    │
│  │            Mizan Backend API (Port 5000)            │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## Configuration

### MCP Server Configuration
Located in `backend/Mizan.Mcp.Server/appsettings.json`:
```json
{
  "MizanApiUrl": "http://localhost:5000",
  "ServiceApiKey": "change_this_to_a_secure_random_string_in_production"
}
```

### Environment Variables (Docker)
The MCP server uses these environment variables:
- `ASPNETCORE_ENVIRONMENT=Development`
- `ASPNETCORE_URLS=http://+:5001`
- `ConnectionStrings__PostgreSQL=Host=postgres;Database=mizan;...`

## Troubleshooting

### Container Won't Start
```bash
# Check logs
docker-compose logs mcp

# Rebuild
docker-compose build --no-cache mcp
docker-compose up -d mcp
```

### Database Connection Issues
```bash
# Verify postgres is running
docker-compose ps postgres

# Check database connectivity
docker-compose exec mcp bash -c "nc -zv postgres 5432"
```

### Authentication Failures
- Verify token format: `mcp_` + 64 characters
- Check token is active in database: `SELECT * FROM mcp_tokens WHERE is_active = true;`
- Ensure token hasn't expired

## Development Mode

To run the MCP server directly without Docker:
```bash
cd backend/Mizan.Mcp.Server
dotnet run
```

The server will start on http://localhost:5001

## Next Steps

1. **Frontend Testing**: Start the frontend and generate a real MCP token
2. **Claude Integration**: Configure Claude for Desktop with your local MCP server
3. **Load Testing**: Test with multiple concurrent connections
4. **Monitoring**: Check logs in `backend/Mizan.Mcp.Server/logs/`

## File Locations

- **MCP Server**: `backend/Mizan.Mcp.Server/`
- **Dockerfile**: `backend/Dockerfile.mcp`
- **Logs**: `backend/Mizan.Mcp.Server/logs/` (mounted in Docker)
- **Documentation**: `docs/MCP_INTEGRATION.md`
