# STRÖM – Windows-Version (WinUI 3)

Portierung der macOS-App zur Steuerung eines IKEA Dirigera-Hubs.

## Voraussetzungen

- Windows 10 1809+ (besser Windows 11)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (17.8 oder neuer) mit Workload **„.NET Desktop-Entwicklung“**

## Öffnen und starten

1. Ordner entpacken.
2. `STROM.csproj` in Visual Studio öffnen.
3. NuGet-Pakete werden automatisch geladen.
4. **Wichtig**: In der Dropdown neben dem grünen Start-Button **„STROM (Unpackaged)“** oder einfach das normale Profil wählen (da `WindowsPackageType=None` gesetzt ist).
5. Zielplattform **x64** wählen.
6. Starten (F5).

Falls Visual Studio trotzdem meckert:
- Rechtsklick auf das Projekt → „NuGet-Pakete verwalten“ → alle wiederherstellen.
- Oder einmal `dotnet restore` im Ordner ausführen.

## Erste Nutzung

1. Tab **Einstellungen**
2. IP des Dirigera eintragen
3. Token erzeugen (Taste am Hub drücken) oder vorhandenen Token einfügen
4. Geräte suchen und übernehmen
5. In **Steuerung** bedienen

Einstellungen und Geräteliste werden unter `%LocalAppData%\STROM\settings.json` gespeichert.
