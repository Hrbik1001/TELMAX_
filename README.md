# PID Mobile Speaker

První mobilní verze pro Android. Běží kompletně v mobilu, bez PC.

## Co umí

- načíst ZIP s `data/` a `Audio/`
- načíst PID data
- vybrat linku/spoj
- sledovat GPS
- držet aplikaci na šířku
- automaticky hlásit podle dvou polí okolo aktuální zastávky:
  - vstup do **50 m** pole = `GONG + aktuální zastávka`
  - opuštění **80 m** pole = posun na další zastávku + `GONG_b + Příští zastávka + název`
- ruční tlačítka:
  - Hlášení zastávky
  - Příští zastávka
  - zpět/vpřed
  - start/stop GPS

## ZIP pro import do mobilu

Aplikace čeká ZIP, ve kterém je ideálně:

```text
data/
  PID.zip
  StopsByName.xml
  phrases.json
Audio/
  System/
  Zastavky/
```

Může to být i ZIP celé složky desktopového PIDSpeakeru, aplikace se pokusí vytáhnout `data` a `Audio`.

## Build APK

Ve Visual Studiu s MAUI workloadem:

```powershell
dotnet build -f net8.0-android -c Release
```

Nebo přes Visual Studio: otevřít `PIDMobileSpeaker.csproj`, vybrat Android zařízení/emulátor, spustit.

## Poznámka

Import GTFS/PID dat přímo v mobilu může chvíli trvat. Telefon není serverovna, i když si to někdy výrobci reklam myslí.
