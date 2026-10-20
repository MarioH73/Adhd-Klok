

# <ins>Desktop Clock</ins>

Een bureaubladklok voor Windows, gemaakt met C# en .NET 8 WinForms.

Het klokje toont de huidige tijd, datum, een afteltimer en het actuele weer.

Deze applicatie is speciaal ontwikkeld voor developers, studenten en iedereen die zo opgaat in coderen, leren, huiswerk maken of andere werkzaamheden dat een pauze nemen er vaak bij inschiet.

De klok fungeert als een persoonlijke hulpmiddel om een gezonde balans tussen werken en rusten te behouden. Je kunt een werktimer instellen die aftelt naar je volgende pauze. Daarnaast kun je zelf bepalen hoe lang de pauze duurt.

Voor extra motivatie kun je ervoor kiezen om tijdens de pauze het scherm automatisch uit te laten schakelen. Gedurende deze ingestelde pauzetijd reageert de computer niet op muis- of toetsenbordinvoer, zodat je echt even afstand neemt van je werk.

Gebruik die tijd bijvoorbeeld om:

Een kop koffie of thee te drinken ☕
Iets te eten te pakken 🥪
Even naar buiten te gaan 🚶
Je partner een knuffel te geven ❤️
Je huisdier wat aandacht te geven 😺
Of simpelweg iets anders te doen dan naar een scherm te staren

#  Advies

Begin rustig met een werkperiode van 30 minuten, gevolgd door 15 minuten pauze. Zo kun je wennen aan het ritme.

Wanneer dit goed bevalt, kun je de werktijd eventueel uitbreiden naar maximaal 60 minuten, met daarna 15 minuten pauze. Persoonlijk ervaar ik deze verhouding als een prettige en gezonde balans tussen concentratie en ontspanning.


# _Screenshot_


![Donker Thema](Adhdklok_C/Media/Images/ScreenShots/Vast_Dark.png)     ![Licht Thema](Adhdklok_C/Media/Images/ScreenShots/Vast_Light.png)


# Starten

Benodigd: de .NET 8 SDK en Windows.

dotnet run --project AdhdKlok.csproj

## Functies

-  Tijd- en datumweergave
   
-  Hert menu zit tussen de 4 en de 5
  
-  Instelbare Afteltimer en Pauzetijd met kiezen van Vier alarmgeluiden

-  Ge keuze om tijdens de pauze het scherm uit te schakelen Zo lang als de ingestelde Pauze
   let er wel op Het schermj reageerd pas op muis en toetsenbord als dew pauze voorbij is !

-  Actueel het Weer en Tempratuur via Open-Meteo

-  Donker en licht thema

-  Vast zetten op het bureaublad

-  Opstarten met Windows  

-  Uitschuifbaar instellingenmenu met animaties

-  Altijd zichtbaar op het bureaublad


# Werking

klok is randloos. 
Sleep het venster om het te verplaatsen en klik op het instellingen-icoon in het berichtenpaneel onderaan om het uitklapmenu te openen.

Het icoon draait kort en het menu klapt na een korte vertraging onder het icoon open.
 Klik nogmaals op het icoon om het menu omhoog te laten inklappen.

Via Weergave kies je het donkere thema of Licht (Windows). 
De keuze wordt bewaard in Data/theme.txt.

Onder Timer > Alarmgeluid kun je kiezen uit vier WAV-bestanden. 
De geselecteerde toon wordt gebruikt wanneer de timer afloopt.

De vensterpositie wordt opgeslagen in Data/position.txt.

Weerinformatie

Het weer wordt opgehaald via Open-Meteo en vereist geen API-sleutel of extra NuGet-pakketten.


# _Een internetverbinding is nodig voor de actuele temperatuur._

# Licentie

_Dit project valt onder de MIT License.     Copyright (c) 2026 Mario Hägele_(R)

