$ErrorActionPreference = 'Stop'
$envPath = Join-Path $PSScriptRoot '../.env'
if (Test-Path -LiteralPath $envPath) {
    Write-Host '.env já existe; nenhuma configuração foi alterada.'
    return
}

function New-DockerSecret {
    $bytes = New-Object byte[] 32
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) }
    finally { $generator.Dispose() }
    return 'Aa1!' + [BitConverter]::ToString($bytes).Replace('-', '')
}

$content = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '../.env.example'))
$content = $content.Replace('MSSQL_SA_PASSWORD=', 'MSSQL_SA_PASSWORD=' + (New-DockerSecret))
$content = $content.Replace('APP_DB_PASSWORD=', 'APP_DB_PASSWORD=' + (New-DockerSecret))
$content = $content.Replace('Jwt__Key=', 'Jwt__Key=' + (New-DockerSecret))
[IO.File]::WriteAllText($envPath, $content)
Write-Host '.env criado com senhas e chave JWT aleatórias. Nenhum segredo foi exibido.'
