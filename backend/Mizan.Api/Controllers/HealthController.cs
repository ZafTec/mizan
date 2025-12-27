using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReportDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get()
    {
        var report = await _healthCheckService.CheckHealthAsync();

        var response = new HealthReportDto
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            System = new SystemInfoDto
            {
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                MemoryUsageBytes = GC.GetTotalMemory(false),
                UptimeSeconds = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds
            },
            Entries = report.Entries.Select(e => new HealthCheckEntryDto
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration.TotalMilliseconds,
                Tags = e.Value.Tags
            }).ToList()
        };

        return report.Status == HealthStatus.Healthy ? Ok(response) : StatusCode(503, response);
    }
}

public class HealthReportDto
{
    public string Status { get; set; } = string.Empty;
    public double TotalDuration { get; set; }
    public SystemInfoDto System { get; set; } = new();
    public List<HealthCheckEntryDto> Entries { get; set; } = new();
}

public class SystemInfoDto
{
    public string Environment { get; set; } = string.Empty;
    public long MemoryUsageBytes { get; set; }
    public double UptimeSeconds { get; set; }
}

public class HealthCheckEntryDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Duration { get; set; }
    public IEnumerable<string> Tags { get; set; } = new List<string>();
}
