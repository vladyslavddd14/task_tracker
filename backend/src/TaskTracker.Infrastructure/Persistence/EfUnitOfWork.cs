using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;

namespace TaskTracker.Infrastructure.Persistence;

/// <summary>
/// SaveChanges runs all pending changes in one database transaction, so a status change
/// and its transition record are always saved together or not at all.
/// </summary>
public class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The data was changed by another request. Reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           {
                                               SqlState: PostgresErrorCodes.UniqueViolation
                                           })
        {
            throw new ConflictException("A record with the same unique value already exists.");
        }
    }
}
