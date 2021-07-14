dotnet build Protsyk.PMS.FullText.Core -c=Release /p:ForNuget=true
dotnet pack Protsyk.PMS.FullText.Core -c=Release /p:ForNuget=true --no-build -o=./

powershell "Get-FileHash .\Protsyk.PMS.FullText.Core.1.3.0.nupkg  -Algorithm SHA512 | Format-List"