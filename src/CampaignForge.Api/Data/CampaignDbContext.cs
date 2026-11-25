using CampaignForge.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CampaignForge.Api.Data;

public sealed class CampaignDbContext(DbContextOptions<CampaignDbContext> options)
    : DbContext(options)
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Character> Characters => Set<Character>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var campaign = modelBuilder.Entity<Campaign>();
        campaign.ToTable("Campaigns");
        campaign.HasKey(x => x.Id);
        campaign.Property(x => x.Name).HasMaxLength(120).IsRequired();
        campaign.Property(x => x.Setting).HasMaxLength(160);
        campaign.Property(x => x.Description).HasMaxLength(4000);
        campaign.Property(x => x.Status).HasMaxLength(30).IsRequired();
        campaign.Property(x => x.CreatedUtc).HasColumnType("datetime2");
        campaign.Property(x => x.UpdatedUtc).HasColumnType("datetime2");
        campaign.HasIndex(x => x.Name);
        campaign.HasIndex(x => x.Status);

        var character = modelBuilder.Entity<Character>();
        character.ToTable("Characters");
        character.HasKey(x => x.Id);
        character.Property(x => x.Name).HasMaxLength(100).IsRequired();
        character.Property(x => x.PlayerName).HasMaxLength(100);
        character.Property(x => x.Race).HasMaxLength(60);
        character.Property(x => x.ClassName).HasMaxLength(60);
        character.Property(x => x.Alignment).HasMaxLength(30);
        character.Property(x => x.Deity).HasMaxLength(80);
        character.Property(x => x.CreationStatus).HasMaxLength(30).IsRequired();
        character.Property(x => x.AbilityScoresJson).HasColumnType("nvarchar(max)").IsRequired();
        character.Property(x => x.CharacterDataJson).HasColumnType("nvarchar(max)").IsRequired();
        character.Property(x => x.CreatedUtc).HasColumnType("datetime2");
        character.Property(x => x.UpdatedUtc).HasColumnType("datetime2");
        character.HasIndex(x => x.Name);
        character.HasIndex(x => x.CampaignId);
    }
}
