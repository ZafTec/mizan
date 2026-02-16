using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Mcp.Server.Authentication;
using Mizan.Mcp.Server.Models;
using Mizan.Mcp.Server.Services;

namespace Mizan.Mcp.Server.Controllers;

[ApiController]
[Route("mcp")]
[Authorize(AuthenticationSchemes = McpTokenAuthenticationOptions.DefaultScheme)]
public class McpController : ControllerBase
{
    private readonly IBackendClient _backend;
    private readonly McpToolHandler _toolHandler;
    private readonly ILogger<McpController> _logger;

    // Store active SSE connections (simplified for single-instance)
    private static readonly ConcurrentDictionary<string, HttpResponse> _connections = new();

    public McpController(IBackendClient backend, McpToolHandler toolHandler, ILogger<McpController> logger)
    {
        _backend = backend;
        _toolHandler = toolHandler;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdClaim!);
    }

    private Guid GetTokenId()
    {
        var tokenIdClaim = User.FindFirst("mcp_token_id")?.Value;
        return Guid.Parse(tokenIdClaim!);
    }

    [HttpGet("sse")]
    [AllowAnonymous] // Auth is handled manually for SSE to support query parameter tokens
    public async Task ConnectSse()
    {
        // For SSE, we need to manually authenticate since the auth handler already ran
        // and set the User principal if a valid token was provided
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            Response.StatusCode = 401;
            return;
        }

        var userId = GetUserId();

        // 2. Setup SSE
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var sessionId = Guid.NewGuid().ToString();
        _logger.LogInformation("SSE Connected: {SessionId} User: {UserId}", sessionId, userId);

        // Send endpoint URL event
        var endpointEvent = new
        {
            type = "endpoint",
            uri = $"/mcp/messages?sessionId={sessionId}" 
        };
        await Response.WriteAsync($"event: endpoint\ndata: {JsonSerializer.Serialize(endpointEvent)}\n\n");
        await Response.Body.FlushAsync();

        try
        {
            // Keep connection open
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        finally
        {
            _logger.LogInformation("SSE Disconnected: {SessionId}", sessionId);
        }
    }

    [HttpPost("messages")]
    public async Task<IActionResult> HandleMessage([FromQuery] string sessionId, [FromBody] JsonRpcRequest request)
    {
        var userId = GetUserId();
        var tokenId = GetTokenId();

        _logger.LogInformation("Received method: {Method} from User: {UserId}", request.Method, userId);

        try
        {
            object? result = null;

            switch (request.Method)
            {
                case "initialize":
                    result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "mizan-mcp", version = "1.0.0" }
                    };
                    break;
                
                case "notifications/initialized":
                    // No response needed
                    return Ok();

                case "tools/list":
                    result = new { tools = _toolHandler.GetTools() };
                    break;

                case "tools/call":
                    if (request.Params == null) throw new Exception("Params missing");
                    var name = request.Params.Value.GetProperty("name").GetString();
                    var args = request.Params.Value.GetProperty("arguments");
                    var stopwatch = Stopwatch.StartNew();

                    try
                    {
                        var toolResult = await _toolHandler.ExecuteToolAsync(userId, name!, args);
                        var elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                        await _backend.LogUsageAsync(tokenId, userId, name!, args.GetRawText(), true, null, elapsedMs);

                        result = new
                        {
                            content = new[]
                            {
                                new { type = "text", text = JsonSerializer.Serialize(toolResult) }
                            }
                        };
                        break;
                    }
                    catch (Exception ex)
                    {
                        var elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                        await _backend.LogUsageAsync(tokenId, userId, name ?? "unknown", args.GetRawText(), false, ex.Message, elapsedMs);
                        throw;
                    }

                default:
                    throw new Exception("Method not found");
            }

            return Ok(new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RPC Error for User: {UserId}", userId);
            return Ok(new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError { Code = -32603, Message = ex.Message }
            });
        }
    }
}
