@echo off
dotnet publish "DM_RMS.Wpf\DM_RMS.Wpf.csproj" -c Release -p:PublishProfile="Properties\PublishProfiles\OfflineInstaller.pubxml"
