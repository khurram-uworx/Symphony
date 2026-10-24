using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Symphony.Data;

namespace Symphony.Data;

public sealed class SymphonyDbContext(DbContextOptions<SymphonyDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<UserConfiguration> UserConfigurations { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserConfiguration>(entity =>
        {
            entity.HasKey(configuration => configuration.Id);

            entity.Property(configuration => configuration.Id)
                .HasMaxLength(128)
                .ValueGeneratedOnAdd();

            entity.HasIndex(configuration => configuration.UserId)
                .IsUnique();

            entity.Property(configuration => configuration.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.HasOne(configuration => configuration.User)
                .WithOne()
                .HasForeignKey<UserConfiguration>(configuration => configuration.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(configuration => configuration.TrackerKind).HasMaxLength(64);
            entity.Property(configuration => configuration.TrackerEndpoint).HasMaxLength(2048);
            entity.Property(configuration => configuration.TrackerApiKey).IsRequired();
            entity.Property(configuration => configuration.TrackerProjectSlug).HasMaxLength(256);
            entity.Property(configuration => configuration.WorkspaceRoot).HasMaxLength(2048);
            entity.Property(configuration => configuration.CopilotProvider).HasMaxLength(64);
            entity.Property(configuration => configuration.CopilotBaseUrl).HasMaxLength(2048);
            entity.Property(configuration => configuration.CopilotModelId).HasMaxLength(256);
            entity.Property(configuration => configuration.CopilotWireApi).HasMaxLength(64);
            entity.Property(configuration => configuration.DefaultWorkflowName).HasMaxLength(256);

            entity.Property(configuration => configuration.CreatedAt)
                .IsRequired();

            entity.Property(configuration => configuration.UpdatedAt)
                .IsRequired();
        });
    }
}