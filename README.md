# soad-csharp

Welcome to `soad-csharp`! This project contains independent trading strategies written in C# that are based off the [`soad`](https://github.com/r0fls/soad) database and its accompanying dashboard + API.


## Setting Up the Database

To get started, you need to initialize and seed the database. Follow these steps:
1. Create secrets.json
 
    ```json
    {
      "TestStrategy": {
        "ApiKey": "...",
        "ApiSecret": "..."
      },
      "OtherStrategy": {
        "ApiKey": "...",
        "ApiSecret": "..."

      },
      "ConnectionStrings": {
        "TradeDb": "Host=localhost;Port=5432;Database=TradeDb;Username=postgres;Password=..."
      }
    }
    ```
2. Run code first migrations to create the tradedb.db file
    
    ```bash
    dotnet ef migrations add init1
    dotnet ef database update
    ```

## Acknowledgments 

   * Special thanks to [r0fls](https://github.com/r0fls) for creating the `soad` project, which serves as inspiration.  