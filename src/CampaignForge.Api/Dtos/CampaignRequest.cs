using System.ComponentModel.DataAnnotations;

namespace CampaignForge.Api.Dtos;

public sealed class CampaignRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; init; } = "";

    [StringLength(160)]
    public string? Setting { get; init; }

    [StringLength(4000)]
    public string? Description { get; init; }

    [Required]
    [RegularExpression("^(Planning|Active|Paused|Completed|Archived)$",
        ErrorMessage = "Status must be Planning, Active, Paused, Completed, or Archived.")]
    public string Status { get; init; } = "Planning";
}
