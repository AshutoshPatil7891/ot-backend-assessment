
using Dapper;
using Microsoft.Data.SqlClient;
using OT.Assessment.Common.Resp;
using System.Data;

namespace OT.Assessment.App.Services
{
    public class SqlPlayerReader : IPlayerReader
    {
        private readonly string _connectionString;

        public SqlPlayerReader(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DatabaseConnection");
        }

        public async Task<PagedResp<PlayerBetResp>> GetPlayerBetsAsync(
            Guid playerId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);

            var rows = (await connection.QueryAsync<PlayerBetRow>(
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

            return (await connection.QueryAsync<TopSpenderResp>(
                new CommandDefinition(
                    "usp_GetTopSpenders",
                    new { Count = count },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken
                )
            )).ToList();
        }

        private class PlayerBetRow : PlayerBetResp
        {
            public int Total { get; set; }
        }
    }
}

