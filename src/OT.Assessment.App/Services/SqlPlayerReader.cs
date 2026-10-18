
using Dapper;
using Microsoft.Data.SqlClient;
using OT.Assessment.Common.Resp;
using System.Data;
using System.Diagnostics;

namespace OT.Assessment.App.Services
{
    public class SqlPlayerReader : IPlayerReader
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlPlayerReader> _logger;

        public SqlPlayerReader(IConfiguration configuration, ILogger<SqlPlayerReader> logger)
        {
            _connectionString = configuration.GetConnectionString("DatabaseConnection");
            _logger = logger;
        }

        public async Task<PagedResp<PlayerBetResp>> GetPlayerBetsAsync(
            Guid playerId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);

            var timer = Stopwatch.StartNew();
            List<PlayerBetRow> rows;
            try
            {
                rows = (await connection.QueryAsync<PlayerBetRow>(
                    new CommandDefinition(
                        "usp_GetPlayerCasinoBets",
                        new
                        {
                            AccountId = playerId,
                            Page = page,
                            PageSize = pageSize
                        },
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: cancellationToken
                    )
                )).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "usp_GetPlayerCasinoBets failed for player {PlayerId}", playerId);
                throw;
            }

            LogQuery("usp_GetPlayerCasinoBets", timer.ElapsedMilliseconds, rows.Count);

            int total = rows.Count > 0 ? rows[0].Total : 0;

            var response = new PagedResp<PlayerBetResp>
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            };

            response.Data = rows.Select(r => new PlayerBetResp
            {
                BetId = r.BetId,
                Game = r.Game,
                Provider = r.Provider,
                Amount = r.Amount,
                CreatedDate = r.CreatedDate
            }).ToList();

            return response;
        }

        public async Task<List<TopSpenderResp>> GetTopSpendersAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);

            var timer = Stopwatch.StartNew();
            List<TopSpenderResp> result;
            try
            {
                result = (await connection.QueryAsync<TopSpenderResp>(
                    new CommandDefinition(
                        "usp_GetTopSpenders",
                        new { Count = count },
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: cancellationToken
                    )
                )).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "usp_GetTopSpenders failed for count {Count}", count);
                throw;
            }

            LogQuery("usp_GetTopSpenders", timer.ElapsedMilliseconds, result.Count);

            return result;
        }

        // a query slower than this is logged as a warning so slow reads stand out in the logs
        private const long SlowQueryMs = 1000;

        private void LogQuery(string procedure, long elapsedMs, int rows)
        {
            if (elapsedMs >= SlowQueryMs)
            {
                _logger.LogWarning("{Procedure} was slow: {ElapsedMs} ms, {Rows} rows", procedure, elapsedMs, rows);
            }
            else
            {
                _logger.LogInformation("{Procedure} took {ElapsedMs} ms, {Rows} rows", procedure, elapsedMs, rows);
            }
        }

        private class PlayerBetRow : PlayerBetResp
        {
            public int Total { get; set; }
        }
    }
}

