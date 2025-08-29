using System;

public class StockData
{
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }

    public override string ToString()
    {
        return $"{Date:yyyy-MM-dd HH:mm:ss}: O:{Open}, H:{High}, L:{Low}, C:{Close}, V:{Volume}";
    }
}