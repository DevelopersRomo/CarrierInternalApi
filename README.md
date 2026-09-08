# CarrierInternalApp API

An ASP.NET Core 10 REST API for managing Carrier's internal server inventory.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/downloads)
- Access to [SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads)

SQL Server Express works for local development on Windows. On macOS, use a
remote SQL Server or Azure SQL endpoint.

## Setup

Clone the repository and enter the project directory:

```console
git clone https://github.com/DevelopersRomo/CarrierInternalApi.git
cd CarrierInternalApi
```

Create your local settings file.

Windows PowerShell:

```powershell
Copy-Item .\appsettings.example.json .\appsettings.json
```

macOS:

```bash
cp appsettings.example.json appsettings.json
```

Edit `appsettings.json` and set:

- `ConnectionStrings:DefaultConnection` to your SQL Server connection string.
- `JwtSettings:SecretKey` to a private random value of at least 32 characters.
- `SeedAdmin:Email` and `SeedAdmin:Password` if you want to create an initial
  administrator; otherwise, leave both empty.

Restore dependencies and build the project:

```console
dotnet restore
dotnet build
```

Install the EF Core 10 command-line tool:

```console
dotnet tool install --global dotnet-ef --version 10.0.0
```

If `dotnet-ef` is already installed, update it instead:

```console
dotnet tool update --global dotnet-ef --version 10.0.0
```

Apply the database migrations:

```console
dotnet ef database update
```

Run the API with its development profile:

```console
dotnet run --launch-profile InternalCarrierApp.API
```

Open Swagger at <https://localhost:49278/swagger>.

## Troubleshooting

If the project does not build, confirm that `dotnet --info` lists an SDK 10.x.
For SQL errors, verify the server address, credentials, database permissions,
network access, and firewall rules in `ConnectionStrings:DefaultConnection`.
