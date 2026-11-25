
param(
    [string]$ServerInstance = "RALIEN11",
    [string]$BackupDirectory = "D:\SQLBackups"
)

# Campaign Forge - SQL Server backup script
# Uses Windows Integrated Authentication.
# Databases are backed up sequentially.
# Retention cleanup is intentionally not enabled yet.

$ErrorActionPreference = "Stop"

$LogFile = Join-Path $BackupDirectory "BackupLog.txt"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

function Write-Log {
    param([string]$Message)

    $Line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $Message"
    Add-Content -Path $LogFile -Value $Line
    Write-Host $Line
}

function Invoke-SqlQuery {
    param([string]$Query)

    $ConnectionString = "Data Source=$ServerInstance;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;"

    $Connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)

    try {
        $Connection.Open()

        $Command = $Connection.CreateCommand()
        $Command.CommandText = $Query
        $Command.CommandTimeout = 0

        $Reader = $Command.ExecuteReader()
        $Results = @()

        while ($Reader.Read()) {
            $Results += [string]$Reader["name"]
        }

        $Reader.Close()
        return $Results
    }
    finally {
        if ($Connection.State -ne "Closed") {
            $Connection.Close()
        }
        $Connection.Dispose()
    }
}

function Invoke-SqlCommand {
    param([string]$Query)

    $ConnectionString = "Data Source=$ServerInstance;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;"

    $Connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)

    try {
        $Connection.Open()

        $Command = $Connection.CreateCommand()
        $Command.CommandText = $Query
        $Command.CommandTimeout = 0

        [void]$Command.ExecuteNonQuery()
    }
    finally {
        if ($Connection.State -ne "Closed") {
            $Connection.Close()
        }
        $Connection.Dispose()
    }
}

# Confirm the backup directory exists.
if (-not (Test-Path $BackupDirectory)) {
    throw "Backup directory does not exist: $BackupDirectory"
}

Write-Log "========== BACKUP RUN STARTED =========="
Write-Log "SQL Server: $ServerInstance"
Write-Log "Backup directory: $BackupDirectory"

$Failures = 0

try {
    # Discover all online databases except tempdb.
    $Databases = Invoke-SqlQuery @"
SELECT name
FROM sys.databases
WHERE state_desc = N'ONLINE'
  AND name <> N'tempdb'
ORDER BY
    CASE WHEN database_id <= 4 THEN 0 ELSE 1 END,
    database_id;
"@

    if ($Databases.Count -eq 0) {
        throw "No eligible databases were found."
    }

    Write-Log "Found $($Databases.Count) databases."

    foreach ($Database in $Databases) {
        $SafeName = $Database -replace '[\\/:*?"<>|]', '_'
        $BackupFile = Join-Path $BackupDirectory "${SafeName}_${Timestamp}.bak"

        $SqlDatabase = $Database.Replace("]", "]]")
        $SqlFile = $BackupFile.Replace("'", "''")

        Write-Log "Starting backup: $Database"

        try {
            $BackupSql = @"
BACKUP DATABASE [$SqlDatabase]
TO DISK = N'$SqlFile'
WITH INIT, CHECKSUM, STATS = 10;

RESTORE VERIFYONLY
FROM DISK = N'$SqlFile'
WITH CHECKSUM;
"@

            Invoke-SqlCommand -Query $BackupSql

            Write-Log "SUCCESS: $Database - backup verified."
        }
        catch {
            $Failures++
            Write-Log "FAILED: $Database - $($_.Exception.Message)"
        }
    }
}
catch {
    $Failures++
    Write-Log "BACKUP PROCESS ERROR: $($_.Exception.Message)"
}
finally {
    Write-Log "Backups attempted: $($Databases.Count)"
    Write-Log "Failures: $Failures"
    Write-Log "========== BACKUP RUN FINISHED =========="
}

if ($Failures -gt 0) {
    throw "Backup run completed with $Failures failure(s). Review $LogFile"
}

Write-Host ""
Write-Host "All database backups completed and passed verification." -ForegroundColor Green
