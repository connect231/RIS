using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniCP.DbData;

namespace UniCP
{
    public static class Diagnostic
    {
        public static async Task RunQueries(MskDbContext db)
        {
            Console.WriteLine("--- TBL_VARUNA_SOZLESME ---");
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TBL_VARUNA_SOZLESME'";
                await db.Database.OpenConnectionAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        Console.WriteLine($"{reader.GetString(0)}: {reader.GetString(1)}");
                    }
                }
            }

            Console.WriteLine("\n--- VIEW_ORTAK_PROJE_ISIMLERI ---");
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VIEW_ORTAK_PROJE_ISIMLERI'";
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        Console.WriteLine($"{reader.GetString(0)}: {reader.GetString(1)}");
                    }
                }
            }
        }
    }
}
