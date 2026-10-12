# Shared by update-tutorials.ps1 and export-tutorials.ps1: access to the site's "nikoo" database.

function Get-NikooConnectionString([string]$Site) {
    [xml]$config = Get-Content (Join-Path $Site 'Web.config') -Raw
    $entry = @($config.configuration.connectionStrings.add) | Where-Object { $_.name -eq 'Nikoo' } | Select-Object -First 1
    if (-not $entry) { throw 'Web.config has no "Nikoo" connection string.' }
    $entry.connectionString
}

function Invoke-NikooQuery([string]$ConnectionString, [string]$Sql, [hashtable]$Parameters = @{}) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Sql
        foreach ($name in $Parameters.Keys) {
            $value = $Parameters[$name]
            $parameter = $command.Parameters.AddWithValue("@$name", $(if ($null -eq $value) { [DBNull]::Value } else { $value }))
            if ($value -is [datetime]) { $parameter.SqlDbType = [System.Data.SqlDbType]::DateTime2 }
        }
        $table = New-Object System.Data.DataTable
        $table.Load($command.ExecuteReader())
        , $table
    }
    finally { $connection.Dispose() }
}
