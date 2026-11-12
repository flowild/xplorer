# Xplorer – Projektstand und nächste Schritte

Stand: 12. November 2026. Die App ist ein früher Windows-Prototyp, keine fertige oder veröffentlichte Anwendung. Maßgebliche Version: `Xplorer.csproj` (aktuell 0.5.0). Die Benutzeroberfläche ist Englisch.

## Vorhanden

- WPF-App mit Ordnernavigation, Pfadeingabe, Breadcrumbs, Laufwerksübersicht, Seitenleiste, Tabs mit eigenem Verlauf, Sortierung und Mehrfachauswahl.
- Favoriten mit lokaler Speicherung, Größenberechnung im Hintergrund und lokale Suche im gewählten Ordner einschließlich Unterordnern. Die Suche zeigt Quelle und Bereich, ist abbrechbar und auf 1.000 Treffer begrenzt.
- Erstellen, Umbenennen, Kopieren, Ausschneiden, Einfügen und eingeschränktes Löschen über den Papierkorb. Windows-Dialoge werden für Dateioperationen verwendet. Rechtsklickmenüs bieten die Aktionen an. Ordnerinhalte werden bei Dateisystemereignissen aktualisiert; F5 aktualisiert manuell.
- Eine erste, kompakte Tableiste: `+` für einen neuen Tab, mittlere Maustaste zum Schließen; der letzte Tab bleibt offen.
- Ein separates experimentelles WinUI-3-Projekt unter `WinUI/` setzt Navigation, Tabs, Favoriten, Größen, lokale Suche und erste Dateiaktionen mit gemeinsam genutzter Dateisystemlogik um. Es ersetzt WPF noch nicht und muss auf Funktionsgleichheit und sichere Dateioperationen interaktiv geprüft werden.
- Die Dateien `Start-Xplorer-WPF.cmd` und `Start-Xplorer-WinUI.cmd` bauen und starten die jeweilige Anwendung per Doppelklick.

Diese Funktionen sind teilweise erprobt, aber nicht vollständig interaktiv oder unter allen Windows-Versionen getestet. Ordnergrößen mit ausgelassenen Links sind gekennzeichnet; die Suche folgt verknüpften Unterordnern nicht. Papierkorblöschen ist derzeit auf lokale feste Laufwerke beschränkt und lehnt umgeleitete Einträge ab.

## Nächste Schritte

Für die Featureplanung und Bedienabläufe dient Files als wichtigste Referenz. Vor der Umsetzung die jeweilige Funktion und ihr Verhalten vergleichen und geeignete vorhandene Bausteine einschließlich Lizenz und Abhängigkeiten prüfen. Die folgenden Punkte bleiben offene Arbeit; der Vergleich ist noch nicht erfolgt.

1. **Grundfunktionen prüfen und stabilisieren.** Auf Windows 10 und 11 mit temporären Testdaten die neuen Rechtsklickmenüs, Favoriten nach Neustart, Suche, Tabs und Wiederherstellung aus dem Papierkorb bei geöffnetem Ordner interaktiv prüfen. Dateioperationen einschließlich mehrerer Einträge, Konflikten, Abbruch, teilweisen Ergebnissen, fehlenden Rechten und Explorer-Zwischenablage erneut prüfen. Fehler beheben und Testergebnisse festhalten.
2. **Dateisystem und Bedienbarkeit härten.** Große Ordner, langsame Netzlaufwerke, getrennte Laufwerke, lange Pfade, Verknüpfungen und Berechtigungsfehler messen und testen. Vorhandene vollständige Ordnerauflistung und automatische Größenberechnung hinsichtlich Reaktionsfähigkeit verbessern. Drag-and-drop, Windows-Shell-Kontextmenüs, Dateisymbole und weitere Tastenkürzel anhand konkreter Tests ergänzen.
3. **WinUI 3 vervollständigen und praktisch prüfen.** Die Übertragung der WPF-Funktionen in die gewählte Zieltechnologie vervollständigen und interaktiv mit temporären Testdaten auf Funktionsgleichheit prüfen, insbesondere Zwischenablage, Papierkorb, Konflikte, Abbruch, Tastatur und mittlere Maustaste. Verhalten, Leistung, Barrierefreiheit, Shell-Anbindung, Ressourcen/Verteilung und Unterstützung der konkreten Windows-10/11-Versionen prüfen; siehe [UI-DECISION.md](UI-DECISION.md).
4. **Oberfläche konsistent weiterentwickeln.** Tableiste, Seitenleiste, Symbole, Abstände und Zustände verbessern; Tastaturbedienung und Barrierefreiheit berücksichtigen. Nutzerwünsche zur Bedienung beibehalten.
5. **Everything optional integrieren.** Offizielles SDK/IPC als austauschbaren lokalen Suchanbieter prüfen und gegen eine verfügbare Everything-Instanz testen. Status, Suchquelle und Suchbereich anzeigen; lokale Suche als unabhängigen Fallback erhalten. Keine Installation oder Dienstkonfiguration ohne Auftrag; Lizenz und Weitergabe vor dem Mitliefern konkreter Dateien klären.
6. **Danach erweitern.** Zwei unabhängige Panels, Vorschau, Befehlspalette, Warteschlange, Stapelumbenennung mit Vorschau und Sitzungswiederherstellung nach Bedarf priorisieren. Archive, Tags, Dateivergleich und eigene Indizierung später anhand konkreter Anforderungen bewerten.

## Gewünschte Features nach dem Vorbild von File Pilot

Stand der Recherche: 12. November 2026. Die folgenden Wünsche orientieren sich an den Herstellerangaben zu [File Pilot](https://filepilot.tech/) und seiner [Roadmap](https://filepilot.tech/roadmap); sie sind keine Zusage zur unveränderten Übernahme oder zur Reihenfolge. File Pilot ist proprietär: Funktionen und Bedienkonzepte dienen als Inspiration, geschützter Code und Assets werden nicht übernommen.

- **Flexible Panels und Tabs:** unabhängige Panels mit Tabs, anpassbarer Anordnung und Drag-and-drop; Layouts speichern und wiederherstellen. Tabs sind im Prototyp vorhanden, Panels und Layouts sind offen.
- **Schnelle Suche und Filter:** verschachtelte Ordner und Laufwerke durchsuchen, unscharfe Suche und Filter nach Dateiendung prüfen. Die lokale Ordnersuche ist vorhanden; Everything als optionaler Suchanbieter bleibt geplant. Leistung und Suchbereich transparent machen.
- **Vorschau:** Inhalte wie Texte und Bilder sowie Ordner schnell ansehen, ohne die Anwendung zu verlassen. Geplant.
- **Stapelumbenennung:** mehrere Einträge interaktiv mit Vorschau umbenennen; Regeln wie Datum und eindeutige Kennungen prüfen. Geplant.
- **Schnellnavigation:** Systempfade und zuletzt besuchte Ordner über eine Suche mit Autovervollständigung öffnen. Ein Tab-Verlauf ist vorhanden; eine entsprechende Schnellnavigation ist offen.
- **Befehlspalette und Tastatur:** Aktionen suchen und Tastenkürzel anpassen; Tastensequenzen und Aliase auf Eignung prüfen. Befehlspalette ist geplant, erweiterte Anpassung offen.
- **Windows-Kontextmenüs:** System- und Drittanbieteraktionen zugänglich machen; Suche und Anheften häufig verwendeter Einträge prüfen. Eigene Grundaktionen sind vorhanden, Shell-Kontextmenüs offen.
- **Anpassbare Oberfläche und Ansichten:** Themes, Schriftgröße, Abstände und Animationen sowie Baumansicht, Spaltenansicht, Gruppierung und vererbbare Ordnereinstellungen nach Stabilisierung der Grundfunktionen bewerten.

Dateioperationen, Tastaturbedienung, Barrierefreiheit und Reaktionsfähigkeit bleiben vorrangig. Die Herstellerangaben zu File Pilots Geschwindigkeit wurden nicht unabhängig gemessen; Xplorer benötigt eigene Messungen. Rückgängig/Wiederholen, MTP, virtuelle Ordner, Cloud-Status und direkte NTFS-Indizierung standen auf der File-Pilot-Roadmap noch aus und werden für Xplorer bei Bedarf gesondert bewertet.

## Frühzeitig zu klärende technische Entscheidungen

- **Dateioperationen:** Die endgültige technische Umsetzung wird auf Nutzerwunsch entschieden, sobald Funktions- und Laufzeittests eine ausreichende Grundlage bieten. Konflikte, Abbruch und teilweise Ergebnisse mit temporären Daten prüfen; die bestehende Umsetzung dient bis dahin als Ausgangspunkt.
- **WinUI und Verteilung:** WinUI 3 ist auf Nutzerwunsch als Zieltechnologie festgelegt. Die Fassung bleibt bis zur Prüfung von Ressourcen, Start, Verteilung, Barrierefreiheit und Leistung ein Prototyp. Paketierung und Veröffentlichungsweg sind offen; der aktuelle Ressourcen-Workaround ist keine Freigabe.
- **Windows-Unterstützung:** Windows 10 und 11 bleiben Ziel. Eine konkrete Mindestversion von Windows 10 sowie die Kombination aus SDK, Laufzeit und Paketierung vor Veröffentlichung überprüfen und festlegen; die Projektangabe allein ist kein Kompatibilitätsnachweis.
- **Grenzen zwischen Komponenten:** Dateisystem, Shell, Suche und Zustandsverwaltung von der Oberfläche getrennt halten. Everything bleibt ein optionaler Suchanbieter; eine lokale Alternative muss unabhängig funktionieren. Schnittstellen und Migrationsschritte bei Bedarf anhand konkreter Funktionen festlegen.
- **Leistung und Tests:** Messbare Szenarien und reproduzierbare Testdaten weiter ausbauen, bevor Leistungsziele oder Gleichwertigkeit mit anderen Produkten zugesagt werden. Die derzeitigen Tests decken nicht alle Windows- und UI-Fälle ab.

Der Nutzer hat Windows 10 und 11 vorerst als Zielsysteme festgelegt und wünscht, Architekturentscheidungen von der gemessenen Geschwindigkeit abhängig zu machen. Dafür vergleichbare Szenarien, Datensätze und Umgebungen dokumentieren; Reaktionsfähigkeit und Korrektheit neben der reinen Laufzeit prüfen. Die exakten Mindestversionen und konkrete Architekturentscheidungen bleiben bis zu den jeweiligen Prüfungen offen.

## Letzter bekannter Prüfstand

- Build des WPF-Prototyps mit .NET 10 erfolgreich, zuletzt ohne Warnungen und Fehler.
- Temporäre Tests prüften unter anderem Favoritenspeicherung, lokale Suche und Abbruch, Kopieren und Verschieben, Konfliktschutz sowie eine Dateisystem-Benachrichtigung. Sie ersetzen keine interaktiven End-to-End-Tests.
- Die NuGet-Quelle war korrekt; die ursprünglichen Fehler entstanden durch die eingeschränkte Netzwerkumgebung des Agenten. Mit freigegebenem Netzwerk wurden die WinUI-Vorlage und Pakete lokal geladen und ein separates leeres WinUI-Testprojekt für win-x64 erfolgreich gebaut. Der gepackte Start benötigt den Windows-Entwicklermodus. Beim ungepackten Start fehlte zunächst eine Theme-Ressource; nach einer vorläufigen Korrektur im temporären Build blieb der Testprozess versteckt drei Sekunden aktiv. Oberfläche und Ressourcen sind noch nicht geprüft; es gibt keinen Navigationsvergleich.
- Das eigenständige WinUI-Projekt mit ersten Dateiaktionen wurde für win-x64 gebaut; die Oberfläche und insbesondere die Dateiaktionen sind noch nicht interaktiv abgenommen. Der Ressourcen-Workaround ist experimentell und keine geprüfte Veröffentlichungsmethode.
- WinUI-Tabs zeigen bei mehreren offenen Tabs eine Schließen-Schaltfläche; der letzte Tab bleibt offen. Bei Navigation über den Verlauf wird die Auswahl nur beim erneuten Laden desselben Ordners beibehalten. Der win-x64-Build dieser Änderungen war ohne Warnungen und Fehler erfolgreich; Schließen per Schaltfläche und mittlerer Maustaste sowie das Auswahlverhalten müssen noch interaktiv geprüft werden.
- In WinUI öffnet Enter den ausgewählten Eintrag; Strg+W aktualisiert beim Schließen die Tab-Schaltflächen. Beide Tastaturabläufe müssen noch interaktiv geprüft werden.
- WinUI bietet Strg+L für die Adresse, Strg+F für die Suche, Alt+Pfeiltasten für Verlauf und übergeordneten Ordner, Enter zum Suchen und Escape zum Abbrechen einer laufenden Suche. „Clear“ kehrt zur Ordneransicht zurück; die Adressleiste passt sich der Fensterbreite an. Diese Änderungen sind noch interaktiv zu prüfen.
- `NuGet.Config` konfiguriert nuget.org für das Repository und ersetzt geerbte Paketquellen. Die Quellenauflistung und der WinUI-Restore ohne Quellenargument waren erfolgreich; dabei waren die benötigten Pakete bereits lokal vorhanden. Ein Download kann in eingeschränkten Umgebungen Netzwerkfreigabe benötigen.
- Ein neuer Prüflauf für gemeinsame Dateisystemlogik bestand sechs Funktionsprüfungen mit temporären Daten; ein einfacher Benchmark für 2.000 Dateien wurde ausgeführt. WPF baute ohne Warnungen und Fehler. Ein WinUI-Smoke-Test startete die App und navigierte per Windows UI Automation zu einer temporären Datei. Vorgehen, Messwerte und Grenzen stehen in [TESTING.md](TESTING.md). Umfangreichere UI-Tests und Leistungsmessungen auf Windows 10 und 11 bleiben offen.
- Ein erneuter Lauf bestand alle sechs Funktionsprüfungen und den WinUI-Smoke-Test; der WinUI-x64-Build war warnungs- und fehlerfrei. Ein fehlender WinUI-Build wurde im Smoke-Test mit einem Fehlercode erkannt. Weitere Fehler- und Cleanup-Pfade der Testsuite sind nicht gezielt geprüft.
- Der WinUI-Smoke-Test wurde um lokale Suche, Rückkehr zur Ordneransicht sowie Öffnen und Schließen eines Tabs einschließlich Schutz des letzten Tabs erweitert. Alle fünf UI-Prüfungen bestanden auf Windows-Build 26300; Tastenkürzel, Tabverlauf und Suchabbruch bleiben offen.
- WinUI bietet eine sichtbare Befehlsleiste für Erstellen, Kopieren, Ausschneiden, Einfügen, Umbenennen und Löschen sowie Strg+Umschalt+N für neue Ordner. Die Befehle verwenden die vorhandenen Dateiaktionen; das Tastenkürzel und native Dialoge sind noch nicht interaktiv geprüft.
- Die automatisierten Prüfungen wurden um Favoritenspeicherung, Breadcrumbs und Sortierung sowie WinUI-Dialoge zum Erstellen und Umbenennen ergänzt. Acht Logikprüfungen und neun UI-Prüfungen bestanden; Kopieren/Verschieben über Zwischenablage und native Dialoge, Papierkorb, Tastenkürzel und weitere Systemfälle bleiben offen. Details in [TESTING.md](TESTING.md).
- Drei weitere Logikprüfungen bestanden für Ordnerkopie mit Konflikt, Verschiebekonflikte und Schutz von Laufwerkswurzeln. Diese Prüfungen verwenden keine nativen Dialoge und decken keine mehrteiligen Teilergebnisse ab.
- Ressourcenuntersuchung: Ein isolierter WinUI-Build ohne Kopierregel startete auf Windows-Build 26300; alle neun UI-Smoke-Prüfungen bestanden. Der frühere Fehler ließ sich dort nicht reproduzieren. Die Kopierregel bleibt bis zu Tests des Verteilungswegs auf Windows 10 und 11 bestehen; sichtbare Darstellung, Veröffentlichung und ursprüngliche Fehlerursache sind offen. Einzelheiten und Build-Warnungen siehe [UI-DECISION.md](UI-DECISION.md).
- Netzwerkprüfung: nuget.org war innerhalb der eingeschränkten Agentenumgebung nicht erreichbar, außerhalb jedoch schon. Ein erzwungener WinUI-Restore ohne Cache lief mit Netzwerkfreigabe warnungsfrei; die Sandbox-Netzwerkregel wurde nicht dauerhaft geändert.
- Auf Nutzerwunsch wurde lokal in der Codex-Konfiguration `sandbox_workspace_write.network_access = true` gesetzt. In der laufenden Sitzung blieb nuget.org in der Sandbox unerreichbar. Nach Neustart einer Sitzung erneut testen; ein verwaltetes Berechtigungsprofil kann die Einstellung übersteuern. Keine Zugangsdaten dokumentieren.
- Die WinUI-Oberfläche bündelt Breadcrumbs und Pfadeingabe, bietet kompakte Aktions- und Einstellungsmenüs und eine verstellbare Seitenleiste. Ordnergrößeneinstellung und Seitenleistenbreite werden gespeichert. Weitere Windows-, Theme- und Bedienhilfentests bleiben offen.
- Everything ist noch nicht angebunden; die Suche nutzt ausschließlich das lokale Dateisystem.

Diese Datei nach relevanten Änderungen aktualisieren und offene oder nicht überprüfte Punkte ausdrücklich kennzeichnen.
