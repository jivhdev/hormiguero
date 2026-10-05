# Arma el paquete portable de Hormiguero para copiar a otro PC (no necesita instalar .NET).
# Uso: powershell -ExecutionPolicy Bypass -File herramientas\empaquetar.ps1 [-Destino E:\Hormiguero-portable]
param([string]$Destino = 'E:\Hormiguero-portable')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (Test-Path $Destino) { Remove-Item $Destino -Recurse -Force }
New-Item -ItemType Directory -Force $Destino | Out-Null
foreach ($app in 'Archivero', 'Buscadero', 'Mensajero') {
    Write-Host "Publicando $app..."
    dotnet publish (Join-Path $repo "src\Hormiguero.$app") -c Release -r win-x64 --self-contained true `
        -p:PublishReadyToRun=false -o (Join-Path $Destino $app) --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de $app" }
}
Copy-Item (Join-Path $PSScriptRoot 'crear-accesos.ps1') $Destino
Copy-Item (Join-Path $PSScriptRoot 'LEEME-portable.txt') (Join-Path $Destino 'LEEME.txt')
$version = (git -C $repo log -1 --format='%h %cd' --date=format:'%Y-%m-%d %H:%M')
Set-Content -Path (Join-Path $Destino 'VERSION.txt') -Value "Hormiguero $version" -Encoding utf8
Write-Host "Listo: $Destino"
