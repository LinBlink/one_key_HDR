$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet publish OneKeyHdr.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Write-Host "Ready: $PSScriptRoot\dist\OneKeyHdr.exe"
}
finally { Pop-Location }
