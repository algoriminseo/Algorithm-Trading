using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main(string[] args)
    {
        // 여기에 실제 MySQL 연결 문자열을 입력하세요.
        string connectionString = "server=localhost;user=root;password=Mm10194105828!21;database=db;";
        var sqlAccess = new SQLAccess(connectionString);

        try
        {
            // 각 정보를 개별적으로 입력받도록 수정
            Console.Write("Enter the stock ticker symbol: ");
            string ticker = Console.ReadLine();

            Console.Write("Enter the stock name: ");
            string name = Console.ReadLine();

            Console.Write("Enter the starting date (YYYY-MM-DD): ");
            DateTime startDate = DateTime.ParseExact(Console.ReadLine(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

            Console.Write("Enter the ending date (YYYY-MM-DD): ");
            DateTime endDate = DateTime.ParseExact(Console.ReadLine(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // 데이터 가져오기
            List<StockData> stockData = await sqlAccess.GetStockDataAsync(ticker, name, startDate, endDate);

            if (stockData.Count > 0)
            {
                Console.WriteLine($"\n--- Data for {ticker} ---");
                foreach (var dataPoint in stockData)
                {
                    Console.WriteLine(dataPoint);
                }
            }
            else
            {
                Console.WriteLine($"\nNo data found for {ticker} in the specified range.");
            }
        }
        catch (FormatException)
        {
            Console.WriteLine("Invalid date format. Please use YYYY-MM-DD.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}