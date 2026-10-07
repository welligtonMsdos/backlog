using Backlog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backlog.Infrastructure;

public sealed class BacklogDbContext(DbContextOptions<BacklogDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<AuthSession> Sessions => Set<AuthSession>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<StageEntry> Stages => Set<StageEntry>();

    public DbSet<StageNote> Notes => Set<StageNote>();

    public DbSet<StageAttachment> Attachments => Set<StageAttachment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(b =>
        {
            b.ToTable("users", t => t.HasCheckConstraint("ck_user_role", "role IN ('tesouraria', 'desenvolvedor', 'gestor')"));

            b.HasKey(x => x.Id);

            b.Property(x => x.Name).HasMaxLength(200);

            b.Property(x => x.Email).HasMaxLength(254);

            b.Property(x => x.Role).HasMaxLength(30);

            b.Property(x => x.PasswordHash).HasMaxLength(512);

            b.HasIndex(x => x.Email).IsUnique();
        });

        model.Entity<AuthSession>(b =>
        {
            b.ToTable("auth_sessions");

            b.HasKey(x => x.Id);

            b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.UserId, x.ExpiresAt });
        });

        model.Entity<TaskItem>(b =>
        {
            b.ToTable("tasks", t => t.HasCheckConstraint("ck_task_status", "current_status IN ('backlog', 'desenvolvimento', 'homologação', 'em implantação', 'finalizado')"));

            b.HasKey(x => x.Id);

            b.Ignore(x => x.Stages);

            b.Ignore(x => x.CurrentStage);

            b.Property(x => x.Title).HasMaxLength(200);

            b.Property(x => x.Description).HasMaxLength(10000);

            b.Property(x => x.Status).HasColumnName("current_status").HasMaxLength(30);

            b.Property(x => x.Version).IsConcurrencyToken();

            b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>().WithMany().HasForeignKey(x => x.TargetDeveloperId).OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>().WithMany().HasForeignKey(x => x.CurrentDeveloperId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.CreatorId, x.Status });

            b.HasIndex(x => new { x.TargetDeveloperId, x.Status });

            b.HasIndex(x => new { x.CurrentDeveloperId, x.Status });
        });

        model.Entity<StageEntry>(b =>
        {
            b.ToTable("task_stage_entries", t => t.HasCheckConstraint("ck_stage_dates", "ended_at IS NULL OR ended_at >= started_at"));

            b.HasKey(x => x.Id);

            b.Property(x => x.Status).HasMaxLength(30);

            b.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>().WithMany().HasForeignKey(x => x.StartedByUserId).OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>().WithMany().HasForeignKey(x => x.EndedByUserId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.TaskId, x.Sequence }).IsUnique();

            b.HasIndex(x => x.TaskId).IsUnique().HasFilter("ended_at IS NULL").HasDatabaseName("ux_task_open_stage");
        });

        model.Entity<StageNote>(b =>
        {
            b.ToTable("stage_notes");

            b.HasKey(x => x.Id);

            b.Property(x => x.Text).HasMaxLength(4000);

            b.HasOne<StageEntry>().WithMany(x => x.Notes).HasForeignKey(x => x.StageEntryId).OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<StageAttachment>(b =>
        {
            b.ToTable("stage_attachments");

            b.HasKey(x => x.Id);

            b.Property(x => x.OriginalName).HasMaxLength(200);

            b.Property(x => x.ContentType).HasMaxLength(100);

            b.Property(x => x.Content).HasColumnType("bytea");

            b.HasOne<StageEntry>().WithMany(x => x.Attachments).HasForeignKey(x => x.StageEntryId).OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.Name != nameof(TaskItem.Status) || entity.ClrType != typeof(TaskItem))
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        return string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
