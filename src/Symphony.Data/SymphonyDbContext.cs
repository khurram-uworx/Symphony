using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Symphony.Data;

public sealed class SymphonyDbContext(DbContextOptions<SymphonyDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<UserConfiguration> UserConfigurations { get; set; } = default!;

    public DbSet<Workflow> Workflows { get; set; } = default!;

    public DbSet<Job> Jobs { get; set; } = default!;

    public DbSet<JobIssue> JobIssues { get; set; } = default!;

    public DbSet<JobEvent> JobEvents { get; set; } = default!;

    public DbSet<AppSetting> AppSettings { get; set; } = default!;

    public DbSet<RepositoryWorkspace> RepositoryWorkspaces { get; set; } = default!;

    public DbSet<ApiKey> ApiKeys { get; set; } = default!;

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

        modelBuilder.Entity<Workflow>(entity =>
        {
            entity.HasKey(workflow => workflow.Id);

            entity.Property(workflow => workflow.Id)
                .HasMaxLength(128)
                .ValueGeneratedOnAdd();

            entity.HasIndex(workflow => new { workflow.OwnerUserId, workflow.Name })
                .IsUnique();

            entity.Property(workflow => workflow.OwnerUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.HasOne(workflow => workflow.Owner)
                .WithMany()
                .HasForeignKey(workflow => workflow.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(workflow => workflow.Name)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(workflow => workflow.Content)
                .IsRequired();

            entity.Property(workflow => workflow.IsDefault)
                .IsRequired();

            entity.Property(workflow => workflow.CreatedAt)
                .IsRequired();

            entity.Property(workflow => workflow.UpdatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(job => job.Id);

            entity.Property(job => job.Id)
                .ValueGeneratedOnAdd();

            entity.Property(job => job.OwnerUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(job => job.WorkflowName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(job => job.Repository)
                .IsRequired()
                .HasMaxLength(2048);

            entity.Property(job => job.State)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(job => job.EncryptedRequestSnapshot);

            entity.Property(job => job.ExternalId).HasMaxLength(256);
            entity.Property(job => job.ErrorCode).HasMaxLength(128);
            entity.Property(job => job.ErrorMessage).HasMaxLength(4000);
            entity.Property(job => job.SummaryJson);

            entity.Property(job => job.QueuedAt)
                .IsRequired();

            entity.Property(job => job.StartedAt);
            entity.Property(job => job.CompletedAt);

            entity.HasIndex(job => new { job.State, job.QueuedAt });
            entity.HasIndex(job => new { job.OwnerUserId, job.QueuedAt });

            entity.HasMany(job => job.Issues)
                .WithOne(issue => issue.Job)
                .HasForeignKey(issue => issue.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(job => job.Events)
                .WithOne(jobEvent => jobEvent.Job)
                .HasForeignKey(jobEvent => jobEvent.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobIssue>(entity =>
        {
            entity.HasKey(issue => issue.Id);

            entity.Property(issue => issue.Id)
                .ValueGeneratedOnAdd();

            entity.HasIndex(issue => issue.JobId);

            entity.Property(issue => issue.JobId)
                .IsRequired();

            entity.Property(issue => issue.IssueId).HasMaxLength(256);
            entity.Property(issue => issue.Identifier).HasMaxLength(256);
            entity.Property(issue => issue.Title).HasMaxLength(512);
            entity.Property(issue => issue.State).HasMaxLength(64);
            entity.Property(issue => issue.Reason).HasMaxLength(4000);
            entity.Property(issue => issue.WorkspacePath).HasMaxLength(2048);

            entity.Property(issue => issue.Success)
                .IsRequired();

            entity.Property(issue => issue.TurnCount)
                .IsRequired();

            entity.Property(issue => issue.StartedAt);
            entity.Property(issue => issue.CompletedAt);

            entity.HasOne(issue => issue.Job)
                .WithMany(job => job.Issues)
                .HasForeignKey(issue => issue.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobEvent>(entity =>
        {
            entity.HasKey(jobEvent => jobEvent.Id);

            entity.Property(jobEvent => jobEvent.Id)
                .ValueGeneratedOnAdd();

            entity.HasIndex(jobEvent => new { jobEvent.JobId, jobEvent.Timestamp });

            entity.Property(jobEvent => jobEvent.JobId)
                .IsRequired();

            entity.Property(jobEvent => jobEvent.Timestamp)
                .IsRequired();

            entity.Property(jobEvent => jobEvent.EventName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(jobEvent => jobEvent.Summary).HasMaxLength(4000);
            entity.Property(jobEvent => jobEvent.PayloadJson);

            entity.HasOne(jobEvent => jobEvent.Job)
                .WithMany(job => job.Events)
                .HasForeignKey(jobEvent => jobEvent.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(jobEvent => jobEvent.JobIssue)
                .WithMany()
                .HasForeignKey(jobEvent => jobEvent.JobIssueId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(key => key.Id);

            entity.Property(key => key.Id)
                .HasMaxLength(128)
                .ValueGeneratedOnAdd();

            entity.Property(key => key.OwnerUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.HasOne(key => key.Owner)
                .WithMany()
                .HasForeignKey(key => key.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(key => key.Name)
                .HasMaxLength(256);

            entity.Property(key => key.KeyPrefix)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(key => key.KeyHash)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(key => key.IsEnabled)
                .IsRequired();

            entity.Property(key => key.CreatedAt)
                .IsRequired();

            entity.HasIndex(key => key.OwnerUserId);
            entity.HasIndex(key => key.KeyPrefix);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(setting => setting.Key);

            entity.Property(setting => setting.Key)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(setting => setting.Value)
                .IsRequired();
        });

        modelBuilder.Entity<RepositoryWorkspace>(entity =>
        {
            entity.HasKey(workspace => workspace.Id);

            entity.Property(workspace => workspace.Id)
                .ValueGeneratedOnAdd();

            entity.Property(workspace => workspace.OwnerUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(workspace => workspace.Repository)
                .IsRequired()
                .HasMaxLength(2048);

            entity.Property(workspace => workspace.CreatedAt)
                .IsRequired();

            entity.HasIndex(workspace => new { workspace.OwnerUserId, workspace.Repository })
                .IsUnique();
        });
    }
}