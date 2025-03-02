# Finance Ledger Updater

## Overview
This project is a C# application designed to update financial ledger files by fetching market prices and currency exchange rates from Yahoo Finance. It processes a `meta.ledger` file to identify commodities (stocks or currencies), retrieves their latest quotes, and saves the results in separate ledger files for regular tickers and currencies.

The application collects all commodities configured in the `meta.ledger` file and determines the last recorded date in the corresponding output file (`market.prices.ledger` for stocks or `currency.prices.ledger` for currencies). It then fetches quotes from that date up to the current date and appends them to the file. If no previous records exist for a commodity, it retrieves the last 30 days of quotes as a starting point.

Each time the application runs, it incrementally updates the ledger files. Since multiple commodities are stored in the same file, new quotes are appended after the existing ones for each commodity, maintaining a grouped structure. For example, if quotes for JNJ already exist, the latest quotes are added at the end of the JNJ section before moving on to the next commodity. Here’s an example of how the output might look in `market.prices.ledger` after running the application:


```
P 2025-02-20 JNJ 159,68 USD
P 2025-02-21 JNJ 162,30 USD
P 2025-02-24 JNJ 163,74 USD
P 2025-02-25 JNJ 166,09 USD
P 2025-02-26 JNJ 163,08 USD
P 2025-02-27 JNJ 163,73 USD
P 2025-02-28 JNJ 162,95 USD


P 2025-02-20 MSFT 416,13 USD
P 2025-02-21 MSFT 408,21 USD
P 2025-02-24 MSFT 404,00 USD
P 2025-02-25 MSFT 397,90 USD
P 2025-02-26 MSFT 399,73 USD
P 2025-02-27 MSFT 392,53 USD
P 2025-02-28 MSFT 391,25 USD

P 2025-02-20 MDB 290,00 USD
P 2025-02-21 MDB 273,26 USD
P 2025-02-24 MDB 267,10 USD
P 2025-02-25 MDB 259,71 USD
P 2025-02-26 MDB 268,30 USD
P 2025-02-27 MDB 262,41 USD
P 2025-02-28 MDB 263,54 USD
```

## Features
- Fetches stock prices and currency exchange rates from Yahoo Finance.
- Supports a configurable `meta.ledger` file with commodities and currency definitions.
- Separates output into `market.prices.ledger` (stocks) and `currency.prices.ledger` (exchange rates).
- Handles flexible formatting for currency definitions using regular expressions.

## Requirements
- .NET SDK (version 8.0 or higher)
- Access to Yahoo Finance API via the [Finance.Net](https://github.com/thorstenalpers/Finance.NET) library. We use `Finance.Net` because it performs web scraping of Yahoo Finance data, as Yahoo discontinued its official quote API on 2017. This library enables us to fetch financial data despite the lack of an official API endpoint.
- A `config.json` file with file paths

## Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/finance-ledger-updater.git
2. Restore dependencies:

```
dotnet restore
```
3. Build the project:

```
dotnet build
```

## Configuration

Create a config.json file in the executable directory with the following structure:

```
{
    "ledgerFilePath": "path/to/meta.ledger",
    "outputFilePath": "path/to/market.prices.ledger",
    "currencyFilePath": "path/to/currency.prices.ledger"
}
```

I prefer to keep my commodity declarations separate in a dedicated file called meta.ledger. In this configuration, you must specify the path to the file where all commodity declarations are located. The application will read this file, locate all commodity definitions, and fetch their respective quotes from Yahoo Finance.

Additionally, I like to separate the price quotes for stock-type commodities from those of currency-type commodities, which is why this tool uses two output files: market.prices.ledger for stocks and currency.prices.ledger for exchange rates. If desired, you can later combine the two files manually by copying and pasting their contents.


## Usage

1. Define your commodities in the meta.ledger file. Example:

```
commodity BRL
  type: currency
  codes: [USD:BRL=X,EUR:EURBRL=X]
  format 1.000,00 BRL
commodity EUR
  format 1.000,00 EUR
commodity USD
  format 1.000,00 USD
  type: currency
  codes: [EUR:EUR=X]

commodity JNJ
commodity AAPL
commodity KO
commodity PG
```

Note that "type: currency" and "codes: []"" are not part of the official hledger documentation. We use them in this project to simplify the code's construction.

The codes: property is a list of conversion codes we want to fetch. 

Each entry follows the format TARGET:YAHOO_CODE, where TARGET is the target currency and YAHOO_CODE is the Yahoo Finance ticker for that conversion (e.g., USD:BRL=X for USD to BRL). 

Since hledger can infer reverse conversions automatically, you don’t need to declare all possible pairs. For example, I manage accounts in three currencies: BRL, USD, EUR and declare just three conversions (USD→BRL, EUR→BRL, and EUR→USD), letting hledger handle the rest.

2. Run the application:

```
dotnet run
```

3. Check the output files:

- market.prices.ledger for stock prices (e.g., P 2025-01-01 NSC 150,25 USD)
- currency.prices.ledger for exchange rates (e.g., P 2025-01-01 USD 5,85 BRL)



## Notes

Commodities with type: currency but no codes: are ignored.

The application fetches data only for dates after the last recorded entry in the output files.

