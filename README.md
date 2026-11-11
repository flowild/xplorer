# Xplorer

Projektstand und nächste Schritte stehen in [PLAN.md](PLAN.md).

Ein früher Windows-Dateimanager-Prototyp. Er zeigt Ordnerinhalte, öffnet Dateien über Windows und unterstützt Pfadeingabe, übergeordneten Ordner sowie Zurück und Vorwärts. Ordner werden im Hintergrund geladen; eine neue Navigation verwirft das Ergebnis einer älteren Anfrage.

Die Seitenleiste bietet Home, Desktop, Documents und Downloads sowie Laufwerke. „Refresh drives“ aktualisiert die Laufwerksliste nach dem Anschließen oder Trennen von Geräten.

Ordner lassen sich über `+` neben Favorites oder über das Rechtsklickmenü eines Ordners anheften und mit `×` wieder entfernen. Die Favoriten werden lokal unter `%LOCALAPPDATA%\Xplorer\favorites.json` gespeichert.

„Search“ durchsucht Datei- und Ordnernamen im aktuell geöffneten Ordner einschließlich Unterordnern. Quelle und Suchbereich werden angezeigt. Die Suche läuft im Hintergrund, lässt sich abbrechen und folgt keinen verknüpften Unterordnern. Sie zeigt höchstens 1.000 Treffer; unzugängliche Ordner werden gemeldet. Everything ist noch nicht integriert.

Die Breadcrumb-Leiste öffnet übergeordnete Ordner per Klick. Ein Klick auf Name, Type, Size oder Modified sortiert die Liste; ein weiterer Klick kehrt die Richtung um. Ordner stehen vor Dateien, noch unbekannte Größen am Ende ihrer jeweiligen Gruppe. Bei laufender Größenberechnung wird die Größensortierung aktualisiert.

Der geöffnete Ordner wird nach Dateisystemänderungen automatisch aktualisiert. Mit `F5` oder der Aktualisierungsschaltfläche kann die Ansicht manuell neu geladen werden; das ist insbesondere hilfreich, wenn ein Laufwerk keine zuverlässigen Änderungsbenachrichtigungen liefert.

Tabs besitzen einen eigenen Navigationsverlauf. `Ctrl+T` öffnet einen neuen Tab, `Ctrl+W` schließt den aktiven Tab (mindestens einer bleibt offen), `Ctrl+Tab` und `Ctrl+Shift+Tab` wechseln zwischen Tabs. In der Dateiliste lassen sich mehrere Einträge mit Strg oder Umschalt auswählen. Einzelne Dateien und Ordner können erstellt und umbenannt werden. Kopieren, Ausschneiden und Einfügen verwenden die Windows-Zwischenablage; die Windows-Dialoge behandeln dabei Konflikte und Abbruch. Löschen verwendet den Papierkorb und ist vorerst auf lokale feste Laufwerke beschränkt; verknüpfte oder umgeleitete Einträge können darüber noch nicht gelöscht werden. Bei Abbruch oder Fehler können einzelne Einträge bereits bearbeitet sein.

Ordnergrößen werden standardmäßig im Hintergrund berechnet und können über das Kontrollkästchen abgeschaltet werden, etwa bei großen Ordnern oder Netzlaufwerken. Höchstens zwei Berechnungen laufen gleichzeitig. Symbolische Links und Junctions werden nicht verfolgt; ein Stern an der Größe und die Details weisen auf ausgelassene Einträge hin. Andere nicht unterstützte umgeleitete Einträge oder Zugriffsfehler führen zu „Unavailable“; der Tooltip erklärt den Grund. Die angezeigte logische Größe entspricht nicht zwingend dem belegten Speicherplatz. Die Werte sind Momentaufnahmen und werden beim erneuten Öffnen des Ordners neu berechnet.

## Start

Windows 10/11 und das .NET 10 SDK mit Windows-Desktop-Unterstützung sind erforderlich:

```powershell
dotnet run --project Xplorer.csproj
```

Vor einem erneuten Build oder Start ein bereits geöffnetes Xplorer-Fenster schließen. Windows sperrt die laufende `Xplorer.exe`; andernfalls kann der Build mit MSB3026/MSB3027 scheitern. Falls kein Fenster sichtbar ist, den zugehörigen Xplorer-Prozess im Task-Manager beenden und den Befehl erneut ausführen.

Die maßgebliche Projektversion steht in `Xplorer.csproj`. WPF dient zunächst dazu, die Grundfunktionen lokal zu erproben; der Stand der WinUI-3-Prüfung steht in [UI-DECISION.md](UI-DECISION.md). Die Shell-Integration ist noch unvollständig. Große und langsame Ordner werden noch vollständig eingelesen, bevor sie angezeigt werden; die Oberfläche bleibt dabei bedienbar, aber eine blockierte Dateisystemabfrage lässt sich nicht sofort abbrechen. Windows-10/11-Kompatibilität und Leistung müssen auf beiden Systemen noch praktisch geprüft werden.
