using System.Security.Cryptography;
using System.Text.Json;
using CampaignForge.Api.Data;
using CampaignForge.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampaignForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CharactersController(CampaignDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Character>>> GetAll(CancellationToken ct) =>
        Ok(await db.Characters.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Character>> Get(Guid id, CancellationToken ct)
    {
        var character = await db.Characters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return character is null ? NotFound() : Ok(character);
    }

    [HttpPost]
    public async Task<ActionResult<Character>> Create(CharacterRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new { error });
        var now = DateTime.UtcNow;
        var item = new Character
        {
            Name = request.Name.Trim(), PlayerName = Clean(request.PlayerName), Race = Clean(request.Race),
            ClassName = Clean(request.ClassName), Alignment = Clean(request.Alignment), Deity = Clean(request.Deity),
            CampaignId = request.CampaignId, CharacterLevel = request.CharacterLevel,
            CreationStatus = request.CreationStatus, AbilityScoresJson = JsonSerializer.Serialize(request.AbilityScores, JsonOptions),
            CharacterDataJson = JsonSerializer.Serialize(request.Data ?? new Dictionary<string, object?>(), JsonOptions),
            CreatedUtc = now, UpdatedUtc = now
        };
        db.Characters.Add(item);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Character>> Update(Guid id, CharacterRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new { error });
        var item = await db.Characters.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        item.Name = request.Name.Trim(); item.PlayerName = Clean(request.PlayerName); item.Race = Clean(request.Race);
        item.ClassName = Clean(request.ClassName); item.Alignment = Clean(request.Alignment); item.Deity = Clean(request.Deity);
        item.CampaignId = request.CampaignId; item.CharacterLevel = request.CharacterLevel;
        item.CreationStatus = request.CreationStatus; item.AbilityScoresJson = JsonSerializer.Serialize(request.AbilityScores, JsonOptions);
        item.CharacterDataJson = JsonSerializer.Serialize(request.Data ?? new Dictionary<string, object?>(), JsonOptions);
        item.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var item = await db.Characters.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        db.Characters.Remove(item); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPost("starting-gold/{className}")]
    public ActionResult<StartingGoldResponse> RollStartingGold(string className)
    {
        var rule = className.ToLowerInvariant() switch
        {
            "barbarian" => (Count: 4, Multiplier: 10), "bard" => (Count: 4, Multiplier: 10), "cleric" => (Count: 5, Multiplier: 10),
            "druid" => (Count: 2, Multiplier: 10), "fighter" => (Count: 6, Multiplier: 10), "monk" => (Count: 5, Multiplier: 1), "paladin" => (Count: 6, Multiplier: 10),
            "ranger" => (Count: 6, Multiplier: 10), "rogue" => (Count: 4, Multiplier: 10), "sorcerer" => (Count: 3, Multiplier: 10), "wizard" => (Count: 3, Multiplier: 10),
            _ => (Count: 0, Multiplier: 0)
        };
        if (rule.Count == 0) return BadRequest(new { error = "Choose a core D&D 3.5 class first." });
        var dice = Enumerable.Range(0, rule.Count).Select(_ => RandomNumberGenerator.GetInt32(1, 5)).ToArray();
        return Ok(new StartingGoldResponse(className, dice, rule.Multiplier, dice.Sum() * rule.Multiplier));
    }

    // Cryptographically strong server-side 4d6-drop-lowest rolls; browser animation never determines the result.
    [HttpPost("roll-abilities")]
    public ActionResult<AbilityRollResponse> RollAbilities()
    {
        var rolls = Enumerable.Range(0, 6).Select(index =>
        {
            var dice = Enumerable.Range(0, 4).Select(_ => RandomNumberGenerator.GetInt32(1, 7)).ToArray();
            var kept = dice.OrderByDescending(x => x).Take(3).ToArray();
            return new AbilityRoll(index, dice, dice.Min(), kept.Sum());
        }).ToArray();
        return Ok(new AbilityRollResponse("4d6 drop lowest", rolls));
    }

    private static string? Validate(CharacterRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Character name is required.";
        if (r.CharacterLevel is < 1 or > 20) return "Character level must be between 1 and 20.";
        if (r.AbilityScores is not null)
        {
            var keys = new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" };
            foreach (var key in keys)
                if (!r.AbilityScores.TryGetValue(key, out var score) || score is < 3 or > 30)
                    return $"Ability score '{key}' must be between 3 and 30.";
        }
        return null;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CharacterRequest(
    string Name, string? PlayerName, Guid? CampaignId, string? Race, string? ClassName,
    string? Alignment, string? Deity, int CharacterLevel = 1, string CreationStatus = "Draft",
    Dictionary<string, int>? AbilityScores = null, Dictionary<string, object?>? Data = null);
public sealed record AbilityRoll(int Sequence, int[] Dice, int Dropped, int Total);
public sealed record AbilityRollResponse(string Method, AbilityRoll[] Rolls);
public sealed record StartingGoldResponse(string ClassName, int[] Dice, int Multiplier, int TotalGold);
