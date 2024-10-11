@echo off
dotnet publish -c Release -r linux-x64 --self-contained
'dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true
pause