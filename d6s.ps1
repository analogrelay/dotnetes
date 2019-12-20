$ProjectPath = Join-Path (Join-Path $PSScriptRoot "src") "d6s"
dotnet run --project "$ProjectPath" -- @args