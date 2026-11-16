using TaskTracker.Application.Common;
using TaskTracker.Application.Filtering;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken Create(User user);
}

/// <summary>Commits all pending changes in one database transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
    void Add(User user);
}

public interface ITaskRepository
{
    /// <summary>Loads a task with all its children. Returns null when it belongs to another user.</summary>
    Task<TaskItem?> GetAsync(Guid userId, Guid id, CancellationToken ct = default);

    /// <summary>Applies the filters and the sort order of the query. Paging is applied only when requested.</summary>
    Task<PagedResult<TaskItem>> SearchAsync(Guid userId, TaskQuery query, DateOnly today, bool paged,
        CancellationToken ct = default);

    /// <summary>Highest kanban position in a column, or 0 when the column is empty.</summary>
    Task<double> GetMaxPositionAsync(Guid userId, TaskItemStatus status, CancellationToken ct = default);

    /// <summary>All tasks of one kanban column (tracked, so positions can be renumbered).</summary>
    Task<List<TaskItem>> GetColumnAsync(Guid userId, TaskItemStatus status, CancellationToken ct = default);

    void Add(TaskItem task);
    void Remove(TaskItem task);
}

public interface ITagRepository
{
    Task<List<Tag>> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task<Tag?> GetAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<List<Tag>> GetByIdsAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid userId, string normalizedName, Guid? exceptId, CancellationToken ct = default);
    void Add(Tag tag);
    void Remove(Tag tag);
}

public interface ICustomFieldRepository
{
    Task<List<CustomFieldDefinition>> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task<CustomFieldDefinition?> GetAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid userId, string normalizedName, Guid? exceptId, CancellationToken ct = default);
    void Add(CustomFieldDefinition field);
    void Remove(CustomFieldDefinition field);
}
