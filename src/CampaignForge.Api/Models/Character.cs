using System.ComponentModel.DataAnnotations;

namespace CampaignForge.Api.Models;

/// <summary>Character aggregate. Detailed selections are versioned JSON so the creator can evolve without destructive schema churn.</summary>
public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CampaignId { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
    [MaxLength(100)] public string? PlayerName { get; set; }
    [MaxLength(60)] public string? Race { get; set; }
    [MaxLength(60)] public string? ClassName { get; set; }
    [MaxLength(30)] public string? Alignment { get; set; }
    [MaxLength(80)] public string? Deity { get; set; }
    public int CharacterLevel { get; set; } = 1;
    [MaxLength(30)] public string CreationStatus { get; set; } = "Draft";
    public string AbilityScoresJson { get; set; } = "{}";
    public string CharacterDataJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
