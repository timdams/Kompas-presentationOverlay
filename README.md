# Presentation Overlay

Een Windows-sidebar voor het publieksscherm tijdens presentaties. De sidebar staat
permanent links op scherm 2 en toont grote iconen van de apps waar je tijdens de
presentatie tussen wisselt (slides, VS Code, terminal, Verkenner, ...). De app waar je nu
in zit licht op, zodat het publiek niet verward raakt bij al dat schakelen.

## Hoe het werkt

- **Allowlist.** Alleen de apps in `config.json` verschijnen. Alle items staan altijd in
  de sidebar, ook als de app nog niet draait. Andere vensters (Teams, Spotify, ...)
  worden genegeerd en laten de highlight ongemoeid.
- **Alleen het publieksscherm telt.** Een venster telt alleen mee als het op de gekozen
  monitor staat. Je speaker view op je eigen scherm verstoort de highlight dus niet.
- **AppBar.** De sidebar registreert zich zoals de taakbalk, waardoor Windows de strook
  vrijhoudt en andere vensters ernaast schuiven.
- **Slides herkennen op URL.** Voor een browser kun je matchen op een stuk van de URL
  (bijvoorbeeld `slides`). De URL wordt uit de adresbalk gelezen via UI Automation
  (Chrome, Brave en Edge). Zo wordt een Quarto-deck "Presentatie" en blijft een gewoon
  browservenster genegeerd.
- **Fullscreen (F11).** Een fullscreen-venster dat de hele monitor vult zou de sidebar
  overdekken. De tool verkleint zo'n venster tot de ruimte rechts van de sidebar.

## Vereisten

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download) om te bouwen

## Bouwen en starten

```powershell
cd src
dotnet run -c Release
```

Of een zelfstandige map maken die je naar een andere machine kunt kopiëren:

```powershell
dotnet publish src -c Release -r win-x64 --self-contained false -o publish
publish\PresentationOverlay.exe
```

Rechtsklik op de sidebar voor **Opties**, **Config herladen** en **Afsluiten**.

## Tegels aanpassen

- **Verplaatsen:** sleep een tegel omhoog of omlaag. De volgorde wordt bewaard.
- **Verbergen:** rechtsklik op een tegel, **Verberg**. Dit geldt alleen voor deze sessie: bij
  de volgende start staan alle tegels er weer.
- **Opties:** het tandwiel linksonder (of rechtsklik, **Opties…**) opent een venster op het
  hoofdscherm. Daar haal je verborgen tegels terug, voeg je een extra app toe (kies een
  draaiende app of blader naar een `.exe`), en stel je scherm, breedte, fullscreen-gedrag en
  accentkleur in.

Wat je in de app instelt staat in `%APPDATA%\PresentationOverlay\user.json` en wint van
`config.json` voor scherm, breedte, fullscreen en accent. Verwijder dat bestand om terug te
gaan naar de standaard.

## Configuratie

De config is `config.json` naast de exe. Een ander pad kan als argument:
`PresentationOverlay.exe D:\pad\config.json`. Wijzigingen worden live herladen. In
`src/config.json` staat de voorbeeldconfig, die bij het bouwen naast de exe wordt gekopieerd.

```json
{
  "monitor": "secondary",
  "width": 260,
  "fitFullscreen": true,
  "accent": "#E4002B",
  "items": [
    { "label": "VS Code",     "process": ["Code.exe"] },
    { "label": "Presentatie", "process": ["brave.exe"], "urlContains": "slides" },
    { "label": "Verkenner",   "process": ["explorer.exe"], "className": "CabinetWClass" }
  ]
}
```

### Instellingen

| Veld | Betekenis |
|---|---|
| `monitor` | `"secondary"` (eerste niet-primaire scherm, standaard), `"primary"`, of een 0-gebaseerde index als tekst (`"1"`) |
| `width` | Breedte van de sidebar in DIP (96-dpi pixels). Standaard 260 |
| `fitFullscreen` | Fullscreen vensters verkleinen tot naast de sidebar. Standaard `true` |
| `hideTaskbar` | Windows-taakbalk automatisch verbergen zolang de overlay draait. Standaard `true` |
| `accent` | Kleur van het actieve item; terugval als `autoAccent` geen kleur in het icoon vindt |
| `autoAccent` | Highlightkleur per tegel uit het icoon halen. Standaard `true`; een accent dat je in Opties kiest wint |
| `hotkeys` | Globale sneltoetsen: modifiers + `1`-`9` springt naar de n-de tegel, modifiers + `0` toont/verbergt de sidebar. Standaard `true` |
| `cursorHighlight` | Gele cirkel rond de muis met een ripple bij klikken, alleen op het scherm van het publiek. Standaard `true`. Aan/uit via het contextmenu of modifiers + `H` |
| `hotkeyModifiers` | Standaard `"Ctrl+Alt+Shift"`. Ctrl+Alt is AltGr en blokkeert `@ # \|` op een Belgisch toetsenbord |
| `items` | Lijst met regels, zie hieronder. De volgorde is de volgorde in de sidebar |

### Regels (`items`)

Alle ingevulde voorwaarden moeten kloppen. Bij meerdere matches wint de eerste regel.

| Veld | Betekenis |
|---|---|
| `label` | Tekst in de sidebar (verplicht) |
| `process` | Lijst procesnamen, met of zonder `.exe` (verplicht) |
| `className` | Vensterklasse, bijvoorbeeld `CabinetWClass` om alleen Verkenner-vensters te matchen en niet het bureaublad |
| `titleContains` | Deel van de venstertitel |
| `urlContains` | Deel van de URL in de adresbalk (Chromium-browsers) |
| `icon` | Pad naar een png/ico, wint van het exe-icoon |
| `exe` | Pad naar de exe waar het icoon uit komt, voor als de app nog niet draait. Omgevingsvariabelen werken (`%ProgramFiles%`) |
| `glyph` | Tekst of emoji als vervanging als er geen icoon is |

Zonder `icon` of `exe` wordt het icoon van het draaiende proces gebruikt. Daarvoor moet
de app minstens één keer gedraaid hebben terwijl de overlay actief is. Tot dan toont de
sidebar de eerste letter van het label.

## Bekende beperkingen

- Bij een fullscreen browser is de adresbalk onzichtbaar. De laatst gelezen URL blijft dan
  gelden, dus eerst naar de slides navigeren en dan pas F11.
- Het verkleinen van fullscreen-vensters gebeurt bij de volgende focuswissel of tick
  (maximaal een halve seconde).
- De sidebar is zo hoog als het werkgebied (zonder taakbalk). In fullscreen zie je daardoor
  linksonder een smal stuk van het venster.
- Monitorwijzigingen (scherm aan- of uitkoppelen) vragen een herstart, of
  "Config herladen" via rechtsklik.
- Afsluiten met `Afsluiten` in het contextmenu geeft de gereserveerde ruimte netjes vrij.
  Beëindig het proces bij voorkeur niet met geweld.

## Projectstructuur

```
src/
  Program.cs            startpunt
  SidebarWindow.cs      venster, AppBar, foreground-hook, matching, fullscreen-fit
  ItemView.cs           één rij in de sidebar (icoon, label, animatie)
  Config.cs             config en matchregels
  UrlReader.cs          URL uit de adresbalk via UI Automation
  Native.cs             Win32 P/Invoke (monitoren, AppBar, iconen, ...)
  app.manifest          per-monitor DPI
  config.json           voorbeeldconfig
```
