using InternalCarrierApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InternalCarrierApp.API.Data;

public static class ServerSeedData
{
    private static readonly IReadOnlyList<SeedServer> Servers =
    [
        new(1, "VMC7222QA010", "10.93.42.14", "AMC Server", "AMC Server", "AWS", "QA", "Virtual", "Apps", null, "Windows Server 2022 Datacenter", 8, "Intel Xeon Platinum 8259CL", "125Gb", "15.9Gb", null, null, null, "UTEC"),
        new(2, "CMXMOK06", "10.93.51.16", "App - Acronis & xLink Adapter", "ACRONIS & Aegis Prod xLink", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2012 R2", 8, "2", "99.9Gb", "99.9Gb", null, "CE0002215", null, "UTEC"),
        new(3, "VMC8107PA005", "10.92.38.150", "App - Dataworks / Laser / XRAY", "Aegis Prod DB", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2016", 12, "4", "126Gb", "15.9Gb", "31.8Gb", "CE0002216", null, "UTEC"),
        new(4, "CMXMOA0I", "172.29.82.22", "App - Factory Logix Primary", "Aegis Prod App Primary & Bartender", "OnPrem", "PRD", "Virtual", "Apps", null, "Windows Server 2012 R2", 16, "8", "99.9Gb", "249Gb", null, "CE0002215", null, "UTEC"),
        new(5, "CMXMOA1I", "172.29.82.204", "App - Factory Logix Secondary", "Aegis Prod App Secondary", "OnPrem", "PRD", "Physical", "Apps", null, "Windows Server 2016", 32, "12", "222Gb", "222Gb", null, "CE0002216", null, "UTEC"),
        new(6, "CMXMOK04", "10.93.51.159", "App - Factory Logix Unique", "Aegis QA App", "AWS", "QA", "Virtual", "SQL", "SQL 2012", "Windows Server 2019", 32, "12", "114Gb", "499Gb", "199Gb", null, null, "UTEC"),
        new(7, "CMXMOA0H", "172.29.82.21", "App - Fuji FLEXA (1-4)", "Fuji FLEXA", "OnPrem", "PRD", "Virtual", "Apps", null, "Windows Server 2012 R2", 16, "8", "99.9Gb", "199Gb", null, "CE0002215", null, "UTEC"),
        new(8, "CMXMOK05", "172.29.82.29", "App - Interface / Bartector / Universe / AOI", "Aegis QA DB", "OnPrem", "QA/PRD", "Virtual", "SQL", "SQL 2012", "Windows Server 2012 R2", 8, "4", "89.9Gb", "499Gb", "199Gb", "CE0002215", "CE0002213", "UTEC"),
        new(9, "CMXMOA0W", "172.29.82.20", "App - Omron SMT01/03/06", "Omron Prod", "OnPrem", "PRD", "Virtual", "Apps-SQL", "SQL 2008", "Windows Server 2008 R2", 8, "4", "179Gb", "1999Gb", null, "CE0002204", "CE0002212", "UTEC"),
        new(10, "CMXMOA0K", "172.29.82.24", "App - Omron Nuevo SMT04", "Omron QA", "OnPrem", "PRD", "Physical", "Apps-SQL", "SQL 2019", "Windows Server 2016", 8, "8", "278Gb", "278Gb", null, "CE0002217", "CE0002214", "UTEC"),
        new(11, "CMXMOD05", "172.29.82.26", "Database - AegisInterface / Universe / Bartector", "Aegis Prod Laser Serials", "OnPrem", "PRD", "Virtual", "SQL", "SQL 2012", "Windows Server 2012 R2", 16, "4", "129Gb", "499Gb", "199Gb", "CE0002215", "CE0002213", "UTEC"),
        new(12, "CMXSCD15", "172.29.82.73", "Database - AOI Kouh Yong", "Kouh Yong", "OnPrem", "PRD", "Virtual", "SQL", "SQL 2016", "Windows Server 2016", 128, "32", null, null, null, "CE0002217", "CE0002214", "UTEC"),
        new(13, "VMC10134DA035", "10.233.194.54", "Database - FactoryLogix 2 / Enterprise WH", "Aegis QA DB & Prod WH", "AWS", "QA", "Virtual", "Apps-SQL", "SQL 2016", "Windows Server 2022 Datacenter", 32, "12", "127Gb", null, null, null, null, "UTEC"),
        new(14, "CMXSCD0S", "172.29.82.59", "Database - FactoryLogix Primary", "Aegis Prod DB Primary", "OnPrem", "PRD", "Physical", "SQL", "SQL 2019", "Windows Server 2016", 128, "32", null, null, null, "CE0002216", null, "UTEC"),
        new(15, "CMXSCD0T", "172.29.82.58", "Database - FactoryLogix Secondary", "Aegis Prod DB Secondary", "OnPrem", "PRD", "Physical", "SQL", "SQL 2019", "Windows Server 2016", 128, "32", "219Gb", null, "2.59Tb", "CE0002216", null, "UTEC"),
        new(16, "VMC8107PA004", "10.92.38.104", "EDICOM PROD", "SAP EDICOM Facturación", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2022 Datacenter", 32, "Intel Xeon Platinum 8488C", "136Gb", "31.9Gb", "45.9Gb", null, null, "UTEC"),
        new(17, "VMC8107QA001", "10.93.51.39", "EDICOM QA", "EDICOM QA", "AWS", "QA", "Virtual", "Apps", null, "Windows Server 2022", 8, "Intel Xeon Platinum 8259CL", "148Gb", "15.9Gb", "45.9Mb", null, null, "UTEC"),
        new(18, "CMXSCF04", "172.29.82.93", "File Share Server", "All Test/Laser/Functional Machines", "OnPrem", "PRD", "Virtual", "File Share", null, "Windows Server 2019", 16, "4", "199Gb", "4.49Tb", "3.69Tb", null, null, "UTEC"),
        new(19, "AI260592", "172.29.67.183", "Fuji NEXIM", "Fuji Nexim", "CMXF PC", "PRD", "PC", "Apps", null, "Windows 10", 192, "Intel Xeon Silver 4110", "237Gb", "237Gb", "953Gb", null, null, "UTEC"),
        new(20, "VMCHVM0PF001", "10.92.38.21", "MFT", "SAP/MES/SUPPORTAL/AOI/VISION", "AWS", "PRD", "Virtual", "File Transfer", null, "Windows Server 2022 Datacenter", 16, "Intel Xeon Platinum 8259CL", "126Gb", "149Gb", null, null, null, "UTEC"),
        new(21, "5DFK0R3", "172.29.67.65", "MFT XML / Vision Pictures", "MFT, Vision, MES XML", "CMXF PC", "PRD", "PC", "Apps", null, "Windows 10", null, null, null, null, null, null, null, "UTEC"),
        new(22, "CMXMOA0Z", "172.29.82.173", "On Guard / Windchill", "Windchill", "OnPrem", "PRD", "Physical", "Apps-SQL", null, "Windows Server 2016", 8, "6", "278Gb", null, "5.19Tb", "CE0002216", null, "UTEC"),
        new(23, "VMC8107PD003", "10.92.38.62", "PPV Tracker DB", "PPV Tracker", "AWS", "PRD", "Virtual", "SQL", "SQL 2016", "Windows Server 2016", 16, "2", "126Gb", "31.9Gb", null, "CE0002217", "CE0002214", "UTEC"),
        new(24, "VMC8107PW001", "10.92.38.30", "PPV Tracker IIS", "PPV Tracker", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2016", 8, "2", "126Gb", "15.9Gb", null, "CE0002216", null, "UTEC"),
        new(25, "VMC8107QD002", "10.93.51.161", "PPV Tracker QA DB", "PPV Tracker", "AWS", "QA", "Virtual", "SQL", "SQL 2016", "Windows Server 2016", 8, "2", "148Gb", "99.8Gb", null, null, null, "UTEC"),
        new(26, "CMXMOP01", "10.92.38.13", "Print - Printer Honeywell Driver", "Laser/WebTools/MES shopfloor", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2022", 4, "2", "87Gb", "99.9Gb", null, null, null, "UTEC"),
        new(27, "CMXMOK08", "10.233.9.184", "Print - Whitepath Commander/Bartender", "Bartender", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2012 R2", 16, "4", "129Gb", "99.9Gb", null, "CE0002215", null, "UTEC"),
        new(28, "VMC10134AJOLH01", "10.233.29.234", "SAP Print Server (Multi-Plant)", "SAP - UTEC MX/BNA/MTY/Whitepath/Saltillo/Georgia", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2019", 4, "2", "59.9Gb", "127Gb", null, null, null, "UTEC"),
        new(29, "CMXMOA0J", "10.92.38.44", "Service - xLink Adapter 1", "Aegis Prod xLink", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2016", 16, "16", "99.5Gb", "99.9Gb", null, "CE0002216", null, "UTEC"),
        new(30, "CMXMOA0V", "10.92.38.6", "Service - xLink Adapter 2", "Aegis Prod xLink", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2019", 16, "16", "119Gb", "99.9Gb", null, null, null, "UTEC"),
        new(31, "CMXMOA10", "10.92.38.22", "Service - xLink Adapter 3 (Laser XML)", "Aegis Prod xLink", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2016", 8, "8", "99.9Gb", "49.9Gb", null, "CE0002216", null, "UTEC"),
        new(32, "CMXMOA1H", "172.29.82.202", "Service - xLink Adapter 4", "Aegis Prod xLink", "Virtual Machine", "PRD", "Physical", "Apps", null, "Windows Server 2019", 32, "24", "222Gb", "222Gb", null, null, null, "UTEC"),
        new(33, "CMXMOA1K", "172.29.82.67", "Service - xLink Adapter 5", "Aegis Prod xLink", "OnPrem", "PRD", "Virtual", "Apps", null, "Windows Server 2016", 8, "4", "118Gb", "49Gb", null, "CE0002216", null, "UTEC"),
        new(34, "VMC10134AWNLC01", "10.233.31.43", "Service - xLink Adapter 6", "Aegis Prod xLink", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2019", 8, "4", "127Gb", null, null, null, null, "UTEC"),
        new(35, "VMC8107PA009", "10.92.38.36", "Service - xLink Adapter 7", "Aegis Prod xLink", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2016", 16, "8", "125Gb", "63.9Gb", null, "CE0002216", null, "UTEC"),
        new(36, "CMXMOA0Y", "10.233.17.162", "UTEC Access Control (OnGuard)", "OnGuard", "AWS", "PRD", "Virtual", "Infra", null, "Windows Server 2019", 4, "16", "109Gb", "5.99Gb", "5.99Gb", null, null, "UTEC"),
        new(37, "CMXSCD0C", null, "Database", "SSIS DTSX", "OnPrem", "QA", "Physical", "SQL", "SQL 2022", "Windows Server 2022 Datacenter", 16, "Intel(R) Xeon(R) Platinum 8259CL CPU @ 2.50GHz   2.50 GHz", "114GB", "280GB", "Disk L: 149, Disk T: 199", null, null, "A"),
        new(38, "CMXSCD19", null, "Aplicaciones Desarrollo QA", "Angular, Net10", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2025 Standard", 32, "Intel(R) Xeon(R) Platinum 8452Y (2.00 GHz)", "99GB", "249Gb", null, null, null, "F"),
        new(39, "CMXSCA1M", null, "Ignition", "Ingnition Apps", "AWS", "PRD", "Virtual", "Apps", null, "Windows Server 2022 Standard", 32, "Intel(R) Xeon(R) Platinum 8452Y   2.00 GHz", "129Gb", "127Gb", null, null, null, "A"),
        new(40, "CMXSCD13", null, "Database", "Ignition Db", "AWS", "PRD", "Virtual", "SQL", "SQL 2022", "Windows Server 2022 Standard", 64, "Intel(R) Xeon(R) Platinum 8452Y   2.00 GHz", "129GB", "255Gb", null, null, null, "A"),
        new(41, "sqc8107qmi002.229b7873c6f5.database.windows.net", null, "Database BPCS", "BPCS QA", "Azure", "QA", "Virtual", "SQL", "SQL 2022", "Windows Server 2022 Standard", 64, null, null, null, null, null, null, "A"),
        new(42, "VMA10134ASJGA01", null, "Aplication Server Dev", "Development", null, null, null, null, null, null, null, null, null, null, null, null, null, "A"),
        new(43, "sc-601242391086-pp-dkvfynpogsu42-mssqldb-uxdowvwq8pcy.cshbmfldesup.us-east-1.rds.amazonaws.com", null, "Base de datos", "Development", null, null, null, null, null, null, null, null, null, null, null, null, null, "A"),
        new(0, "VMC10134DD226.carcgl.com", "10.93.51.108", "Base de datos", "Development", "AWS", "QA", "Virtual", "SQL", "SQL 2022", "Windows Server 2022 Datacenter", null, null, null, null, null, null, null, "G"),
        new(0, "CMXSCD1A.carcgl.com", "172.29.82.220", "Base de datos", "Produccion", "AWS", "PRD", "Virtual", "SQL", "SQL 2022", "Windows Server 2022 Datacenter", null, null, null, null, null, null, null, "G"),
        new(44, "VMC10134DA224", "10.93.51.111", "Python, Ignition Apps QA", "Development", "AWS", "QA", "Virtual", "Apps-Ignition", null, "Windows Server 2022 Datacenter", 32, "AMD EPYC 7571   2.20 GHz", "99Gb", "255GB", null, null, null, "G"),
        new(45, "CMXSCA1O", "172.29.82.221", "Python, Ignition Apps Prod", "Python Ignition, Production Apps", "OnPrem", "PRD", "Physical", "Apps-Ignition", null, "Windows Server 2022 Standard", 32, "Intel(R) Xeon(R) Gold 6326 CPU @ 2.90GHz   2.89 GHz", "130Gb", "149Gb", null, null, null, "G")
    ];

    public static readonly IReadOnlySet<string> ServerNames = Servers
        .Select(server => server.ServerName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static async Task InitializeAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var plantCodes = Servers.Select(server => server.PlantCode).Distinct().ToArray();
        var plantIds = await db.Plants
            .Where(plant => plantCodes.Contains(plant.Code))
            .ToDictionaryAsync(plant => plant.Code, plant => plant.Id, cancellationToken);

        var missingPlantCodes = plantCodes.Except(plantIds.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        if (missingPlantCodes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Cannot seed servers because these plants do not exist: {string.Join(", ", missingPlantCodes)}.");
        }

        var seedNames = ServerNames.ToArray();
        var existingNames = (await db.Servers
                .Where(server => seedNames.Contains(server.ServerName))
                .Select(server => server.ServerName)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingServers = Servers
            .Where(server => !existingNames.Contains(server.ServerName))
            .Select(server => server.ToEntity(plantIds[server.PlantCode]))
            .ToList();

        if (missingServers.Count == 0)
        {
            return;
        }

        db.Servers.AddRange(missingServers);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {ServerCount} servers from the Carrier Mexico inventory.", missingServers.Count);
    }

    private sealed record SeedServer(
        int Priority,
        string ServerName,
        string? IpAddress,
        string? ApplicationDescription,
        string? AppName,
        string? Site,
        string? Environment,
        string? InfraType,
        string? ResourceType,
        string? SqlVersion,
        string? OperativeSystem,
        int? RamGb,
        string? CpuQty,
        string? DiskC,
        string? DiskD,
        string? DiskE,
        string? CeNumberServer,
        string? CeNumberSql,
        string PlantCode)
    {
        public ServerRecord ToEntity(int plantId) => new()
        {
            PlantId = plantId,
            Priority = Priority,
            ServerName = ServerName,
            IpAddress = IpAddress,
            ApplicationDescription = ApplicationDescription,
            AppName = AppName,
            Site = Site,
            Environment = Environment,
            InfraType = InfraType,
            ResourceType = ResourceType,
            SqlVersion = SqlVersion,
            OperativeSystem = OperativeSystem,
            RamGb = RamGb,
            CpuQty = CpuQty,
            DiskC = DiskC,
            DiskD = DiskD,
            DiskE = DiskE,
            CeNumberServer = CeNumberServer,
            CeNumberSql = CeNumberSql
        };
    }
}
