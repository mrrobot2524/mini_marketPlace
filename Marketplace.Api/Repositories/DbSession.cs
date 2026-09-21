using Npgsql;

namespace Marketplace.Api.Repositories;

public sealed record DbSession(NpgsqlConnection Connection, NpgsqlTransaction? Transaction);