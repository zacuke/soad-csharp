# soad-csharp

Welcome to `soad-csharp`! This project contains independent trading strategies written in C# that are designed to leverage the [`soad`](https://github.com/r0fls/soad) database and its accompanying dashboard + API.

## Overview

The purpose of `soad-csharp` is to provide example strategies in C# that interact with the `soad` infrastructure. The strategies in this repository are designed to:

- Utilize the `soad` database as a backend.
- Leverage the `soad` dashboard and its API for enhanced functionality.
> **Note:** Before running these strategies, it is important to set up and seed the `soad` database schema.

## Prerequisites

- Install and set up the `soad` project. Follow the instructions in the official repository: [r0fls/soad](https://github.com/r0fls/soad).
 
## Setting Up the `soad` Database

To get started with `soad`, you need to initialize and seed the database. Follow these steps:

1. Clone the `soad` repository:

   ```bash
   git clone https://github.com/r0fls/soad.git
   cd soad
   ```
2. Run the init_db.py script to initialize and seed the database: 
	```bash
	python init_db.py
	```
3. Start the `soad` dashboard and API as described in the `soad` documentation.
5. See [multi-launcher](https://github.com/zacuke/multi-launcher) for an example to launch React and Python from a single process.

## Setting Up soad-csharp 


1. Clone the `soad-csharp` repository: 
2. Modify `appsettings.json` to point to `soad` database.
     
 
    ```bash
    git clone https://github.com/zacuke/soad-csharp.git
    cd soad-csharp
    dotnet run
    ```

3. Add to [multi-launcher](https://github.com/zacuke/multi-launcher) to launch the C# strategy alongside the React and Python processes.
     
## Acknowledgments 

   * Special thanks to [r0fls](https://github.com/r0fls) for creating the `soad` project, which serves as inspiration, database, and dashboard api.  