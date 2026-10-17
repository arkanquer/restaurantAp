using System.Data;
using Npgsql;
using RestApp.Infrastructure;

namespace RestApp.Repositories;

public abstract class RepositoryBase
{
    protected DbSession Session { get; }

    protected RepositoryBase(DbSession session)
    {
        Session = session;
    }

    protected NpgsqlCommand CreateCommand(string commandText, bool isStoredProcedure = false)
    {
        var cmd = new NpgsqlCommand(commandText, Session.Connection, Session.Transaction)
        {
            CommandType = isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text
        };
        return cmd;
    }
}