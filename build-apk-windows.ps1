$ErrorActionPreference = "Stop"

Write-Host "Instaluju MAUI Android workload..."
dotnet workload install maui-android

Write-Host "Obnovuju balíčky..."
dotnet restore .\PIDMobileSpeaker.csproj

Write-Host "Sestavuju Debug APK..."
dotnet build .\PIDMobileSpeaker.csproj -f net8.0-android -c Debug -p:AndroidPackageFormat=apk

Write-Host ""
Write-Host "Hotová APK:"
Get-ChildItem -Recurse -Filter *.apk | ForEach-Object { Write-Host $_.FullName }
