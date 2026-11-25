using System.ComponentModel.DataAnnotations;

namespace CampaignForge.Api.Models;

public sealed class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public required string Name { get; set; }

    [MaxLength(160)]
    public string? Setting { get; set; }

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Planning";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
