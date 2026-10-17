
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using OT.Assessment.Common.msg;
using System.Data;

namespace OT.Assessment.Consumer.Services
{
    public class SqlBetWriter : IBetWriter
    {
        private readonly string _connectionString;

        public SqlBetWriter(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DatabaseConnection");
        }

        public async Task WriteBatchAsync(
            IReadOnlyList<CasinoBetMesg> bets,
            CancellationToken cancellationToken = default)
        {
            var table = BuildTable(bets);

            using var connection = new SqlConnection(_connectionString);

            var command = new CommandDefinition(
                "usp_InsertCasinoBets",
                new
                {
                    Bets = table.AsTableValuedParameter("CasinoBetType")
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
        }

        private static DataTable BuildTable(IReadOnlyList<CasinoBetMesg> bets)
        {
            var table = new DataTable();

            table.Columns.Add("BetId", typeof(Guid));
            table.Columns.Add("AccountId", typeof(Guid));
            table.Columns.Add("Username", typeof(string));
            table.Columns.Add("CountryCode", typeof(string));
            table.Columns.Add("GameName", typeof(string));
            table.Columns.Add("Provider", typeof(string));
            table.Columns.Add("Theme", typeof(string));
            table.Columns.Add("Amount", typeof(decimal));
            table.Columns.Add("CreatedDateTime", typeof(DateTimeOffset));
            table.Columns.Add("NumberOfBets", typeof(int));
            table.Columns.Add("Duration", typeof(long));
            table.Columns.Add("TransactionId", typeof(Guid));

            foreach (var bet in bets)
            {
                table.Rows.Add(
                    bet.BetId,
                    bet.AccountId,
                    bet.Username,
                    bet.CountryCode ?? (object)DBNull.Value,
                    bet.GameName,
                    bet.Provider,
                    bet.Theme ?? (object)DBNull.Value,
                    bet.Amount,
                    bet.CreatedDateTime,
                    bet.NumberOfBets,
                    bet.Duration,
                    bet.TransactionId);
            }

            return table;
        }
    }
}

