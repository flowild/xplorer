# Xplorer – Hinweise für Agenten

## Ziel

Xplorer soll ein schneller, zuverlässiger und moderner Dateimanager für Windows 10 und 11 werden. Orientierung bieten die Oberfläche von [Files](https://github.com/files-community/Files), die schnelle Navigation und Tastaturbedienung von [File Pilot](https://filepilot.tech/) sowie die Dateisuche von [Everything](https://www.voidtools.com/). Der Windows-Explorer muss nicht deinstalliert oder ungefragt als Standard ersetzt werden.

Das Repository begann ohne Anwendungscode. Architekturentscheidungen sind Vorschläge und anhand eines lauffähigen Prototyps zu überprüfen. Anforderungen des Nutzers und bestehender Projektcode haben Vorrang vor diesen Vorschlägen.

## Projektstand und Planung aktuell halten

Diese `AGENTS.md` bei jeder relevanten Änderung an Anforderungen, Projektstruktur, Architekturentscheidungen, Arbeitsabläufen oder Werkzeugen prüfen und unmittelbar aktualisieren, damit sie jederzeit dem tatsächlichen Projektstand entspricht. Bestätigte Nutzervorgaben aufnehmen; Erledigtes und Geplantes nicht verwechseln. Keine Zugangsdaten oder persönlichen Daten dokumentieren.

Die laufende Planung und den überprüften Projektstand in [docs/PLAN.md](docs/PLAN.md) pflegen. Nach relevanten Änderungen erledigte, offene und blockierte Schritte sowie Prüfergebnisse und Einschränkungen aktualisieren. [README.md](README.md) und [docs/UI-DECISION.md](docs/UI-DECISION.md) bei Bedarf konsistent halten.

Aktueller Ausgangspunkt: WPF-Prototyp mit .NET 10 und eine WinUI-3-Fassung unter `WinUI/`; WPF-Oberfläche liegt in `src/Wpf/`, gemeinsam genutzte Logik in `src/Shared/`. Der Nutzer hat WinUI 3 als Zieltechnologie festgelegt; die Fassung ist noch nicht vollständig geprüft oder veröffentlichungsreif. Die Startdateien `Start-Xplorer-WPF.cmd` und `Start-Xplorer-WinUI.cmd` bauen und öffnen die jeweilige Fassung. Die maßgebliche Version steht in `Xplorer.csproj`. Einzelheiten und nächste Schritte stehen in `docs/PLAN.md`.

`NuGet.Config` legt nuget.org als Paketquelle für das Repository fest und überschreibt dafür geerbte Paketquellen. Der Restore benötigt Zugang zu nuget.org oder bereits lokal vorhandene Pakete.

Der Nutzer hat für Codex `sandbox_workspace_write.network_access = true` in seiner lokalen Konfiguration aktiviert. Nach dem Sitzungsneustart war TCP zu api.nuget.org innerhalb der Sandbox weiterhin nicht erreichbar; ein Restore mit Sicherheitsprüfung gelang über den vorgesehenen Freigabemechanismus. Die genaue Ursache der Einschränkung ist nicht festgestellt.

`tests/Xplorer.Checks/` enthält einen eigenständigen Prüflauf für gemeinsam genutzte Dateisystemlogik und einfache Leistungsmessungen mit temporären Testdaten und wählbarer Dateianzahl; `tests/WinUI-Smoke.ps1` prüft Start, Navigation, Suche, Erstellen, Umbenennen und Tabs per Windows UI Automation; `tests/WinUI-NavigationRace.ps1` prüft schnelle Navigations-, Such- und Tabwechsel als zeitabhängigen Smoke-Test. Ausführung, Prüfumfang, Messbedingungen und offene UI- und Windows-Tests stehen in `docs/TESTING.md`.

## Produkt und Prioritäten

Die Benutzeroberfläche der Anwendung einschließlich Beschriftungen, Status- und Fehlermeldungen muss auf Englisch sein.

Die WinUI-Oberfläche soll sich stärker an Files orientieren: schlicht und übersichtlich, mit leicht erreichbaren Aktionen, einer einzigen Pfaddarstellung, einem erweiterbaren Einstellungsmenü und getrennten, soweit sinnvoll verschiebbaren Bereichen.

1. Zuerst ein stabiles Grundprodukt: Navigation, Breadcrumbs, Tabs, Favoriten, Verlauf, Listen- und Detailansicht, Sortierung und Mehrfachauswahl.
2. Zuverlässige Dateioperationen: Öffnen, Kopieren, Verschieben, Umbenennen, Erstellen und Löschen über den Papierkorb; Konflikte, Fortschritt, Abbruch und Fehler verständlich behandeln. Drag-and-drop, Windows-Tastenkürzel, Dateisymbole und Shell-Kontextmenüs berücksichtigen.
3. Everything optional für schnelle Suche integrieren; ohne verfügbare Everything-Instanz eine verständliche lokale Ordnersuche anbieten. Suchbereich und verwendete Suchquelle anzeigen. Die normale Dateiverwaltung darf nicht von Everything abhängen.
4. Anschließend zwei unabhängige Panels, Vorschau, Befehlspalette, Warteschlange, Stapelumbenennung mit Vorschau und Sitzungswiederherstellung ergänzen. Archivfunktionen, Tags, Dateivergleich und eigene Indizierung später anhand konkreter Bedürfnisse bewerten.

Oberfläche auch bei großen Ordnern und langsamen Netzlaufwerken reaktionsfähig halten: Arbeit im Hintergrund, abgebrochene veraltete Anfragen, virtualisierte Listen und bedarfsweise geladene Symbole und Vorschaudaten. Mit echten Messungen auf Windows 10 und 11 prüfen. Netzwerkpfade, Berechtigungsfehler, getrennte Laufwerke und lange Pfade berücksichtigen. Dateioperationen mit temporären Testdaten prüfen und Datenverlust vermeiden.

## Technische Richtung

- Zieltechnologie: C# und WinUI 3 / Windows App SDK für eine native Windows-Desktopanwendung. Kompatibilität der konkret gewählten SDK- und .NET-Version mit der gewünschten Windows-10-Version vor Festlegung und Veröffentlichung überprüfen.
- Windows 10 und Windows 11 sind vorerst die Zielsysteme; konkrete Mindestversionen müssen vor Veröffentlichung geprüft und festgelegt werden. Architekturentscheidungen anhand reproduzierbarer Messungen der Geschwindigkeit und Reaktionsfähigkeit treffen; korrekte und sichere Dateioperationen bleiben Voraussetzung.
- Oberfläche, Dateisystem und Windows-Shell, Suche sowie Einstellungen und Sitzungszustand klar trennen. Windows-Shell-Schnittstellen nutzen, wo sie Explorer-kompatibles Verhalten ermöglichen.
- Die endgültige Umsetzung der Dateioperationen erst nach ausreichenden Funktions- und Laufzeittests entscheiden; vorhandene Implementierungen dienen bis dahin als Ausgangspunkt.
- Everything über das offizielle [SDK und IPC](https://www.voidtools.com/support/everything/sdk/) als austauschbaren lokalen Suchanbieter anbinden. Es ist ein separates proprietäres Freeware-Programm; die Lite-Version unterstützt das SDK nicht. NTFS-Indizierung und Netzlaufwerke haben eigene Einrichtungsbedingungen. Siehe [FAQ](https://www.voidtools.com/faq/) und [Ordnerindexierung](https://www.voidtools.com/support/everything/folder_indexing/).
- Everything nicht ungefragt installieren, als Administrator starten oder dessen Dienst konfigurieren. Status und Einrichtung dem Nutzer verständlich machen. Vor dem Mitliefern von Everything oder SDK-Dateien deren konkrete Lizenz und Weitergabebedingungen prüfen.

## Open Source und Referenzen

- Das Files-Repository ist lokal unter `C:/Github/Files` im Workspace verfügbar. Für die Featureplanung und Bedienabläufe ausdrücklich an bestehenden Dateimanagern orientieren. Files ist die wichtigste Referenz; gewünschte Funktionen nach dem Vorbild von File Pilot stehen in `docs/PLAN.md`. Vor der Umsetzung konkrete Funktionen und ihr Verhalten vergleichen und für Xplorer passend priorisieren. Bewährte vorhandene Bausteine können nach Prüfung ihrer Eignung und Lizenz verwendet werden.
- [Files](https://github.com/files-community/Files): wichtigste Referenz für moderne Windows-Oberfläche und mögliche C#-Bausteine. Das Repository enthält MIT- und MPL-2.0-Lizenztexte; vor Übernahme die Lizenz der konkreten Dateien und deren Abhängigkeiten klären, nicht automatisch eine freie Lizenzwahl annehmen.
- [Explorer++](https://github.com/derceg/explorerplusplus): schlanker Windows-Dateimanager, GPLv3; interessant für native Windows-Integration.
- [Double Commander](https://github.com/doublecmd/doublecmd): GPLv2, Referenz für zwei Panels, Stapeloperationen und Arbeitsabläufe.
- [Microsoft WinFile](https://github.com/microsoft/winfile): MIT, archiviert; historische Win32-Referenz, keine bevorzugte moderne Grundlage.
- File Pilot dient als Produktinspiration. Keinen proprietären Code oder geschützte Assets übernehmen.

Vor jeder Übernahme Herkunft, Version bzw. Commit, Lizenz und Bedingungen pro Bestandteil prüfen und in einer Third-Party-Notices-Datei dokumentieren. Lizenz- und Copyright-Hinweise erhalten; Copyleft- und Weitergabepflichten vor Integration berücksichtigen. Bloße Inspiration durch Funktionen erlaubt keine Übernahme von Quellcode oder Assets. Bei unklarer Lizenz zunächst keinen Code übernehmen.

## Geeignete Skills

Verfügbare Skills nur einsetzen, wenn sie zur konkreten Aufgabe passen, und ihre jeweiligen `SKILL.md`-Anweisungen beachten:

- `imagegen`: falls eigens erstellte Rastergrafiken oder Illustrationen benötigt werden; für native UI und einfache Vektoren bevorzugt vorhandene UI-Mittel verwenden.
- `openai-docs`: nur für Fragen zu OpenAI-Produkten, APIs oder Codex selbst.
- `skill-creator`: wenn ausdrücklich ein wiederverwendbarer Codex-Skill erstellt oder geändert werden soll.
- `plugin-management:plugin-management`: wenn externe Dienste oder Plugins für die Aufgabe erforderlich sind.
- `sites:sites`: nur für eine ausdrücklich gewünschte Projektwebsite, nicht für die Windows-Desktopanwendung selbst.

Skill-Verfügbarkeit kann sich ändern; keine Skills oder Werkzeuge voraussetzen, die in der jeweiligen Sitzung nicht angeboten werden.

## Versionierung, Commit und Push

Der Nutzer beauftragt den Agenten, die Projektversion eigenständig zu verwalten und bei einem Versionsbump zu committen und zu pushen.

- Sobald Anwendung und Build-System existieren, eine eindeutige maßgebliche Versionsquelle festlegen und alle erforderlichen abgeleiteten Versionsangaben konsistent halten. Semantische Versionierung verwenden: Patch für kompatible Fehlerkorrekturen, Minor für kompatible Funktionen, Major für inkompatible Änderungen. Vor 1.0 die tatsächliche Kompatibilität und Projektkonventionen beachten.
- Bei zusammengehörigen, nutzerrelevanten Änderungen selbst entscheiden, ob ein Versionsbump sinnvoll ist. Reine Dokumentation, Experimente und unvollständige Arbeit erfordern nicht automatisch einen Bump. Keine Version erfinden, bevor ein Projekt und seine Versionsquelle existieren.
- Vor einem Bump Git-Status, aktuellen Branch, Remote und fremde Änderungen prüfen. Nur zur Aufgabe gehörende Änderungen einschließlich Versionsangaben stagen; fremde oder unklare Änderungen nicht einbeziehen. Passende Tests und Build ausführen und Fehler beheben oder Einschränkungen offen melden.
- Den Bump und die zugehörige fertige Änderung mit einer aussagekräftigen Commit-Nachricht committen und den aktuellen Branch auf seinen vorgesehenen Upstream pushen. Gibt es keinen Upstream, den passenden Remote-Branch aus Repository-Kontext ermitteln und bei Bedarf den Upstream setzen. Diese Anweisung autorisiert den normalen Commit und Push bei einem Versionsbump.
- Keine Force-Pushes, History-Rewrites, fremde Änderungen überschreiben oder Tags/Releases veröffentlichen, sofern nicht ausdrücklich beauftragt. Bei fehlendem Remote, fehlenden Rechten, Konflikten oder unklarer Zielbranch nicht raten: Ergebnis und Hindernis melden. Keine Zugangsdaten ausgeben.
- Abschließend Version, Commit, Push-Ziel und ausgeführte Prüfungen nennen; fehlgeschlagene oder nicht mögliche Schritte ausdrücklich benennen.
