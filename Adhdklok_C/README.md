
# <ins>Adhd-Klok</ins>


Een klein bureaubladklokje voor Windows, 

gemaakt met C# en .NET 8 WinForms. 

Het klokje toont de tijd, datum, een afteltimer en het actuele weer.


# _Screenshot_

![Donker Thema](Adhdklok_C/Media/Images/ScreenShots/Vast_Dark.png)     ![Licht Thema](Adhdklok_C/Media/Images/ScreenShots/Vast_Light.png)



# Starten

Benodigd: de .NET 8 SDK en Windows.

dotnet run --project AdhdKlok.csproj

## Functies

-  Tijd- en datumweergave

-  Afteltimer met alarmgeluiden

-  Actueel weer via Open-Meteo

-  Donker en licht thema

-  Opslaan van vensterpositie

-  Uitschuifbaar instellingenmenu met animaties

-  Altijd zichtbaar op het bureaublad


# Werking


klok is randloos. Sleep het venster om het te verplaatsen en klik op het instellingen-icoon in het berichtenpaneel onderaan om het uitklapmenu te openen.

Het icoon draait kort en het menu klapt na een korte vertraging onder het icoon open. Klik nogmaals op het icoon om het menu omhoog te laten inklappen.

Via Weergave kies je het donkere thema of Licht (Windows). De keuze wordt bewaard in Data/theme.txt.

Onder Timer > Alarmgeluid kun je kiezen uit vier WAV-bestanden. De geselecteerde toon wordt gebruikt wanneer de timer afloopt.

De vensterpositie wordt opgeslagen in Data/position.txt.

Weerinformatie

Het weer wordt opgehaald via Open-Meteo en vereist geen API-sleutel of extra NuGet-pakketten.


# _Een internetverbinding is nodig voor de actuele temperatuur._

# Licentie

_Dit project valt onder de MIT License.     Copyright (c) 2026 Mario Hägele_
