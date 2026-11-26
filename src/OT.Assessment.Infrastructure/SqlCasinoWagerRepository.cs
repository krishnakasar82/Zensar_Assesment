using System.Data;
using Microsoft.Data.SqlClient;
using OT.Assessment.Application;
using OT.Assessment.Domain;
using OT.Assessment.Shared;

namespace OT.Assessment.Infrastructure;

public sealed class SqlCasinoWagerRepository(string connectionString) : ICasinoWagerRepository
{
    public async Task SaveAsync(CasinoWagerMessage wager, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction =
            (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var duplicateCheck = new SqlCommand(
            "SELECT 1 FROM dbo.CasinoWagers WITH (UPDLOCK, HOLDLOCK) WHERE WagerId = @WagerId;",
            connection,
            transaction))
        {
            duplicateCheck.Parameters.Add("@WagerId", SqlDbType.UniqueIdentifier).Value = wager.WagerId;
            if (await duplicateCheck.ExecuteScalarAsync(cancellationToken) is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }
        }

        await using (var upsertPlayer = new SqlCommand(
            """
            MERGE dbo.Players WITH (HOLDLOCK) AS target
            USING (SELECT @AccountId AS AccountId,
                          @Username AS Username,
                          @UpdatedAt AS UpdatedAt) AS source
               ON target.AccountId = source.AccountId
            WHEN MATCHED AND target.UpdatedAt <= source.UpdatedAt THEN
                UPDATE SET Username = source.Username, UpdatedAt = source.UpdatedAt
            WHEN NOT MATCHED THEN
                INSERT (AccountId, Username, UpdatedAt)
                VALUES (source.AccountId, source.Username, source.UpdatedAt);
            """,
            connection,
            transaction))
        {
            AddPlayerParameters(upsertPlayer, wager);
            await upsertPlayer.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertWager = new SqlCommand(
            """
            INSERT INTO dbo.CasinoWagers
                (WagerId, AccountId, GameName, Provider, Amount, CreatedDateTime)
            VALUES
                (@WagerId, @AccountId, @GameName, @Provider, @Amount, @CreatedDateTime);
            """,
            connection,
            transaction))
        {
            insertWager.Parameters.Add("@WagerId", SqlDbType.UniqueIdentifier).Value = wager.WagerId;
            insertWager.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = wager.AccountId;
            insertWager.Parameters.Add("@GameName", SqlDbType.NVarChar, 200).Value = wager.GameName;
            insertWager.Parameters.Add("@Provider", SqlDbType.NVarChar, 200).Value = wager.Provider;
            var amount = insertWager.Parameters.Add("@Amount", SqlDbType.Decimal);
            amount.Precision = 28;
            amount.Scale = 17;
            amount.Value = wager.Amount;
            insertWager.Parameters.Add("@CreatedDateTime", SqlDbType.DateTimeOffset).Value =
                wager.CreatedDateTime;
            try
            {
                await insertWager.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException exception) when (
                exception.Number is 2601 or 2627)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CasinoWagerPage> GetHistoryAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        long total;
        await using (var countCommand = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.CasinoWagers WHERE AccountId = @AccountId;",
            connection))
        {
            countCommand.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
            total = (long)(await countCommand.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("The wager count query returned no result."));
        }

        var wagers = new List<CasinoWager>(pageSize);
        await using (var historyCommand = new SqlCommand(
            """
            SELECT WagerId, AccountId, GameName, Provider, Amount, CreatedDateTime
            FROM dbo.CasinoWagers
            WHERE AccountId = @AccountId
            ORDER BY CreatedDateTime DESC, WagerId DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            connection))
        {
            historyCommand.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
            historyCommand.Parameters.Add("@Offset", SqlDbType.BigInt).Value = (long)(page - 1) * pageSize;
            historyCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            await using var reader = await historyCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                wagers.Add(new CasinoWager(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetDecimal(4),
                    reader.GetFieldValue<DateTimeOffset>(5)));
            }
        }

        var totalPages = total == 0 ? 0 : (total + pageSize - 1) / pageSize;
        return new CasinoWagerPage(wagers, page, pageSize, total, totalPages);
    }

    public async Task<IReadOnlyList<TopSpender>> GetTopSpendersAsync(
        int count,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT TOP (@Count)
                p.AccountId,
                p.Username,
                CAST(SUM(w.Amount) AS decimal(38, 17)) AS TotalAmountSpend
            FROM dbo.CasinoWagers AS w
            INNER JOIN dbo.Players AS p ON p.AccountId = w.AccountId
            GROUP BY p.AccountId, p.Username
            ORDER BY TotalAmountSpend DESC, p.AccountId ASC;
            """,
            connection);
        command.Parameters.Add("@Count", SqlDbType.Int).Value = count;

        var spenders = new List<TopSpender>(count);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            spenders.Add(new TopSpender(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDecimal(2)));
        }

        return spenders;
    }

    private static void AddPlayerParameters(SqlCommand command, CasinoWagerMessage wager)
    {
        command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = wager.AccountId;
        command.Parameters.Add("@Username", SqlDbType.NVarChar, 100).Value = wager.Username;
        command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = wager.CreatedDateTime;
    }
}
