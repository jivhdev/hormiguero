# Crea en el escritorio los accesos directos a las apps de Hormiguero de esta carpeta.
# Uso: clic derecho > "Ejecutar con PowerShell" (o: powershell -ExecutionPolicy Bypass -File crear-accesos.ps1)
$carpeta = $PSScriptRoot
$escritorio = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
foreach ($app in 'Archivero', 'Buscadero', 'Mensajero') {
    $exe = Join-Path $carpeta "$app\$app.exe"
    if (-not (Test-Path $exe)) { Write-Host "No se encontró $exe"; continue }
    $acceso = $shell.CreateShortcut((Join-Path $escritorio "$app (Hormiguero).lnk"))
    $acceso.TargetPath = $exe
    $acceso.WorkingDirectory = Split-Path $exe
    $acceso.IconLocation = "$exe,0"
    $acceso.Save()
    Write-Host "Acceso creado: $app (Hormiguero)"
}
Write-Host 'Listo. Puedes cerrar esta ventana.'
Read-Host 'Presiona Enter para salir'
