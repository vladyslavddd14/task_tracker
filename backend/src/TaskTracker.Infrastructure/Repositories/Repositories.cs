using Microsoft.EntityFrameworkCore;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;
using TaskTracker.Application.Filtering;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;
using TaskTracker.Infrastructure.Persistence;

namespace TaskTracker.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

    public void Add(User user) => db.Users.Add(user);
}

/// <summary>Every query is filtered by UserId, so a user can never reach another user's tasks.</summary>
public class TaskRepository(AppDbContext db) : ITaskRepository
{
    public Task<TaskItem?> GetAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        db.Tasks
            .Include(t => t.Subtasks)
            .Include(t => t.TaskTags).ThenInclude(l => l.Tag)
            .Include(t => t.Transitions)
            .Include(t => t.CustomFieldValues)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

    public async Task<PagedResult<TaskItem>> SearchAsync(Guid userId, TaskQuery query, DateOnly today, bool paged,
        CancellationToken ct = default)
    {
        var filtered = db.Tasks.AsNoTracking().Where(t => t.UserId == userId).Apply(query, today);
        var total = await filtered.CountAsync(ct);

        var ordered = filtered.ApplyOrder(query);
        if (paged)
        {
            ordered = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);
        }

        var items = await ordered
            .Include(t => t.Subtasks)
            .Include(t => t.TaskTags).ThenInclude(l => l.Tag)
            .AsSplitQuery()
            .ToListAsync(ct);

        return new PagedResult<TaskItem>(items, total, query.Page, paged ? query.PageSize : items.Count);
    }

    public async Task<double> GetMaxPositionAsync(Guid userId, TaskItemStatus status, CancellationToken ct = default) =>
        await db.Tasks.Where(t => t.UserId == userId && t.Status == status)
            .MaxAsync(t => (double?)t.Position, ct) ?? 0;

    public Task<List<TaskItem>> GetColumnAsync(Guid userId, TaskItemStatus status, CancellationToken ct = default) =>
        db.Tasks.Where(t => t.UserId == userId && t.Status == status).ToListAsync(ct);

    public void Add(TaskItem task) => db.Tasks.Add(task);

    public void Remove(TaskItem task) => db.Tasks.Remove(task);
}

public class TagRepository(AppDbContext db) : ITagRepository
{
    public Task<List<Tag>> GetAllAsync(Guid userId, CancellationToken ct = default) =>
        db.Tags.Where(t => t.UserId == userId).ToListAsync(ct);

    public Task<Tag?> GetAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

    public Task<List<Tag>> GetByIdsAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        db.Tags.Where(t => t.UserId == userId && ids.Contains(t.Id)).ToListAsync(ct);

    public Task<bool> NameExistsAsync(Guid userId, string normalizedName, Guid? exceptId,
        CancellationToken ct = default) =>
        db.Tags.AnyAsync(
            t => t.UserId == userId && t.NormalizedName == normalizedName && (exceptId == null || t.Id != exceptId), ct);

    public void Add(Tag tag) => db.Tags.Add(tag);

    public void Remove(Tag tag) => db.Tags.Remove(tag);
}

public class CustomFieldRepository(AppDbContext db) : ICustomFieldRepository
{
    public Task<List<CustomFieldDefinition>> GetAllAsync(Guid userId, CancellationToken ct = default) =>
        db.CustomFieldDefinitions.Where(f => f.UserId == userId).ToListAsync(ct);

    public Task<CustomFieldDefinition?> GetAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        db.CustomFieldDefinitions.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);

    public Task<bool> NameExistsAsync(Guid userId, string normalizedName, Guid? exceptId,
        CancellationToken ct = default) =>
        db.CustomFieldDefinitions.AnyAsync(
            f => f.UserId == userId && f.NormalizedName == normalizedName && (exceptId == null || f.Id != exceptId),
            ct);

    public void Add(CustomFieldDefinition field) => db.CustomFieldDefinitions.Add(field);

    public void Remove(CustomFieldDefinition field) => db.CustomFieldDefinitions.Remove(field);
}
