# Xplorer – Projektstand und nächste Schritte

Stand: 12. November 2026. Die App ist ein früher Windows-Prototyp, keine fertige oder veröffentlichte Anwendung. Maßgebliche Version: `Xplorer.csproj` (aktuell 0.1.0). Die Benutzeroberfläche ist Englisch.

## Vorhanden

- WPF-App mit Ordnernavigation, Pfadeingabe, Breadcrumbs, Laufwerksübersicht, Seitenleiste, Tabs mit eigenem Verlauf, Sortierung und Mehrfachauswahl.
- Favoriten mit lokaler Speicherung, Größenberechnung im Hintergrund und lokale Suche im gewählten Ordner einschließlich Unterordnern. Die Suche zeigt Quelle und Bereich, ist abbrechbar und auf 1.000 Treffer begrenzt.
- Erstellen, Umbenennen, Kopieren, Ausschneiden, Einfügen und eingeschränktes Löschen über den Papierkorb. Windows-Dialoge werden für Dateioperationen verwendet. Rechtsklickmenüs bieten die Aktionen an. Ordnerinhalte werden bei Dateisystemereignissen aktualisiert; F5 aktualisiert manuell.
- Eine erste, kompakte Tableiste: `+` für einen neuen Tab, mittlere Maustaste zum Schließen; der letzte Tab bleibt offen.
- Ein separates experimentelles WinUI-3-Projekt unter `WinUI/` setzt Navigation, Tabs, Favoriten, Größen, lokale Suche und erste Dateiaktionen mit gemeinsam genutzter Dateisystemlogik um. Es ersetzt WPF noch nicht und muss auf Funktionsgleichheit und sichere Dateioperationen interaktiv geprüft werden.
- Die Dateien `Start-Xplorer-WPF.cmd` und `Start-Xplorer-WinUI.cmd` bauen und starten die jeweilige Anwendung per Doppelklick.

Diese Funktionen sind teilweise erprobt, aber nicht vollständig interaktiv oder unter allen Windows-Versionen getestet. Ordnergrößen mit ausgelassenen Links sind gekennzeichnet; die Suche folgt verknüpften Unterordnern nicht. Papierkorblöschen ist derzeit auf lokale feste Laufwerke beschränkt und lehnt umgeleitete Einträge ab.

## Nächste Schritte

1. **Grundfunktionen prüfen und stabilisieren.** Auf Windows 10 und 11 mit temporären Testdaten die neuen Rechtsklickmenüs, Favoriten nach Neustart, Suche, Tabs und Wiederherstellung aus dem Papierkorb bei geöffnetem Ordner interaktiv prüfen. Dateioperationen einschließlich mehrerer Einträge, Konflikten, Abbruch, teilweisen Ergebnissen, fehlenden Rechten und Explorer-Zwischenablage erneut prüfen. Fehler beheben und Testergebnisse festhalten.
2. **Dateisystem und Bedienbarkeit härten.** Große Ordner, langsame Netzlaufwerke, getrennte Laufwerke, lange Pfade, Verknüpfungen und Berechtigungsfehler messen und testen. Vorhandene vollständige Ordnerauflistung und automatische Größenberechnung hinsichtlich Reaktionsfähigkeit verbessern. Drag-and-drop, Windows-Shell-Kontextmenüs, Dateisymbole und weitere Tastenkürzel anhand konkreter Tests ergänzen.
3. **WinUI 3 vervollständigen und praktisch prüfen.** Die gewünschte Übertragung der WPF-Funktionen im separaten Projekt vervollständigen und interaktiv mit temporären Testdaten auf Funktionsgleichheit prüfen, insbesondere Zwischenablage, Papierkorb, Konflikte, Abbruch, Tastatur und mittlere Maustaste. Verhalten, Leistung, Barrierefreiheit, Shell-Anbindung, Ressourcen/Verteilung und Unterstützung der konkreten Windows-10/11-Versionen mit WPF vergleichen; siehe [UI-DECISION.md](UI-DECISION.md).
4. **Oberfläche konsistent weiterentwickeln.** Tableiste, Seitenleiste, Symbole, Abstände und Zustände nach dem Technologieentscheid verbessern; Tastaturbedienung und Barrierefreiheit berücksichtigen. Nutzerwünsche zur Bedienung beibehalten.
5. **Everything optional integrieren.** Offizielles SDK/IPC als austauschbaren lokalen Suchanbieter prüfen und gegen eine verfügbare Everything-Instanz testen. Status, Suchquelle und Suchbereich anzeigen; lokale Suche als unabhängigen Fallback erhalten. Keine Installation oder Dienstkonfiguration ohne Auftrag; Lizenz und Weitergabe vor dem Mitliefern konkreter Dateien klären.
6. **Danach erweitern.** Zwei unabhängige Panels, Vorschau, Befehlspalette, Warteschlange, Stapelumbenennung mit Vorschau und Sitzungswiederherstellung nach Bedarf priorisieren. Archive, Tags, Dateivergleich und eigene Indizierung später anhand konkreter Anforderungen bewerten.

## Letzter bekannter Prüfstand

- Build des WPF-Prototyps mit .NET 10 erfolgreich, zuletzt ohne Warnungen und Fehler.
- Temporäre Tests prüften unter anderem Favoritenspeicherung, lokale Suche und Abbruch, Kopieren und Verschieben, Konfliktschutz sowie eine Dateisystem-Benachrichtigung. Sie ersetzen keine interaktiven End-to-End-Tests.
- Die NuGet-Quelle war korrekt; die ursprünglichen Fehler entstanden durch die eingeschränkte Netzwerkumgebung des Agenten. Mit freigegebenem Netzwerk wurden die WinUI-Vorlage und Pakete lokal geladen und ein separates leeres WinUI-Testprojekt für win-x64 erfolgreich gebaut. Der gepackte Start benötigt den Windows-Entwicklermodus. Beim ungepackten Start fehlte zunächst eine Theme-Ressource; nach einer vorläufigen Korrektur im temporären Build blieb der Testprozess versteckt drei Sekunden aktiv. Oberfläche und Ressourcen sind noch nicht geprüft; es gibt keinen Navigationsvergleich.
- Das eigenständige WinUI-Projekt mit ersten Dateiaktionen wurde für win-x64 gebaut; die Oberfläche und insbesondere die Dateiaktionen sind noch nicht interaktiv abgenommen. Der Ressourcen-Workaround ist experimentell und keine geprüfte Veröffentlichungsmethode.
- Everything ist noch nicht angebunden; die Suche nutzt ausschließlich das lokale Dateisystem.

Diese Datei nach relevanten Änderungen aktualisieren und offene oder nicht überprüfte Punkte ausdrücklich kennzeichnen.
