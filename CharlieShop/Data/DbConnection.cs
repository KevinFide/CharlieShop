using Npgsql;
using System.Data;

namespace CharlieShop.Data
{
    public class DbConnection
    {
        private readonly IConfiguration _configuration;

        public DbConnection(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection")
            );
        }
    }
}