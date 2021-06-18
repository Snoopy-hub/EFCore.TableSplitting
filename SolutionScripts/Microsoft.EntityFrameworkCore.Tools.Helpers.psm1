function EFCore.Scaffold.SqlServer
{
    param
    (
        [string] $dataSource,
        [string] $initialCatalog,
        [string] $userId,
        [string] $password,
        [string[]] $tableNames = @()
    )

    $providerName = "Microsoft.EntityFrameworkCore.SqlServer"
    $contextDir = "Entities/EFCore/$initialCatalog"
    $modelsDir = "$contextDir/Models"
    $connectionString = "Data Source=$dataSource;Initial Catalog=$initialCatalog;User ID=$userId;Password=$password;MultipleActiveResultSets=True;Connect Timeout=180;"
    $contextName = $initialCatalog

    EFCore.Scaffold.Tables $modelsDir $contextDir $connectionString $providerName $contextName $tableNames -force:$true
}

function EFCore.Scaffold.Tables
{
    param
    (
        [string] $modelsDir,
        [string] $contextDir,
        [string] $connectionString,
        [string] $providerName,
        [string] $contextName,
        [string[]] $tableNames = @(),
        [switch] $force
    )

    $DEFAULT_SIFFIX = "Context"

    $suffixPosition = $contextName.LastIndexOf($DEFAULT_SIFFIX)

    if($suffixPosition -eq -1)
    {
        $contextName = $contextName + $DEFAULT_SIFFIX
    }

    $project = Get-Project
    $projectDir = GetProperty $project.Properties 'FullPath'
    $scaffoldedTablesDirectoryPath = Join-Path -Path $projectDir -ChildPath $modelsDir

    $scaffoldedPathExists = Test-Path -Path $scaffoldedTablesDirectoryPath

    if($scaffoldedPathExists)
    {
        $scaffoldedTableNames = Get-ChildItem $scaffoldedTablesDirectoryPath -Include @("*.cs") -Recurse | select -ExpandProperty BaseName | ForEach-Object { $_.Split(".")[0] }
        $tableNames = $tableNames + $scaffoldedTableNames
    }

    $tableNames = $tableNames | select -Unique | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    if($tableNames.Length -le 0)
    {
        Scaffold-DbContext $connectionString $providerName -Context $contextName -ContextDir $contextDir -OutputDir $modelsDir -Verbose:$true -Force:$true -UseDatabaseNames:$true
    }
    else
    {
        Scaffold-DbContext $connectionString $providerName -Context $contextName -Tables $tableNames -ContextDir $contextDir -OutputDir $modelsDir -Verbose:$true -Force:$true -UseDatabaseNames:$true
    }
}

function GetProperty($properties, $propertyName)
{
    try
    {
        return $properties.Item($propertyName).Value
    }
    catch
    {
        return $null
    }
}
