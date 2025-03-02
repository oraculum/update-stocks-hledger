using System.Text.Json;
using System.Text.RegularExpressions;
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
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (!File.Exists(configPath))
            {
                Console.WriteLine("Configuration file config.json not found!");
                return;
            }

            string configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(configJson);

            string ledgerFilePath = config.ledgerFilePath;
            if (!File.Exists(ledgerFilePath))
            {
                Console.WriteLine("File meta.ledger not found!");
                return;
            }

            List<string> regularTickers = new List<string>();
            Dictionary<string, List<(string TargetCurrency, string YahooCode)>> currencyTickers = new Dictionary<string, List<(string, string)>>();

            var currencyTypeRegex = new Regex(@"^type:\s*currency$", RegexOptions.IgnoreCase);
            var codesRegex = new Regex(@"(\w+):([^,\]]+)", RegexOptions.IgnoreCase);

            // Lê e processa o meta.ledger
            string[] lines = File.ReadAllLines(ledgerFilePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                if (line.StartsWith("commodity"))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    string ticker = parts[1].Trim();

                    bool isCurrency = false;
                    bool hasCodes = false;
                    List<(string TargetCurrency, string YahooCode)> conversions = null;

                    int j = i + 1;
                    while (j < lines.Length && lines[j].StartsWith("  "))
                    {
                        string subLine = lines[j].TrimEnd();
                        string trimmedSubLine = subLine.TrimStart();

                        if (currencyTypeRegex.IsMatch(trimmedSubLine))
                        {
                            isCurrency = true;
                        }
                        else if (trimmedSubLine.StartsWith("codes:"))
                        {
                            hasCodes = true;
                            string codesStr = trimmedSubLine.Substring("codes:".Length).Trim();
                            if (codesStr.StartsWith("[") && codesStr.EndsWith("]"))
                            {
                                codesStr = codesStr.Trim('[', ']');
                                conversions = new List<(string, string)>();
                                var matches = codesRegex.Matches(codesStr);
                                foreach (Match match in matches)
                                {
                                    if (match.Groups.Count == 3)
                                    {
                                        string targetCurrency = match.Groups[1].Value.Trim();
                                        string yahooCode = match.Groups[2].Value.Trim();
                                        conversions.Add((targetCurrency, yahooCode));
                                    }
                                }
                            }
                        }
                        j++;
                    }

                    if (isCurrency && hasCodes && conversions != null)
                        currencyTickers[ticker] = conversions;
                    else if (!isCurrency)
                        regularTickers.Add(ticker);

                    // Avança o índice para a próxima commodity
                    i = j - 1;
                }
            }

            if (regularTickers.Count == 0 && currencyTickers.Count == 0)
            {
                Console.WriteLine("No tickers or currencies found in the meta.ledger file.");
                return;
            }

            string outputFilePath = config.outputFilePath;
            string currencyFilePath = config.currencyFilePath;
            Dictionary<string, List<string>> regularRecords = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> currencyRecords = new Dictionary<string, List<string>>();

            // Carrega registros existentes para tickers regulares
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
                        if (!regularRecords.ContainsKey(ticker))
                        {
                            regularRecords[ticker] = new List<string>();
                        }
                        regularRecords[ticker].Add(line);
                    }
                }
            }

            // Carrega registros existentes para moedas
            if (File.Exists(currencyFilePath))
            {
                string[] existingLines = File.ReadAllLines(currencyFilePath);
                foreach (string line in existingLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 && parts[0] == "P")
                    {
                        string ticker = $"{parts[2]}-{parts[3]}";
                        if (!currencyRecords.ContainsKey(ticker))
                        {
                            currencyRecords[ticker] = new List<string>();
                        }
                        currencyRecords[ticker].Add(line);
                    }
                }
            }

            // Processa tickers regulares
            foreach (string ticker in regularTickers)
            {
                Console.WriteLine($"Fetching data for {ticker}...");
                DateTime startDate = GetStartDate(regularRecords, ticker);
                if (!regularRecords.ContainsKey(ticker))
                {
                    regularRecords[ticker] = new List<string>();
                }

                var records = await yahooService.GetRecordsAsync(ticker, startDate);
                foreach (var record in records)
                {
                    string ledgerEntry = $"P {record.Date:yyyy-MM-dd} {ticker} {record.Close.Value.ToString("n2").Replace(".", ",")} USD";
                    regularRecords[ticker].Add(ledgerEntry);
                    Console.WriteLine($"{ledgerEntry}");
                }
                await Task.Delay(1000);
            }

            // Processa moedas
            foreach (var currency in currencyTickers)
            {
                string baseCurrency = currency.Key;
                foreach (var (targetCurrency, yahooCode) in currency.Value)
                {
                    string recordKey = $"{targetCurrency}-{baseCurrency}";
                    Console.WriteLine($"Fetching currency data for {yahooCode} ({targetCurrency}/{baseCurrency})...");
                    DateTime startDate = GetStartDate(currencyRecords, recordKey);
                    if (!currencyRecords.ContainsKey(recordKey))
                    {
                        currencyRecords[recordKey] = new List<string>();
                    }

                    var records = await yahooService.GetRecordsAsync(yahooCode, startDate);
                    foreach (var record in records)
                    {
                        string ledgerEntry = $"P {record.Date:yyyy-MM-dd} {targetCurrency} {record.Close.Value.ToString("n2").Replace(".", ",")} {baseCurrency}";
                        currencyRecords[recordKey].Add(ledgerEntry);
                        Console.WriteLine($"{ledgerEntry}");
                    }
                    await Task.Delay(1000);
                }
            }

            // Escreve registros regulares
            using (StreamWriter writer = new StreamWriter(outputFilePath, false))
            {
                foreach (var ticker in regularRecords.Keys.OrderBy(k => k))
                {
                    var records = regularRecords[ticker];
                    records.Sort((a, b) =>
                    {
                        string dateA = a.Split(' ')[1];
                        string dateB = b.Split(' ')[1];
                        return DateTime.ParseExact(dateA, "yyyy-MM-dd", null)
                            .CompareTo(DateTime.ParseExact(dateB, "yyyy-MM-dd", null));
                    });

                    foreach (var record in records)
                    {
                        await writer.WriteLineAsync(record);
                    }
                    await writer.WriteLineAsync();
                }
            }

            // Escreve registros de moedas
            using (StreamWriter writer = new StreamWriter(currencyFilePath, false))
            {
                foreach (var ticker in currencyRecords.Keys.OrderBy(k => k))
                {
                    var records = currencyRecords[ticker];
                    records.Sort((a, b) =>
                    {
                        string dateA = a.Split(' ')[1];
                        string dateB = b.Split(' ')[1];
                        return DateTime.ParseExact(dateA, "yyyy-MM-dd", null)
                            .CompareTo(DateTime.ParseExact(dateB, "yyyy-MM-dd", null));
                    });

                    foreach (var record in records)
                    {
                        await writer.WriteLineAsync(record);
                    }
                    await writer.WriteLineAsync();
                }
            }

            Console.WriteLine($"\nRegular prices saved to {outputFilePath}");
            Console.WriteLine($"Currency prices saved to {currencyFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        Console.WriteLine("\nProcess completed...");
    }

    private static DateTime GetStartDate(Dictionary<string, List<string>> records, string ticker)
    {
        if (records.ContainsKey(ticker) && records[ticker].Any())
        {
            string lastLine = records[ticker].Last();
            string[] parts = lastLine.Split(' ');
            DateTime.TryParseExact(parts[1], "yyyy-MM-dd", null,
                System.Globalization.DateTimeStyles.None, out DateTime lastDate);
            return lastDate.AddDays(1);
        }
        return DateTime.Now.AddDays(-30);
    }

    public class AppConfig
    {
        public string ledgerFilePath { get; set; }
        public string outputFilePath { get; set; }
        public string currencyFilePath { get; set; }

        public AppConfig()
        {
            ledgerFilePath = string.Empty;
            outputFilePath = string.Empty;
            currencyFilePath = string.Empty;
        }
    }
}