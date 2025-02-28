using Finance.Net.Extensions;
using Finance.Net.Interfaces;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static async Task Main(string[] args)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddFinanceNet();
        IServiceProvider provider = serviceCollection.BuildServiceProvider();
        var yahooService = provider.GetService<IYahooFinanceService>();

        try
        {
            string ledgerFilePath = "/Users/damon/Library/Mobile Documents/com~apple~CloudDocs/ledger/meta.ledger";
            if (!File.Exists(ledgerFilePath))
            {
                Console.WriteLine("File meta.ledger not found!");
                return;
            }

            List<string> tickers = new List<string>();
            string[] lines = File.ReadAllLines(ledgerFilePath);
            foreach (string line in lines)
            {
                if (line.Trim().StartsWith("commodity"))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    List<string> ignoreCommoditiesCurrency = new List<string>() { "USD", "BRL", "EUR" };
                    if (parts.Length > 1 && !ignoreCommoditiesCurrency.Contains(parts[1]))
                    {
                        tickers.Add(parts[1].Trim());
                    }
                }
            }

            if (tickers.Count == 0)
            {
                Console.WriteLine("No tickers found in the meta.ledger file.");
                return;
            }

            string outputFilePath = "/Users/damon/Library/Mobile Documents/com~apple~CloudDocs/ledger/market.prices.ledger";

            // Dictionary to store all records by ticker
            Dictionary<string, List<string>> tickerRecords = new Dictionary<string, List<string>>();

            // Read existing records
            if (File.Exists(outputFilePath))
            {
                string[] existingLines = File.ReadAllLines(outputFilePath);
                foreach (string line in existingLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 && parts[0] == "P")
                    {
                        string ticker = parts[2];
                        if (!tickerRecords.ContainsKey(ticker))
                        {
                            tickerRecords[ticker] = new List<string>();
                        }
                        tickerRecords[ticker].Add(line);
                    }
                }
            }

            // Fetch and add new prices
            foreach (string ticker in tickers)
            {
                Console.WriteLine($"Fetching data for {ticker}...");

                DateTime startDate;
                if (tickerRecords.ContainsKey(ticker) && tickerRecords[ticker].Any())
                {
                    string lastLine = tickerRecords[ticker].Last();
                    string[] parts = lastLine.Split(' ');
                    DateTime.TryParseExact(parts[1], "dd/MM/yyyy", null,
                        System.Globalization.DateTimeStyles.None, out DateTime lastDate);
                    startDate = lastDate.AddDays(1);
                }
                else
                {
                    startDate = DateTime.Now.AddDays(-30);
                    if (!tickerRecords.ContainsKey(ticker))
                    {
                        tickerRecords[ticker] = new List<string>();
                    }
                }

                var records = await yahooService.GetRecordsAsync(ticker, startDate);
                foreach (var record in records)
                {
                    string ledgerEntry = $"P {record.Date:dd/MM/yyyy} {ticker} {record.Close.Value.ToString("n2").Replace(".", ",")} USD";
                    tickerRecords[ticker].Add(ledgerEntry);
                    Console.WriteLine($"{ledgerEntry}");
                }

                await Task.Delay(1000);
            }

            // Sort records for each ticker by date and rewrite the file
            using (StreamWriter writer = new StreamWriter(outputFilePath, false)) // false to overwrite
            {
                foreach (var ticker in tickerRecords.Keys.OrderBy(k => k))
                {
                    var records = tickerRecords[ticker];
                    // Sort by date
                    records.Sort((a, b) =>
                    {
                        string dateA = a.Split(' ')[1];
                        string dateB = b.Split(' ')[1];
                        return DateTime.ParseExact(dateA, "dd/MM/yyyy", null)
                            .CompareTo(DateTime.ParseExact(dateB, "dd/MM/yyyy", null));
                    });

                    foreach (var record in records)
                    {
                        await writer.WriteLineAsync(record);
                    }
                    await writer.WriteLineAsync(); // Blank line between tickers
                }
            }

            Console.WriteLine($"\nPrices saved to {outputFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        Console.WriteLine("\nProcess completed...");
    }
}