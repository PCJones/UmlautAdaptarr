# UmlautAdaptarr

## English description coming soon

## 12.02.2024: Erste Testversion
Wer möchte kann den UmlautAdaptarr jetzt gerne testen! Über Feedback würde ich mich sehr freuen!

Es sollte mit allen *arrs funktionieren, hat aber nur bei Sonarr schon Auswirkungen (abgesehen vom Caching).

Momentan ist docker dafür nötig, wer kein Docker nutzt muss sich noch etwas gedulden. 

docker-compose.yml
```
version: '3.8'
services:
  umlautadaptarr:
    build: https://github.com/PCJones/UmlautAdaptarr.git#master
    image: umlautadaptarr
    restart: unless-stopped
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Europe/Berlin
      SONARR_HOST: "http://sonarr:8989"
      SONARR_API_KEY: "API_KEY"
    ports:
      - "5005:5005"
```

Zusätzlich müsst ihr in Sonarr oder Prowlarr einen neuen Indexer hinzufügen (für jeden Indexer, bei dem UmlautAdapdarr greifen soll).

Am Beispiel von sceneNZBs:

![grafik](https://github.com/PCJones/UmlautAdaptarr/assets/377223/97ca0aef-1a9e-4560-9374-c3a8215dafd2)

Also alles wie immer, nur dass ihr als API-URL nicht direkt z.B. `https://scenenzbs.com` eingebt, sondern 
`http://localhost:5005/_/scenenzbs.com`

## Was macht UmlautAdaptarr überhaupt?
UmlautAdaptarr löst mehrere Probleme:
- Releases mit Umlauten werden grundsätzlich nicht korrekt von den *Arrs importiert
- Releases mit Umlauten werden oft nicht korrekt gefunden (*Arrs suchen nach "o" statt "ö" & es fehlt häufig die korrekte Zuordnung zur Serie/zum Film beim Indexer)
- Sonarr & Radarr erwarten immer den englischen Titel von https://thetvdb.com/ bzw. https://www.themoviedb.org/. Das führt bei deutschen Produktionen oder deutschen Übersetzungen oft zu Problemen - falls die *arrs schon mal etwas mit der Meldung `Found matching series/movie via grab history, but release was matched to series by ID. Automatic import is not possible/` nicht importiert haben, dann war das der Grund.

# Wie macht UmlautAdaptarr das?
UmlautAdaptarr tut so, als wäre es ein Indexer. In Wahrheit schaltet sich UmlautAdaptarr aber nur zwischen die *arrs und den Indexer und kann somit die Suchen sowie die Ergebnisse abfangen und bearbeiten.
Am Ende werden die gefundenen Releases immer so umbenannt, das die Arrs sie einwandfrei erkennen.
Einige Beispiele findet ihr unter Features.


## Features

| Feature                                                           | Status        |
|-------------------------------------------------------------------|---------------|
| Sonarr & Prowlarr Support                                         | ✓             |
| Releases mit deutschem Titel werden erkannt   | ✓             |
| Releases mit TVDB-Alias Titel werden erkannt  | ✓             |
| Korrekte Suche und Erkennung von Titel mit Umlauten                            | ✓             |
| Anfragen-Caching für 5 Minuten zur Reduzierung der API-Zugriffe   | ✓             |
| Radarr Support                                                    | Geplant       |
| Readarr Support                                                   | Geplant       |
| Prowlarr Unterstützung für "DE" SceneNZBs Kategorien              | Geplant       |
| Unterstützung weiterer Sprachen neben Deutsch                     | Geplant       |
| Wünsche?                                                          | Vorschläge?   |

## Beispiel-Funktionalität
In den Klammern am Ende des Releasenamens (Bild 2 & 4) steht zu Anschauungszwecken der deutsche Titel der vorher nicht gefunden bzw. akzeptiert wurde. Das bleibt natürlich nicht so ;)

**Vorher:**  
![Vorherige Suche ohne deutsche Titel](https://i.imgur.com/7pfRzgH.png)  
Release wird zwar gefunden, kann aber kann nicht zu geordnet werden.

**Jetzt:**  
![Jetzige Suche mit deutschen Titeln](https://i.imgur.com/k55YIN9.png)  
2-3 weitere Releases werden gefunden, außerdem meckert Sonarr nicht mehr über den Namen und würde es bei einer automatischen Suche ohne Probleme importieren.

**Vorher:**  
![Vorherige Suche, englische Titel](https://i.imgur.com/pbRlOeX.png)  
Es werden nur Releases mit dem englischen Titel der Serie gefunden

**Jetzt:**  
![Jetzige Suche, deutsche und englische Titel](https://i.imgur.com/eeq0Voj.png)  
Es werden auch Titel mit dem deutschen Namen gefunden :D (haben nicht alle Suchergebnisse auf den Screenshot gepasst)

**Vorher:**
Die deutsche Produktion `Alone - Überlebe die Wildnis` hat auf [TheTVDB](https://thetvdb.com/series/alone-uberlebe-die-wildnis) den Englischen Namen `Alone Germany`.
![grafik](https://github.com/PCJones/UmlautAdaptarr/assets/377223/62158f77-ecc2-4747-af85-4b8f94f51ab4)
Sonarr erwartet immer den Englischen Namen, der hier natürlich nicht gegeben ist.

**Jetzt:**
![grafik](https://github.com/PCJones/UmlautAdaptarr/assets/377223/57539ffc-b8a6-4255-a7f8-03079c10b1e8)
UmlautAdaptarr hat die Releases in `Alone Germany` umbenannt und Sonarr hat keine Probleme mehr

**Vorher:**
![grafik](https://github.com/PCJones/UmlautAdaptarr/assets/377223/8f3297bd-ebe4-42de-b4e6-952882c8b902)
Hier wird der komplette deutsche Titel im Release angegeben (also mit `- Das Lied von Eis und Feuer`) - glücklicherweise stellt uns [TheTVDB](https://thetvdb.com/series/game-of-thrones) aber diesen längeren Titel als Alias zur Verfügung - nur nutzt Sonarr diese Informationen einfach nicht.

**Jetzt:**
![grafik](https://github.com/PCJones/UmlautAdaptarr/assets/377223/52f0caf5-6e9d-442e-9018-ba29f954a890)
UmlautAdapatarr erkennt alle auf TheTVDB angegebenen Aliase und benennt das Release in den Englischen Titel um

## Kontakt & Support
- Öffne gerne ein Issue auf GitHub falls du Unterstützung benötigst.
- [Telegram](https://t.me/pc_jones)
- Discord: pcjones1
- Reddit: /u/IreliaIsLife


### Licenses & Metadata source
- TV Metadata source: https://thetvdb.com
- Movie Metadata source: https://themoviedb.org
- Licenses: TODO
