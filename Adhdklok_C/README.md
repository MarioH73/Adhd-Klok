# ADHD Klok

Een klein bureaubladklokje voor Windows, gemaakt met C# en .NET 8 WinForms. Het klokje toont de tijd, datum, een afteltimer en het actuele weer.

## Starten

Benodigd: de .NET 8 SDK en Windows.

```powershell
dotnet run --project .\Adhdklok_C\AdhdKlok.csproj
```

De klok is randloos. Sleep het venster om het te verplaatsen en klik op het instellingen-icoon in het berichtenpaneel onderaan om het uitklapmenu te openen. Het icoon draait kort en het menu klapt na een korte vertraging onder het icoon open. Klik nogmaals op het icoon om het menu omhoog te laten inklappen. Openen en sluiten duren ongeveer 0,6 seconde. Via `Weergave` kies je het donkere thema of `Licht (Windows)`; de keuze wordt bewaard in `Data/theme.txt`. Het menu gebruikt in het donkere thema de kleuren van het berichtenpaneel met een donkerrode zijrand; een donkerrode scheidingslijn scheidt het instellingen-icoon van de melding. In het menu kun je ook een timer instellen, de weerlocatie wijzigen of de app afsluiten. Onder `Timer > Alarmgeluid` kun je kiezen uit de vier WAV-bestanden. De geselecteerde toon wordt gebruikt wanneer de timer afloopt. De vensterpositie staat in `Data/position.txt`.

Wanneer de timer afloopt, verschijnt in het berichtenpaneel de melding dat je vijf minuten pauze moet nemen. De afbeeldings-, WAV- en positiebestanden worden tijdens het bouwen naar de uitvoermap gekopieerd.

Het weer wordt opgehaald via Open-Meteo en vereist geen API-sleutel of extra NuGet-pakketten. Een internetverbinding is nodig voor de actuele temperatuur.
