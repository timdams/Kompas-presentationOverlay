# Presentation Overlay

Windows-sidebar (WPF, .NET 9) voor het publieksscherm tijdens presentaties: toont
allowlisted apps als iconen en licht de actieve op. Gebruikersdocumentatie staat in
[README.md](README.md); dit bestand is voor werken aan de code.

## Commando's

Alles draait vanuit `src/`:

```powershell
dotnet build src -c Release
dotnet run --project src -c Release
```

Er zijn geen tests. Controleer wijzigingen door de app te starten en naar het tweede
scherm te kijken.

## Architectuur

- `SidebarWindow` is het hart: registreert zich als AppBar (`SHAppBarMessage`), luistert
  op `EVENT_SYSTEM_FOREGROUND` plus een timer van 500 ms, en matcht het voorgrondvenster
  tegen de regels uit de config.
- Matching staat in `Rule.Matches` (`Config.cs`): procesnaam, optioneel `className`,
  `titleContains`, `urlContains`. Eerste match wint. Geen match laat de highlight staan.
- `UrlReader` leest de omnibox via UI Automation, asynchroon met een cache per venster.
  De klasse heet `OmniboxViewViews` (Chrome) of `BraveOmniboxViewViews` (Brave), dus op
  "contains Omnibox" filteren. Bij een mislukte lezing blijft de laatst bekende URL gelden
  (nodig in fullscreen, waar de adresbalk verdwijnt).
- `FitBesideSidebar` verkleint een fullscreen venster dat exact de monitor vult. Dat
  gebeurt alleen voor vensters die een regel matchen.
- `Native.cs` bundelt alle P/Invoke. Coördinaten zijn fysieke pixels (manifest staat op
  PerMonitorV2); de breedte uit de config is in DIP en wordt per monitor omgerekend.

## Conventies en valkuilen

- Nullable aan, implicit usings aan, maar WPF haalt `System.IO` uit de implicit usings;
  die staat daarom als `<Using>` in de csproj.
- De overlay mag nooit focus stelen: het venster heeft `WS_EX_NOACTIVATE`. Geen
  `Activate()` of `Focus()` toevoegen.
- Config-parsing is tolerant (opmerkingen en trailing commas mogen). Een kapotte config
  tijdens hot reload laat de vorige werkende config staan.
- De exe leest `config.json` uit zijn eigen map (de `bin/`-kopie), niet uit `src/`.
  Wijzig je `src/config.json`, dan moet je opnieuw bouwen, of een pad meegeven als argument.

## Testen zonder jezelf te blokkeren

Bij het testen van fullscreen-gedrag is een apart browserprofiel veilig
(`--user-data-dir=<tijdelijke map>`), zodat de echte sessie ongemoeid blijft. Beëindig de
overlay via het contextmenu, niet met `Stop-Process -Force`: AppBar-ruimte wordt dan niet
netjes vrijgegeven. Focus forceren vanuit een andere app (`SetForegroundWindow`) werkt niet
betrouwbaar; start een proces dat vanzelf voorgrond krijgt.
