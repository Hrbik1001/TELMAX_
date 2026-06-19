# Jak z toho dostat APK

Tady jsou dvě normální možnosti. Slovo „normální“ ber s rezervou, pořád mluvíme o Android buildu.

## Varianta A: GitHub Actions, nejméně bolesti

1. Vytvoř si prázdný GitHub repozitář.
2. Nahraj do něj **obsah složky `PIDMobileSpeaker`**, ne jen ZIP.
3. Otevři záložku **Actions**.
4. Spusť workflow **Build Android APK**.
5. Po doběhnutí otevři běh workflow.
6. Dole v **Artifacts** stáhni `PIDMobileSpeaker-debug-apk`.
7. V ZIPu bude `.apk`.
8. APK pošli/stáhni do mobilu a nainstaluj.

Android může při instalaci chtít povolit instalaci z neznámých zdrojů.

## Varianta B: Lokálně na Windows

Potřebuješ:

- Visual Studio 2022
- workload `.NET Multi-platform App UI development`
- nebo aspoň .NET 8 SDK + Android workload

Pak ve složce projektu spusť:

```powershell
.\build-apk-windows.ps1
```

Hotová APK bude někde v:

```text
bin\Debug\net8.0-android\...
```

## Poznámka

Debug APK není podepsaná produkčním certifikátem, ale pro tvoje testování na mobilu je to přesně to, co potřebuješ. Pro Google Play by to chtělo release signing, ale to zatím fakt není náš problém dne. 
