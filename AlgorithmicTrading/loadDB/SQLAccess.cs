using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

public class SQLAccess
{
    private readonly string _connectionString;

    public SQLAccess(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<StockData>> GetStockDataAsync(string ticker, string name, DateTime startDate, DateTime endDate)
    {
        // 1. DB에서 종목 ID 가져오기
        long stockId = await GetStockIdAsync(ticker);

        // 종목이 DB에 없는 경우, 먼저 Python 스크립트로 추가
        if (stockId == -1)
        {
            Console.WriteLine($"Ticker {ticker} not found in the list. Fetching from source via Python script...");
            await RunPythonScriptAndPopulateDbAsync(ticker, name, startDate, endDate);
            stockId = await GetStockIdAsync(ticker); // ID 다시 조회
            if (stockId == -1)
            {
                Console.WriteLine($"Failed to create ticker {ticker} in the database.");
                return new List<StockData>(); // 실패 시 빈 리스트 반환
            }
        }

        // 2. DB에서 데이터 조회
        var data = await FetchDataFromDbAsync(stockId, startDate, endDate);

        // 3. 데이터 검증 (데이터가 없는 경우)
        if (data.Count == 0)
        {
            Console.WriteLine("No data found in DB for the given range. Fetching from source via Python script...");
            // 4. Python 스크립트 호출하여 데이터 채우기
            await RunPythonScriptAndPopulateDbAsync(ticker, name, startDate, endDate);

            // 5. 데이터 다시 조회
            data = await FetchDataFromDbAsync(stockId, startDate, endDate);
        }

        return data;
    }

    private async Task<long> GetStockIdAsync(string ticker)
    {
        using (var conn = new MySqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var cmd = new MySqlCommand("SELECT id FROM list WHERE ticker = @ticker", conn);
            cmd.Parameters.AddWithValue("@ticker", ticker);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt64(result) : -1;
        }
    }

    private async Task<List<StockData>> FetchDataFromDbAsync(long stockId, DateTime startDate, DateTime endDate)
    {
        if (stockId == -1) return new List<StockData>();

        var data = new List<StockData>();
        bool isIntraday = startDate.Date == endDate.Date;
        string tableName = isIntraday ? "data_intraday" : "data_daily";

        using (var conn = new MySqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var cmd = new MySqlCommand(
                $"SELECT date, open, high, low, close, volume FROM {tableName} WHERE id = @id AND date >= @startDate AND date <= @endDate ORDER BY date",
                conn);
            cmd.Parameters.AddWithValue("@id", stockId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    data.Add(new StockData
                    {
                        Date = reader.GetDateTime("date"),
                        Open = reader.GetDecimal("open"),
                        High = reader.GetDecimal("high"),
                        Low = reader.GetDecimal("low"),
                        Close = reader.GetDecimal("close"),
                        Volume = reader.GetInt64("volume")
                    });
                }
            }
        }
        return data;
    }

    private async Task RunPythonScriptAndPopulateDbAsync(string ticker, string name, DateTime startDate, DateTime endDate)
    {
        // 1. Python 실행 파일 경로 수정 (Windows 환경)

        // 1. 위에서 찾은 실제 python.exe 경로로 수정하세요.
        string pythonPath = @"C:\Users\minse\AppData\Local\Programs\Python\Python310\python.exe"; 

        // 2. 알려주신 스크립트의 절대 경로로 수정
        string scriptPath = @"C:\Users\minse\source\repos\AlgorithmicTrading\AlgorithmicTrading\loadDB\SQLAccess.py";

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.Start();

            using (StreamWriter sw = process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    await sw.WriteLineAsync(ticker);
                    await sw.WriteLineAsync(name);
                    await sw.WriteLineAsync(startDate.ToString("yyyy-MM-dd"));
                    await sw.WriteLineAsync(endDate.ToString("yyyy-MM-dd"));
                }
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Console.WriteLine("--- Python Script Output ---");
            Console.WriteLine(output);
            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine("--- Python Script Error ---");
                Console.WriteLine(error);
            }
            Console.WriteLine("--------------------------");
        }
    }
}