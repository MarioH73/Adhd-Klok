<<<<<<< HEAD
<<<<<<< HEAD
=======

>>>>>>> 2bd3a78ac91c2ec69925718e91013441d6ed992a
=======
>>>>>>> e54f4fc7acbe63cc81b7a64c2b9f4a5aacb30b9c
Adhd-Klok


Een klein bureaubladklokje voor Windows, 

gemaakt met C# en .NET 8 WinForms. 

Het klokje toont de tijd, datum, een afteltimer en het actuele weer.


Screenshot
<<<<<<< HEAD
![Donker Thema](Media/Images/ScreenShots/Dark.png)             
![Licht Thema](Media/Images/ScreenShots/Light.png)


=======



<<<<<<< HEAD
>>>>>>> 2bd3a78ac91c2ec69925718e91013441d6ed992a
Starten
Benodigd: de .NET 8 SDK en Windows.
=======
>>>>>>> e54f4fc7acbe63cc81b7a64c2b9f4a5aacb30b9c


Starten

Benodigd: de .NET 8 SDK en Windows.
dotnet run --project AdhdKlok.csproj

Functies

-*-  Tijd- en datumweergave

-*-  Afteltimer met alarmgeluiden

-*-  Actueel weer via Open-Meteo

-*-  Donker en licht thema

-*-  Opslaan van vensterpositie

-*-  Uitschuifbaar instellingenmenu met animaties

-*-  Altijd zichtbaar op het bureaublad


Werking


klok is randloos. Sleep het venster om het te verplaatsen en klik op het instellingen-icoon in het berichtenpaneel onderaan om het uitklapmenu te openen.

Het icoon draait kort en het menu klapt na een korte vertraging onder het icoon open. Klik nogmaals op het icoon om het menu omhoog te laten inklappen.

Via Weergave kies je het donkere thema of Licht (Windows). De keuze wordt bewaard in Data/theme.txt.

Onder Timer > Alarmgeluid kun je kiezen uit vier WAV-bestanden. De geselecteerde toon wordt gebruikt wanneer de timer afloopt.

De vensterpositie wordt opgeslagen in Data/position.txt.

Weerinformatie
Het weer wordt opgehaald via Open-Meteo en vereist geen API-sleutel of extra NuGet-pakketten.

<<<<<<< HEAD
Een internetverbinding is nodig voor de actuele temperatuur.
=======
Een internetverbinding is nodig voor de actuele temperatuur..
>>>>>>> 2bd3a78ac91c2ec69925718e91013441d6ed992a
