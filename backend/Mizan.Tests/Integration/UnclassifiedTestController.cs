using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mizan.Tests.Integration;

/// <summary>
/// Stands in for a controller someone adds without classifying it in ImpersonationPolicy. It lives
/// in the test assembly, which the fixture registers as an application part, so the API itself
/// stays fully classified. It does nothing but answer.
/// </summary>
[ApiController]
[Route("api/UnclassifiedTest")]
[Authorize]
public sealed class UnclassifiedTestController : ControllerBase
{
    [HttpPost]
    public IActionResult Post() => Ok(new { ok = true });
}
