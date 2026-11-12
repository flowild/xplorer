# Analyse des Files-Repositorys

Stand: 12. November 2026. Grundlage ist der lokale, unveränderte Checkout `C:/Github/Files`, Commit `f6f35e798a81664d73aa0fbfdc6a223bd753909e` vom 11. November 2026. Dies ist eine gezielte statische Untersuchung, keine vollständige Prüfung aller Funktionen, kein Build und kein Leistungs- oder Bedienvergleich. Im Paketmanifest steht für den Entwicklungsstand Version 4.3.3.0.

## Struktur und Verteilung

- `src/Files.App` enthält WinUI-Oberfläche, Aktionen, ViewModels und Services. `Files.App.Controls` enthält gemeinsame Steuerelemente; `Files.Storage` kapselt unter anderem Speicher und Windows-Interop; `Files.Shared` enthält gemeinsam genutzten Code. Ein separater `Files.App.Server` bietet WinRT-Dienste; weitere Projekte betreffen Launcher und Dateidialoge. Diese Grenzen sind eine Referenz, kein Beleg, dass Xplorer dieselbe Mehrprozessarchitektur benötigt.
- [Projektkonfiguration](https://github.com/files-community/Files/blob/f6f35e798a81664d73aa0fbfdc6a223bd753909e/src/Files.App/Files.App.csproj), [Build-Einstellungen](https://github.com/files-community/Files/blob/f6f35e798a81664d73aa0fbfdc6a223bd753909e/Directory.Build.props) und [Paketmanifest](https://github.com/files-community/Files/blob/f6f35e798a81664d73aa0fbfdc6a223bd753909e/src/Files.App/Package.appxmanifest): .NET 10, Windows App SDK 2.6.5, MSIX-Tooling, Bundle-Konfiguration, Projektminimum Windows-Build 19041. Das x64-Publishprofil aktiviert Native AOT und SelfContained. Das belegt keine getestete Kompatibilität von Xplorer und löst sein ungepacktes PRI-Problem nicht; die Paketierungswege unterscheiden sich.
- `Files.Storage` hängt unter anderem von CsWin32, OwlCore.Storage, DiscUtils.Udf, `Files.Shared` und einem Source-Generator ab. Eine Übernahme ist kein isolierter Austausch einer einzelnen Datei.

## Dateiansicht und Suche

- [Win32StorageEnumerator](https://github.com/files-community/Files/blob/f6f35e798a81664d73aa0fbfdc6a223bd753909e/src/Files.App/Utils/Storage/Enumerators/Win32StorageEnumerator.cs) unterstützt CancellationToken und Zwischenlieferungen; der Code sieht eine frühe erste Ausgabe und weitere zeitgesteuerte Gruppen vor. Symbolcache und optionale asynchrone Ordnergrößen sind eingebunden. Die konkreten Zeiten sind Implementierungswerte, keine Leistungszusagen.
- Die [Detailansicht](https://github.com/files-community/Files/blob/f6f35e798a81664d73aa0fbfdc6a223bd753909e/src/Files.App/Views/Layouts/DetailsLayoutPage.xaml) verwendet ListView und ItemsStackPanel; die Rasteransicht GridView. Ob und wie gut ein konkreter Xplorer-Aufbau virtualisiert, muss mit vielen Elementen gemessen werden.
- [FolderSearch](https://github.com/files-community/Files/blob/f6f35e798a81664d73aa0fbfdc6a223bd753909e/src/Files.App/Utils/Storage/Search/FolderSearch.cs) verwendet unter anderem Windows-Suchabfragen mit `UseIndexerWhenAvailable` und Win32-Dateisuche; Ergebnisse werden gruppenweise gemeldet. [ShellViewModel](https://github.com/files-community/Files/blob/f6f35e798a81664d73aa0fbfdc6a223bd753909e/src/Files.App/ViewModels/ShellViewModel.cs) begrenzt die pro Takt ergänzten Suchtreffer auf 500. Bei gezielter Suche nach bekannten Everything-SDK-Bezeichnern wurde keine Integration gefunden; das ist keine vollständige Negativprüfung.
- Für Xplorer zuerst eigene Messszenarien für erste sichtbare Ergebnisse, vollständige Auflistung, Bedienlatenz, Abbruch und Speicherverbrauch bei großen und langsamen Ordnern schaffen. Danach stufenweise Anzeige, begrenzte UI-Aktualisierung und bedarfsweise Metadaten gegen den bestehenden Stand vergleichen; Everything bleibt ein separat geplanter optionaler Anbieter.

## Dateioperationen und Shell

- [WindowsBulkOperations](https://github.com/files-community/Files/blob/f6f35e798a81664d73aa0fbfdc6a223bd753909e/src/Files.Storage/Windows/WindowsBulkOperations.cs) kapselt Windows `IFileOperation` für vorgemerkte Kopier-, Verschiebe-, Lösch-, Erstell- und Umbenennvorgänge. Ein Progress-Sink meldet unter anderem einzelne Ergebnisse und Fortschritt. Die Untersuchung belegt nicht, dass alle Konflikt-, Abbruch- und Teilergebnisfälle korrekt behandelt werden.
- Für Xplorer ist dies ein wichtiger Vergleichskandidat neben der bestehenden Umsetzung. Entscheidung erst nach reproduzierbaren Funktionstests mit temporären Daten und Laufzeitmessungen; insbesondere Dialoge, Abbruch, Fehler und Teilergebnisse prüfen. Shell-Kontextmenüs und native Interop-Grenzen getrennt untersuchen.

## Lizenz und Umfang

Die Datei `WindowsBulkOperations.cs` trägt ausdrücklich `SPDX-License-Identifier: MPL-2.0`; `FolderSearch.cs` und das App-Projekt tragen MIT-Hinweise. Im Repository liegen beide Lizenztexte. Einzelne Dateien und ihre Abhängigkeiten müssen vor jeder konkreten Übernahme hinsichtlich Herkunft, Lizenz und Weitergabepflichten geprüft werden. Diese Untersuchung übernimmt keinen Quellcode und keine Assets. Eine reine Projektdatei-Lizenzangabe gilt nicht automatisch für alle Quelldateien.

## Nächste Vergleichsschritte

1. Publish/Ressourcen von Xplorer weiter separat untersuchen und auf Windows 10 und 11 validieren; Files' MSIX-Konfiguration nur als Vergleich für einen späteren Paketierungsversuch heranziehen.
2. Für große Ordner und langsame Pfade Datensatz, Hardware, Windows-Version, erste Anzeige und Reaktionsfähigkeit dokumentieren; alternative Auflistungsstrategien messen.
3. Dateioperationen und Shell-Kontextmenüs anhand konkreter Bedienabläufe und Fehlerfälle mit temporären Daten vergleichen; Lizenzprüfung vor jeder Codeübernahme.
4. Suche, Tabs, Favoriten und Tastaturabläufe bei Gelegenheit interaktiv vergleichen. Aus statischer Codeanalyse folgt keine Funktionsgleichheit.