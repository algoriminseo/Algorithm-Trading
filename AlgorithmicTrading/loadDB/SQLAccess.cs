using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        // 1. DB���� ���� ID ��������
        long stockId = await GetStockIdAsync(ticker);

        // ������ DB�� ���� ���, ���� Python ��ũ��Ʈ�� �߰�
        if (stockId == -1)
        {
            Console.WriteLine($"Ticker {ticker} not found in the list. Fetching from source via Python script...");
            await RunPythonScriptAndPopulateDbAsync(ticker, name, startDate, endDate);
            stockId = await GetStockIdAsync(ticker); // ID �ٽ� ��ȸ
            if (stockId == -1)
            {
                Console.WriteLine($"Failed to create ticker {ticker} in the database.");
                return new List<StockData>(); // ���� �� �� ����Ʈ ��ȯ
            }
        }

        // 2. DB���� ������ ��ȸ
        var data = await FetchDataFromDbAsync(stockId, startDate, endDate);

        // 3. ������ ���� (�����Ͱ� ���� ���)
        if (data.Count == 0)
        {
            Console.WriteLine("No data found in DB for the given range. Fetching from source via Python script...");
            // 4. Python ��ũ��Ʈ ȣ���Ͽ� ������ ä���
            await RunPythonScriptAndPopulateDbAsync(ticker, name, startDate, endDate);

            // 5. ������ �ٽ� ��ȸ
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
                        Date = reader.GetDateTime(0),
                        Open = reader.GetDecimal(1),
                        High = reader.GetDecimal(2),
                        Low = reader.GetDecimal(3),
                        Close = reader.GetDecimal(4),
                        Volume = reader.GetInt64(5)
                    });
                }
            }
        }
        return data;
    }

    private async Task RunPythonScriptAndPopulateDbAsync(string ticker, string name, DateTime startDate, DateTime endDate)
    {
        string pythonPath;
        string scriptPath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows 환경 경로
            pythonPath = @"C:\Users\minse\AppData\Local\Programs\Python\Python310\python.exe";
            scriptPath = @"C:\Users\minse\AlgorithmTrading\Algorithm-Trading\AlgorithmicTrading\loadDB\SQLAccess.py";
        }
        else
        {
            // Linux (WSL) 환경 경로
            pythonPath = "/mnt/c/Users/minse/AlgorithmTrading/Algorithm-Trading/AlgorithmicTrading/venv/bin/python"; 
            scriptPath = "/mnt/c/Users/minse/AlgorithmTrading/Algorithm-Trading/AlgorithmicTrading/loadDB/SQLAccess.py";
        }

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
