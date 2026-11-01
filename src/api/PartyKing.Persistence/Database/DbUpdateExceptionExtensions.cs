using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PartyKing.Persistence.Database;

public static class DbUpdateExceptionExtensions
{
    // SQLITE_CONSTRAINT_UNIQUE and SQLITE_CONSTRAINT_PRIMARYKEY
    private const int UniqueConstraint = 2067;
    private const int PrimaryKeyConstraint = 1555;

    /// <summary>Whether the save failed because a unique index or a primary key already holds the value.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception)
    {
        return exception.InnerException is SqliteException
        {
            SqliteExtendedErrorCode: UniqueConstraint or PrimaryKeyConstraint
        };
    }
}
