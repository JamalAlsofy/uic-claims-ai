using Oracle.ManagedDataAccess.Client;

namespace Uic.Claims.Ai.Infrastructure.Oracle;

public interface IOracleConnectionFactory
{
    OracleConnection Create();
}

public sealed class OracleConnectionFactory(Microsoft.Extensions.Options.IOptions<OracleOptions> options)
    : IOracleConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public OracleConnection Create() => new(_connectionString);
}
