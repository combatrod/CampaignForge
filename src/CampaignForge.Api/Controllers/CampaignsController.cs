using CampaignForge.Api.Data;
using CampaignForge.Api.Dtos;
using CampaignForge.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampaignForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CampaignsController(CampaignDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Campaign>>> GetAll(
        [FromQuery] string? search, [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = db.Campaigns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) ||
                (x.Setting != null && x.Setting.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        return Ok(await query.OrderByDescending(x => x.UpdatedUtc)
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Campaign>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await db.Campaigns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return campaign is null ? NotFound() : Ok(campaign);
    }

    [HttpPost]
    public async Task<ActionResult<Campaign>> Create(
        CampaignRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var campaign = new Campaign
        {
            Name = request.Name.Trim(),
            Setting = Clean(request.Setting),
            Description = Clean(request.Description),
            Status = request.Status,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = campaign.Id }, campaign);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Campaign>> Update(
        Guid id, CampaignRequest request, CancellationToken cancellationToken)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (campaign is null) return NotFound();

        campaign.Name = request.Name.Trim();
        campaign.Setting = Clean(request.Setting);
        campaign.Description = Clean(request.Description);
        campaign.Status = request.Status;
        campaign.UpdatedUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(campaign);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (campaign is null) return NotFound();

        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
