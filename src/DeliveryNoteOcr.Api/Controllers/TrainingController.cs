using DeliveryNoteOcr.Api.Services;
using DeliveryNoteOcr.Api.Services.Training;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryNoteOcr.Api.Controllers;

[ApiController]
[Route("api/training")]
public class TrainingController : ControllerBase
{
    private readonly IRetrainCoordinator _coordinator;
    private readonly ICurrentUser _user;

    public TrainingController(IRetrainCoordinator coordinator, ICurrentUser user)
    {
        _coordinator = coordinator;
        _user = user;
    }

    [HttpGet("status")]
    public Task<RetrainStatus> Status(CancellationToken ct) => _coordinator.GetStatusAsync(ct);

    public record TriggerDto(string? Notes);

    [HttpPost("trigger")]
    public async Task<IActionResult> Trigger([FromBody] TriggerDto? dto, CancellationToken ct)
    {
        var run = await _coordinator.TriggerAsync(_user.UserId, dto?.Notes, ct);
        return Ok(run);
    }
}
