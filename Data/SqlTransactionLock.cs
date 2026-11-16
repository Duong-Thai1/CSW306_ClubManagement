using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClubManagement.Data;

public sealed class SqlTransactionLock(ClubDbContext db)
{
    public async Task<IDbContextTransaction> AcquireAsync(string resource, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await LockWithinTransactionAsync(resource, ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public Task<int> LockWithinTransactionAsync(string resource, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @LockResult int;
        EXEC @LockResult = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 10000;
        IF @LockResult < 0 THROW 51001, 'Could not acquire resource lock.', 1;
        """, ct);
}
