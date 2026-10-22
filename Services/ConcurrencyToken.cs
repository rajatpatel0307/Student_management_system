using Microsoft.EntityFrameworkCore;

namespace Student_Management_System.Services;

public static class ConcurrencyToken
{
    public static void Apply<TEntity>(
        DbContext context,
        TEntity trackedEntity,
        byte[]? originalRowVersion)
        where TEntity : class
    {
        if (originalRowVersion is { Length: > 0 })
            context.Entry(trackedEntity)
                .Property<byte[]>("RowVersion")
                .OriginalValue = originalRowVersion;
    }
}
