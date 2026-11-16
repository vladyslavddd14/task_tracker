using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskTracker.Domain;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Infrastructure.Persistence;

// Primary keys are generated in the domain (Guid.NewGuid), so EF must never treat a new
// entity with a key as an existing one. That is what ValueGeneratedNever() guarantees.

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email).HasMaxLength(DomainLimits.EmailMax).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(DomainLimits.DisplayNameMax).IsRequired();
        builder.Property(u => u.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Title).HasMaxLength(DomainLimits.TitleMax).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(DomainLimits.DescriptionMax);
        builder.Property(t => t.NotesMarkdown).HasMaxLength(DomainLimits.NotesMax);
        builder.Ignore(t => t.SubtasksTotal);
        builder.Ignore(t => t.SubtasksDone);

        // Optimistic concurrency: PostgreSQL's xmin changes on every update of the row.
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        builder.HasIndex(t => new { t.UserId, t.Status });
        builder.HasIndex(t => new { t.UserId, t.Deadline });

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Subtasks).WithOne().HasForeignKey(s => s.TaskItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.TaskTags).WithOne().HasForeignKey(l => l.TaskItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Transitions).WithOne().HasForeignKey(x => x.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.CustomFieldValues).WithOne().HasForeignKey(v => v.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Subtasks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(t => t.TaskTags).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(t => t.Transitions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(t => t.CustomFieldValues).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class SubtaskConfiguration : IEntityTypeConfiguration<Subtask>
{
    public void Configure(EntityTypeBuilder<Subtask> builder)
    {
        builder.ToTable("Subtasks");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Title).HasMaxLength(DomainLimits.TitleMax).IsRequired();
        builder.HasIndex(s => s.TaskItemId);
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).HasMaxLength(DomainLimits.TagNameMax).IsRequired();
        builder.Property(t => t.NormalizedName).HasMaxLength(DomainLimits.TagNameMax).IsRequired();
        builder.Property(t => t.Color).HasMaxLength(7);

        // Unique per user, ignoring case: the normalized name is always lower-cased.
        builder.HasIndex(t => new { t.UserId, t.NormalizedName }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TaskTagConfiguration : IEntityTypeConfiguration<TaskTag>
{
    public void Configure(EntityTypeBuilder<TaskTag> builder)
    {
        builder.ToTable("TaskTags");
        builder.HasKey(l => new { l.TaskItemId, l.TagId });
        builder.HasOne(l => l.Tag).WithMany().HasForeignKey(l => l.TagId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => l.TagId);
    }
}

public class StatusTransitionConfiguration : IEntityTypeConfiguration<StatusTransition>
{
    public void Configure(EntityTypeBuilder<StatusTransition> builder)
    {
        builder.ToTable("StatusTransitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.TaskItemId);
    }
}

public class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("CustomFieldDefinitions");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Name).HasMaxLength(DomainLimits.CustomFieldNameMax).IsRequired();
        builder.Property(f => f.NormalizedName).HasMaxLength(DomainLimits.CustomFieldNameMax).IsRequired();
        builder.HasIndex(f => new { f.UserId, f.NormalizedName }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CustomFieldValueConfiguration : IEntityTypeConfiguration<CustomFieldValue>
{
    public void Configure(EntityTypeBuilder<CustomFieldValue> builder)
    {
        builder.ToTable("CustomFieldValues");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Value).HasMaxLength(DomainLimits.CustomFieldValueMax).IsRequired();
        builder.HasIndex(v => new { v.TaskItemId, v.CustomFieldDefinitionId }).IsUnique();
        builder.HasOne<CustomFieldDefinition>().WithMany().HasForeignKey(v => v.CustomFieldDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
