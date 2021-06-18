function Location.Use
{
    [CmdletBinding()]
    param 
    (
        [string] $location,

        $params,

        [Parameter(Mandatory = $true)]
        [scriptblock]
        $scriptBlock
    )

    $oldLocation = Get-Location

    if(-not ([System.String]::IsNullOrWhiteSpace($location)))
    {
        Set-Location $location
    }

    Invoke-Command $scriptBlock -ArgumentList $params

    Set-Location $oldLocation
}

function DotNet.UserSecrets.Use
{
    param
    (
        [string] $projectFolder,

        $params,

        [Parameter(Mandatory = $true)]
        [scriptblock]
        $scriptBlock
    )

    Location.Use ($projectFolder) @($scriptBlock, $params) {
        param($scriptBlock, $params)

        try
        {
            Invoke-Command $scriptBlock -ArgumentList $params
        }
        catch
        {
            dotnet user-secrets init
        
            Invoke-Command $scriptBlock -ArgumentList $params
        }
    }
}

function DotNet.UserSecrets.Find
{
    param($secrets, [string] $name)

    $result = $secrets | Where-Object { $_.Name -eq $name } | Select-Object -First 1

    if($result.Length -le 0)
    {
        return $null
    }

    return $result.Value
}

function DotNet.UserSecrets.Set
{
    param([string] $projectFolder, $secrets)

    DotNet.UserSecrets.Use ($projectFolder) $null {
        foreach($secret in $secrets)
        {
            dotnet user-secrets set $secret.Name $secret.Value 
        }
    }
}

function DotNet.UserSecrets.Get
{
    param([string] $projectFolder)

    $secrets = New-Object Collections.Generic.List[object]

    DotNet.UserSecrets.Use ($projectFolder) @($secrets, $null) {
        param($secrets)

        $secretLines = dotnet user-secrets list

        foreach($secretLine in $secretLines) 
        { 
            $pair = $secretLine.Split("=")

            $secrets.Add(@{ Name = $pair[0].TrimEnd(); Value = $pair[1].TrimStart() })
        }
    }

    return $secrets
}
