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
            // Lê os ticker symbols do arquivo meta.ledger
            string ledgerFilePath = "../meta.ledger";
            if (!File.Exists(ledgerFilePath))
            {
                Console.WriteLine("Arquivo meta.ledger não encontrado!");
                return;
            }

            List<string> tickers = new List<string>();
            string[] lines = File.ReadAllLines(ledgerFilePath);
            foreach (string line in lines)
            {
                if (line.Trim().StartsWith("commodity"))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        tickers.Add(parts[1].Trim());
                    }
                }
            }

            if (tickers.Count == 0)
            {
                Console.WriteLine("Nenhum ticker encontrado no arquivo meta.ledger.");
                return;
            }

            // Arquivo de saída
            string outputFilePath = "../market.prices.ledger.txt";

            // Dicionário para armazenar a última data de cada ativo
            Dictionary<string, DateTime> lastDates = new Dictionary<string, DateTime>();

            // Lê as últimas datas do arquivo existente, se houver
            if (File.Exists(outputFilePath))
            {
                string[] existingLines = File.ReadAllLines(outputFilePath);
                foreach (string line in existingLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 && parts[0] == "P")
                    {
                        if (DateTime.TryParseExact(parts[1], "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime date))
                        {
                            string ticker = parts[2];
                            if (!lastDates.ContainsKey(ticker) || date > lastDates[ticker])
                            {
                                lastDates[ticker] = date;
                            }
                        }
                    }
                }
            }

            using (StreamWriter writer = new StreamWriter(outputFilePath, true)) // true para append
            {
                foreach (string ticker in tickers)
                {
                    Console.WriteLine($"Buscando dados para {ticker}...");

                    DateTime startDate;
                    if (lastDates.ContainsKey(ticker))
                    {
                        // Pega o dia seguinte à última data registrada
                        startDate = lastDates[ticker].AddDays(1);
                    }
                    else
                    {
                        // Se não houver data anterior, pega os últimos 30 dias
                        startDate = DateTime.Now.AddDays(-30);
                    }

                    var records = await yahooService.GetRecordsAsync(ticker, startDate);
                    foreach (var record in records)
                    {
                        string ledgerEntry = $"P {record.Date:dd/MM/yyyy} {ticker} {record.Close.Value.ToString("n2").Replace(".", ",")} USD";
                        await writer.WriteLineAsync(ledgerEntry);
                        Console.WriteLine($"Preço salvo: {ledgerEntry}");
                    }

                    await Task.Delay(1000);
                }
            }

            Console.WriteLine($"\nPreços salvos em {outputFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ocorreu um erro: {ex.Message}");
        }

        Console.WriteLine("\nProcesso finalizado...");
    }
}