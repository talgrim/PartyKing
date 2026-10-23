# PartyKing

Aplikacja imprezowa łącząca dwie funkcje: **slideshow zdjęć wrzucanych przez gości z telefonów**
(wyświetlany na wspólnym ekranie / ekranie LED) oraz **wspólną kolejkę muzyki** (utwory ze Spotify i linki do YouTube), którą goście zasilają
bez logowania, a gospodarz odtwarza na jednym urządzeniu.

Backend to .NET 9 (ASP.NET Core) zorkiestrowany przez .NET Aspire, frontend to SPA w React 19 + Vite.

---

## Spis treści

- [Funkcje](#funkcje)
- [Jak to działa](#jak-to-działa)
- [Stos technologiczny](#stos-technologiczny)
- [Struktura repozytorium](#struktura-repozytorium)
- [Wymagania](#wymagania)
- [Instalacja i uruchomienie](#instalacja-i-uruchomienie)
- [Konfiguracja](#konfiguracja)
- [Baza danych](#baza-danych)
- [Wdrożenie (Docker / Fly.io)](#wdrożenie-docker--flyio)
- [API](#api)
- [Testy](#testy)
- [Znane ograniczenia](#znane-ograniczenia)

---

## Funkcje

### Slideshow zdjęć

- Każda sesja ma **własny slideshow**. Goście wchodzą na `/s/<id>/upload` (po podaniu hasła sesji), wybierają zdjęcia z telefonu i wysyłają je na serwer
  (wiele plików naraz, obsługiwane rozszerzenia: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`).
- Opcja **„Usuń po wyświetleniu"** — zdjęcie zostaje skasowane (plik + rekord w bazie)
  natychmiast po tym, jak zejdzie z ekranu.
- Ekran prezentacyjny (`/s/<id>/slideshow`, też za hasłem sesji) pokazuje zdjęcia jedno po drugim, w kolejności wrzucania.
- Gdy sesja nie ma żadnych zdjęć do pokazania, ekran wyświetla „No photos yet" (nie ma zdjęć zastępczych).
- Admin ustawia dla każdej sesji czas slajdu i opcje powtarzania w panelu `/admin` („Slideshow settings").
- Zdjęcia sesji leżą w `wwwroot/Images/Uploaded/<id sesji>/` i są usuwane razem z sesją.
- Każde zdjęcie jest oznaczane jako wyświetlone (`WasPresented`), więc slideshow **wznawia się
  w tym samym miejscu po restarcie serwera**.
- Zachowanie steruje się ustawieniami trzymanymi w bazie: `AutoRepeat` (zapętlenie po dojściu do
  końca), `StartFromBeginning` (pokazuj też zdjęcia już wyświetlone), `SlideTime` (czas slajdu).

### Kolejka muzyki

- Jedna wspólna kolejka (YouTube + Spotify), trzymana w bazie SQLite — przeżywa restart serwera.
- Każda sesja jest chroniona hasłem, które admin ustawia przy jej tworzeniu (min. 4 znaki; admin może je potem
  zmienić przyciskiem „Change password"). Każdy, także admin, musi je podać (`POST /sessions/{id}/join`, limit
  10 prób na minutę z jednego IP), zanim zobaczy sesję; przeglądarka zapamiętuje to w podpisanym cookie.
  Zmiana hasła unieważnia wcześniejsze odblokowania.
- Goście wchodzą na stronę sesji `/s/<id>` (bez konta, ale z hasłem sesji), widzą co aktualnie gra (okładka, wykonawca,
  tytuł, czy jest pauza) i dodają utwory przez **wyszukiwanie w Spotify** albo **wklejenie linku**
  (link/URI utworu ze Spotify lub adres YouTube). Poniżej widzą listę „następne w kolejce".
- Gospodarz loguje się kontem Spotify (wymagany **Premium**) i wraca na tę samą stronę sesji, na której
  pojawia się odtwarzacz: odtwarza kolejkę — utwory ze Spotify przez Web Playback SDK, utwory z YouTube
  przez odtwarzacz YouTube — i automatycznie przechodzi do następnego utworu. Przyciski Play/Pause
  i Następny utwór widzą tylko gospodarz i admin (admin steruje przez serwer, o ile odtwarzacz gospodarza
  jest połączony).
- Jest jedno konto gospodarza; goście nigdy nie logują się do Spotify. Backend używa tokena
  gospodarza do wyszukiwania i pobierania danych utworów w imieniu gości.

## Jak to działa

Kluczowa decyzja architektoniczna: **o tym, które zdjęcie jest aktualnie „na ekranie", decyduje
serwer**, a nie przeglądarka. Klient pyta co 5 sekund o aktualny slajd i dostaje jego nazwę razem
z datą wygaśnięcia. Dzięki temu kilka ekranów podłączonych jednocześnie pokazuje to samo zdjęcie,
a stan prezentacji przeżywa odświeżenie strony.

```
[telefon gościa]                              [ekran prezentacyjny]
 /s/<id>/upload                                 /s/<id>/slideshow
      |                                              |
      | PUT /sessions/<id>/photos/{deleteAfterPresentation} | GET /sessions/<id>/photos/current (co 5 s)
      v                                              v
+---------------------------------------------------------------+
|  PartyKing.API  ->  SlideshowService  ->  SlideshowImages-    |
|                     (singleton, trzyma       Repository       |
|                      aktualny slajd)             |            |
+--------------------------------------------------|------------+
                                                   v
                              SQLite (PartyKing.db) + pliki w wwwroot/
```

Kolejność zdjęć jest realizowana bez osobnej kolumny sortującej: identyfikatory to
`Guid.CreateVersion7()` (UUIDv7 zawiera znacznik czasu i jest sortowalny), więc zapytanie
`WHERE Id > currentId ORDER BY Id` naturalnie zwraca kolejne wrzucone zdjęcie.

Kolejka muzyki działa podobnie: stan trzyma baza (`QueueItems`), a `QueueRepository` pilnuje, że
w danej chwili gra co najwyżej jeden element (`Playing`); `POST /queue/songs/next` oznacza go jako
`Played` i uruchamia następny w kolejce (`Queued`).

## Stos technologiczny

**Backend**

| Technologia | Zastosowanie |
|---|---|
| .NET 9 / C# 13 | Platforma |
| ASP.NET Core (kontrolery) | Warstwa HTTP |
| .NET Aspire 9.4 | Orkiestracja (AppHost + hosting aplikacji npm), dashboard |
| Entity Framework Core 9 + SQLite | Dostęp do danych, migracje, seed |
| ErrorOr | Mapowanie błędów domenowych na odpowiedzi HTTP |
| Spotify Web API | Wyszukiwanie utworów, logowanie gospodarza (OAuth) |
| Swashbuckle / Swagger | Dokumentacja API (tylko w Development) |
| xUnit | Testy backendu |
| OpenTelemetry | Metryki, logi i traces (przez ServiceDefaults) |

**Frontend**

| Technologia | Zastosowanie |
|---|---|
| React 19 + TypeScript | UI |
| Vite 7 | Bundler i dev server |
| MUI 7 + Emotion | Komponenty (ciemny motyw) |
| Tailwind CSS 4 | Stylowanie utility-first |
| SWR | Pobieranie danych i polling |
| React Router 7 | Routing |
| axios | Klient HTTP (upload plików) |
| Splide | Karuzela slideshow |
| react-player | Odtwarzacz YouTube |
| Spotify Web Playback SDK | Odtwarzanie utworów ze Spotify w przeglądarce gospodarza |
| Vitest + Testing Library | Testy |

## Struktura repozytorium

Repozytorium ma dwa katalogi główne: backend w `src/api`, frontend w `src/ui/PartyKing.UI`.

Backend jest podzielony na warstwy w stylu Clean Architecture (projekty w `src/api/PartyKing.sln`):

| Projekt | Odpowiedzialność |
|---|---|
| `PartyKing.AppHost` | Aspire — uruchamia API oraz UI jako aplikację npm |
| `PartyKing.ServiceDefaults` | Współdzielone: OpenTelemetry, health checks, service discovery, resilience |
| `PartyKing.API` | Kontrolery, Swagger, CORS, uwierzytelnianie cookie, pliki statyczne (`wwwroot`) |
| `PartyKing.Contract` | DTO wersjonowane (`V1/...`) — kontrakt dla frontendu |
| `PartyKing.Application` | `SlideshowService`, `QueueService`, `SpotifyService`, `HostTokenProvider`, konfiguracja, mapowania |
| `PartyKing.Infrastructure` | Repozytoria (zdjęcia, kolejka, profile Spotify) — baza danych + operacje na plikach |
| `PartyKing.Infrastructure.Spotify` | Klient Spotify Web API (logowanie, wyszukiwanie, pobieranie utworu) |
| `PartyKing.Infrastructure.Youtube` | Klient oEmbed YouTube (tytuł, autor, miniatura; sprawdzenie, czy film można osadzić) |
| `PartyKing.Persistence` | `DbContext`, konfiguracje encji, migracje, dane startowe |
| `PartyKing.Domain` | Encje, enumy, modele read/write |
| `PartyKing.Application.Tests` | Testy jednostkowe (xUnit) |
| `PartyKing.UI` | SPA w React (osobny projekt npm w `src/ui`, poza `.sln`) |

Kierunek zależności: `API → Application → Infrastructure (+ Infrastructure.Spotify, Infrastructure.Youtube) → Persistence → Domain`,
przy czym `Contract` jest używany przez `API` i `Application`.

W warstwie danych odczyt jest rozdzielony od zapisu — istnieją dwa konteksty,
`ReaderDbContext` i `WriterDbContext`, oba dziedziczące po `PartyKingDbContextBase`.

Strony frontendu (`src/ui/PartyKing.UI/src/pages`):

| Ścieżka | Strona | Opis |
|---|---|---|
| `/s/:sessionId/slideshow` | `Slideshow` | Ekran prezentacyjny sesji — to okno pokazuje się na telewizorze / ekranie LED (wymaga hasła sesji) |
| `/s/:sessionId/upload` | `ImageUploader` | Wysyłanie zdjęć do slideshow sesji z podglądem miniatur i paskiem postępu (wymaga hasła sesji) |
| `/s/:sessionId` | `MusicQueue` | Strona sesji: co gra, wyszukiwanie / wklejanie linku i lista kolejki (bez logowania); dla gospodarza dodatkowo odtwarzacz (Spotify + YouTube), dla admina przyciski sterowania |
| `/callback` | `Callback` | Powrót ze Spotify po zalogowaniu gospodarza; potem przekierowanie na stronę sesji |
| `/` | `Home` | Ekran startowy |

Na stronach slideshow, wysyłania zdjęć i kolejki są przyciski nawigacji między nimi.

## Wymagania

- **.NET SDK 9.0** lub nowszy
- **Node.js 20.19+ albo 22.12+** (wymóg Vite 7) i npm
- Do muzyki ze Spotify: aplikacja w [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
  (Client ID i Client Secret) oraz konto **Spotify Premium** gospodarza
- Opcjonalnie: runtime kontenerów (Docker/Podman) — potrzebny tylko przy publikacji UI
  jako obrazu (`PublishAsDockerFile()`), nie przy pracy lokalnej

Baza danych nie wymaga instalacji — SQLite to jeden plik tworzony automatycznie przy starcie.

## Instalacja i uruchomienie

```bash
git clone https://github.com/talgrim/PartyKing.git
cd PartyKing

# zależności frontendu
cd src/ui/PartyKing.UI && npm install && cd ../../..
```

> **Adres API.** Domyślnie API nasłuchuje na `http://127.0.0.1:4200`, a frontend łączy się z tym
> adresem. Wystarczy to do pracy na jednym komputerze; żeby wejść z telefonów, podmień adres
> na IP maszyny: [Konfiguracja](#konfiguracja).

### Warianty uruchomienia

#### A. Wszystko naraz, przez Aspire (zalecane)

```bash
dotnet run --project src/api/PartyKing.AppHost
```

AppHost startuje API, następnie (po jego gotowości) frontend, i otwiera **dashboard Aspire**
pod `https://localhost:17218`. Na dashboardzie widać aktualne adresy obu usług, logi, metryki
i traces. Migracje bazy danych wykonują się automatycznie przy starcie API.

Wariant ten wymaga `npm` dostępnego w `PATH` — bez zainstalowanego Node.js zasób `ui`
nie wystartuje.

#### B. Osobno backend i frontend

```bash
# terminal 1 — API
dotnet run --project src/api/PartyKing.API

# terminal 2 — frontend
cd src/ui/PartyKing.UI
npm start
```

Frontend wstaje na porcie **2137** i nasłuchuje na `0.0.0.0`, żeby był dostępny z telefonów
w tej samej sieci Wi-Fi. API domyślnie nasłuchuje na adresie z
`src/api/PartyKing.API/Properties/launchSettings.json` (domyślnie `http://127.0.0.1:4200`) —
przy pracy z telefonów trzeba go dostosować do własnej sieci, patrz [Konfiguracja](#konfiguracja).

Adres można też nadpisać jednorazowo, bez modyfikowania plików projektu:

```bash
dotnet run --project src/api/PartyKing.API --no-launch-profile -- \
  --urls=http://0.0.0.0:4200 --environment=Development
```

`--environment=Development` jest tu potrzebne, bo pominięcie profilu uruchomieniowego
pomija również ustawienie środowiska — bez niego API wstanie w trybie Production,
czyli bez Swaggera i bez endpointów `/health` i `/alive`.

### Po uruchomieniu

| Adres | Co to |
|---|---|
| `http://<host-ui>:2137/s/<id>/slideshow` | Ekran prezentacyjny sesji |
| `http://<host-ui>:2137/s/<id>/upload` | Strona do wrzucania zdjęć do sesji (ten link dajesz gościom) |
| `http://<host-ui>:2137/s/<id>` | Strona sesji z kolejką muzyki dla gości (drugi link dla gości); gospodarz loguje się tu przez „Log in as host with Spotify" |
| `http://<host-api>/swagger` | Swagger UI (tylko w Development) |
| `https://localhost:17218` | Dashboard Aspire (wariant A) |

## Konfiguracja

### Adres API

Adres backendu jest ustawiony **w dwóch miejscach** i w obu musi być taki sam (domyślnie
`http://127.0.0.1:4200`):

1. `src/api/PartyKing.API/Properties/launchSettings.json` → `applicationUrl`
2. `src/ui/PartyKing.UI/src/envVariables.ts` → wartość `_apiUrl`

Żeby telefony gości mogły połączyć się z serwerem, w obu miejscach użyj adresu IP maszyny
w lokalnej sieci zamiast `127.0.0.1` (tej zmiany nie commituj). API ma politykę CORS przepuszczającą
dowolne pochodzenie.

### Spotify

W `src/api/PartyKing.API/appsettings.json` (sekcja `SpotifySettings`) wpisz dane aplikacji ze
Spotify Developer Dashboard. Najlepiej trzymać je w `appsettings.Local.json`, który jest
wykluczony z repozytorium:

```json
{
  "SpotifySettings": {
    "ClientId": "<client id>",
    "ClientSecret": "<client secret>",
    "RedirectUri": "http://127.0.0.1:2137/callback"
  }
}
```

`RedirectUri` musi być dokładnie taki sam jak adres dodany w ustawieniach aplikacji w Dashboardzie
Spotify. Używa `127.0.0.1`, więc gospodarz musi logować się przez `http://127.0.0.1:2137/...`
(nie `localhost`; serwer Vite sam przekierowuje `localhost` na `127.0.0.1`). Pierwsze zapisane konto Spotify jest traktowane jako konto gospodarza.

### Ustawienia slideshow

`src/api/PartyKing.API/appsettings.json`:

```json
{
  "SlideshowSettings": {
    "UploadedPhotosDirectory": "Images/Uploaded",
    "SlideTime": "00:00:05"
  }
}
```

| Klucz | Znaczenie |
|---|---|
| `UploadedPhotosDirectory` | Katalog (względem `wwwroot`), do którego trafiają zdjęcia gości; każda sesja ma w nim podkatalog o nazwie swojego id |
| `SlideTime` | Domyślny czas jednego slajdu — używany w sesji, która nie ustawiła własnego, i gdy `slideTime` nie zostanie podany w `PUT /admin/sessions/{id}/slideshow-settings` |

Uwaga: **obowiązujący** czas slajdu, `AutoRepeat` i `StartFromBeginning` są przechowywane
w bazie danych (tabela `SlideshowSettings`, jeden wiersz na sesję) i zmienia się je przez API
lub panel admina — `SlideTime` z `appsettings.json` służy tylko jako wartość domyślna.

Lokalne nadpisania można trzymać w `appsettings.Local.json` lub `appsettings.Development.json`
(pierwszy plik jest wykluczony z repozytorium przez `.gitignore`).

## Baza danych

SQLite, plik `PartyKing.db` tworzony w katalogu roboczym procesu API
(przy `dotnet run --project src/api/PartyKing.API` to `src/api/PartyKing.API/`).

- Migracje (`src/api/PartyKing.Persistence/Migrations`) są **stosowane automatycznie przy starcie API**
  przez `app.Services.MigrateDb()` — nie trzeba nic robić ręcznie.
- Dane startowe: domyślny wiersz ustawień slideshow (`SlideTime` = 20 s, `AutoRepeat` i `StartFromBeginning` wyłączone).
- Aby zacząć od czystej bazy, wystarczy usunąć plik `PartyKing.db` i zrestartować API.

Dodanie nowej migracji (z katalogu `src/api`; wymaga `dotnet tool install --global dotnet-ef`) — ponieważ w projekcie
są dwa konteksty, trzeba wskazać ten służący do zapisu:

```bash
dotnet ef migrations add NazwaMigracji \
  --project PartyKing.Persistence \
  --startup-project PartyKing.Persistence \
  --context WriterDbContext
```

## Wdrożenie (Docker / Fly.io)

Jeden obraz (`Dockerfile`) zawiera nginx i API: nginx serwuje zbudowany frontend (z fallbackiem SPA) i przekazuje
`/api/*` do Kestrela (prefiks `/api` jest obcinany), więc całość działa pod jednym adresem i ciasteczka nie wymagają CORS.
Frontend w buildzie produkcyjnym używa względnego `/api` (nadpisanie: `VITE_API_URL`).

Wszystko, co ma przetrwać redeploy, leży na wolumenie `/data`: baza (`PARTYKING_DATABASE_PATH`), klucze Data Protection
(`DataProtection__KeysDirectory`, bez nich po redeployu wylogowałoby to hosta i admina) i zdjęcia (`ASPNETCORE_WEBROOT=/data/wwwroot`).
Obraz nie zawiera żadnych zdjęć — bez wgranych zdjęć slideshow pokazuje „No photos yet”.

Pierwsze wdrożenie na Fly.io (aplikacja musi działać na **jednej** maszynie: SQLite i stan w pamięci):

```bash
fly launch --no-deploy --copy-config
fly volumes create partyking_data --size 1
fly secrets set \
  SpotifySettings__ClientId=... SpotifySettings__ClientSecret=... \
  AdminSettings__Username=admin AdminSettings__PasswordHash=...   # hash: dotnet run --project PartyKing.API -- hash-password
fly deploy
fly certs add party.talgrim.dev   # + rekord CNAME party -> <app>.fly.dev
```

Klucze Data Protection (`/data/keys`) leżą na wolumenie **bez szyfrowania** — kto ma dostęp do wolumenu lub jego backupu,
mógłby podrobić ciasteczka. Na Fly to akceptowalne; `/health` jest dostępne tylko na wewnętrznym porcie 8182 (używa go check z `fly.toml`).

W panelu Spotify dodaj redirect URI `https://party.talgrim.dev/callback` (ustawiony też w `fly.toml`).
Lokalny test obrazu: `docker build -t partyking .` i `docker run -p 8181:8181 -v partyking-data:/data partyking`
(Spotify wymaga wtedy loopbacku/HTTPS, więc logowanie hosta lokalnie nie zadziała).

## API

Kontrolery nie mają prefiksu trasy — **wszystkie endpointy są w korzeniu**, np.
`GET http://<host-api>/sessions/<id>/photos/current`. W przykładach poniżej:

```bash
API=http://127.0.0.1:4200   # podmień na własny adres z launchSettings.json
```

### Slideshow

Slideshow jest osobny dla każdej sesji. Endpointy zdjęć wymagają hasła sesji (ciasteczko
z `POST /sessions/{id}/join`, jak przy kolejce muzyki), a bez niego odpowiadają `401`.

```bash
SESSION=<id sesji>
curl -c cookies.txt -X POST "$API/sessions/$SESSION/join" \
  -H "Content-Type: application/json" -d '{ "password": "haslo" }'
```

#### `PUT /sessions/{sessionId}/photos/{deleteAfterPresentation}`

Wysłanie jednego lub wielu zdjęć. Dane w formacie `multipart/form-data`, pole **`files`**
(powtórzone dla każdego pliku). Parametr `deleteAfterPresentation` (`true`/`false`) decyduje,
czy zdjęcia mają być usuwane po wyświetleniu.

Dozwolone są tylko `jpg`, `jpeg`, `png`, `gif` i `webp` (sprawdzane po rozszerzeniu i bajtach pliku,
typ z nagłówka klienta jest ignorowany), maks. 20 MB na zdjęcie i 100 MB na żądanie, limit 60 żądań
na minutę z jednego IP (`429`).

```bash
curl -b cookies.txt -X PUT "$API/sessions/$SESSION/photos/false" \
  -F "files=@zdjecie1.jpg" \
  -F "files=@zdjecie2.png"
```

Odpowiedzi: `200 OK` (puste ciało), `400 Bad Request` z powodem (np. nieobsługiwany format),
`401 Unauthorized` (sesja zablokowana) albo `429 Too Many Requests`.

#### `GET /sessions/{sessionId}/photos/current`

Zwraca zdjęcie, które ma być aktualnie na ekranie sesji, wraz z momentem jego wygaśnięcia
(UTC). To ten endpoint odpytuje strona `/s/<id>/slideshow` co 5 sekund.

```bash
curl -b cookies.txt "$API/sessions/$SESSION/photos/current"
```

```json
{
  "fileName": "Images/Uploaded/0198850a-0000-7000-8000-000000000000/01988512-f059-73dd-ae67-29b2edf9f268.jpeg",
  "expirationDate": "2026-09-22T20:15:42.1234567+00:00"
}
```

`fileName` to ścieżka względna wewnątrz `wwwroot`, więc sam plik pobiera się bezpośrednio
(pliki nie są chronione hasłem, ale ich adresy zawierają id sesji i identyfikator UUIDv7):

```bash
curl -O "$API/Images/Uploaded/<id sesji>/01988512-f059-73dd-ae67-29b2edf9f268.jpeg"
```

Gdy sesja nie ma żadnego zdjęcia do pokazania (nic nie wrzucono albo wszystkie zostały już wyświetlone,
a `AutoRepeat` i `StartFromBeginning` są wyłączone), endpoint zwraca `204 No Content`,
a strona slideshow pokazuje „No photos yet".

#### `GET /admin/sessions/{sessionId}/slideshow-settings`

Wymaga zalogowanego admina (hasło sesji nie jest potrzebne). Zwraca ustawienia slideshow sesji;
sesja, która nic nie zmieniała, ma wartości domyślne (czas slajdu z `appsettings.json`).

```bash
curl -b admin-cookies.txt "$API/admin/sessions/$SESSION/slideshow-settings"
```

```json
{ "autoRepeat": false, "startFromBeginning": false, "slideTime": "00:00:05" }
```

#### `PUT /admin/sessions/{sessionId}/slideshow-settings`

Wymaga zalogowanego admina. Zapisuje ustawienia slideshow sesji w bazie danych.

```bash
curl -b admin-cookies.txt -X PUT "$API/admin/sessions/$SESSION/slideshow-settings" \
  -H "Content-Type: application/json" \
  -d '{ "autoRepeat": true, "startFromBeginning": false, "slideTime": "00:00:10" }'
```

| Pole | Typ | Znaczenie |
|---|---|---|
| `autoRepeat` | `bool` | Po dojściu do ostatniego zdjęcia zacznij od początku |
| `startFromBeginning` | `bool` | Pokazuj również zdjęcia już wyświetlone |
| `slideTime` | `string?` (`hh:mm:ss`) | Czas jednego slajdu, od 1 sekundy do 1 godziny; `null` → wartość z `appsettings.json` |

Odpowiedź: `204 No Content` (`400` dla niedozwolonego czasu, `404` dla nieznanej sesji).
Nowy czas slajdu zaczyna obowiązywać przy kolejnej zmianie zdjęcia.

### Muzyka

#### `POST /queue/songs`

Dodaje utwór do wspólnej kolejki. Bez logowania.

```bash
curl -X POST "$API/queue/songs" \
  -H "Content-Type: application/json" \
  -d '{ "input": "https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC" }'
```

`input` to link lub URI utworu ze Spotify (`spotify:track:...`) albo adres pojedynczego filmu
z YouTube (`youtube.com/watch?v=`, `/shorts/`, `/embed/`, `/live/`, `m.`, `music.`, `youtu.be`);
playlisty, kanały i wyniki wyszukiwania są odrzucane. Odpowiedź `200 OK`:

```json
{
  "id": 1,
  "source": 0,
  "url": "spotify:track:4uLU6hMCjMI75M1A2tKUQC",
  "name": "Tytuł utworu",
  "artists": "Wykonawca",
  "albumArtUrl": "https://i.scdn.co/image/...",
  "durationMs": 215000
}
```

`source` to enum serializowany jako liczba: `0` = Spotify, `1` = YouTube. Dla YouTube `url`
to znormalizowany adres `https://www.youtube.com/watch?v=<id>`, a `name`, `artists` i `albumArtUrl`
to tytuł, autor i miniatura z oEmbed (gdy YouTube nie odpowiada, `name` to sam adres). Błędy: `400`
(nierozpoznane wejście albo właściciel filmu zablokował odtwarzanie poza YouTube), `404` (nie ma
takiego utworu lub filmu),
`401` (sesja gospodarza wygasła — musi zalogować się ponownie).

#### `GET /queue/songs`

Lista oczekujących utworów (ten sam kształt co powyżej).

#### `POST /queue/songs/next` (gospodarz)

Oznacza bieżący utwór jako odtworzony i uruchamia następny; zwraca go. `404`, gdy kolejka jest
pusta. Wymaga zalogowanego gospodarza (cookie).

#### `GET /spotify/tracks?query=<tekst>`

Wyszukiwanie utworów w Spotify (bez logowania gościa — używany jest token gospodarza). Zwraca
listę `{ uri, name, artists, albumArtUrl, durationMs }`.

#### `GET /spotify/token` (gospodarz)

Token dostępowy gospodarza dla Web Playback SDK w przeglądarce. Wymaga zalogowania.

#### Logowanie do Spotify

`GET /auth-url` zwraca adres autoryzacji Spotify, a `POST /authenticate` (ciało: kod z
przekierowania) wymienia kod na tokeny i ustawia sesję.

### Diagnostyka

| Endpoint | Opis |
|---|---|
| `GET /health` | Wszystkie health checki (tylko Development) |
| `GET /alive` | Sprawdzenie „liveness" (tylko Development) |
| `GET /swagger` | Swagger UI (tylko Development) |

## Testy

```bash
# backend (xUnit) — z katalogu src/api
dotnet test PartyKing.Application.Tests

# frontend (Vitest) — z katalogu src/ui/PartyKing.UI
npm test
```

Backend ma na razie testy tylko parsera wejścia kolejki (`SongInputParser`). Konfiguracja pokrycia
frontendu znajduje się w `vite.config.ts`.

## Znane ograniczenia

- **Odtwarzanie ze Spotify wymaga konta Premium** gospodarza i jest odtwarzane tylko w przeglądarce
  na stronie sesji `/s/<id>` (trzeba kliknąć „Start", żeby przeglądarka pozwoliła na dźwięk).
- Gospodarz to pierwszy zapisany profil Spotify; nie ma wyboru ani wylogowania gospodarza.
  Gdy refresh token wygaśnie, gospodarz musi zalogować się ponownie.
- Filmów z wyłączonym osadzaniem (błąd YouTube 101/150) nie da się odtworzyć w aplikacji. Są
  odrzucane przy dodawaniu do kolejki; jeśli mimo to trafią do odtwarzacza, są pomijane, a
  powód widać w logu błędów pod odtwarzaczem (widocznym tylko dla admina). Pomijanie kończy się po
  dwóch niepowodzeniach z rzędu — wtedy trzeba kliknąć „Next song".
- YouTube nie odtwarza osadzonych filmów (błąd 150), gdy strona jest otwarta z adresu IP, np.
  `127.0.0.1`; z `localhost` działa. Logowanie do Spotify wymaga `127.0.0.1`, więc lokalnie utwory
  z YouTube nie zagrają — problem znika po wdrożeniu aplikacji pod prawdziwą domeną.
- Strona gości nie pokazuje, który utwór aktualnie gra — API tego nie udostępnia.
- **Adres API jest zahardkodowany** w dwóch miejscach (patrz [Konfiguracja](#konfiguracja))
  zamiast być czytany ze zmiennych środowiskowych.
- **Connection string do SQLite jest zahardkodowany** w `PartyKingDbContextBase.OnConfiguring`,
  więc nie da się go nadpisać z `appsettings.json`.
- Nazwa pliku i typ MIME są zamienione miejscami w danych startowych
  (`SlideshowImageSeedHelper` wywołuje konstruktor `SlideshowImage` w błędnej kolejności
  argumentów) — dotyczy wyłącznie 19 przykładowych wpisów, nie zdjęć wrzucanych przez API.
- Ścieżki w odpowiedziach API używają separatora Windows (`\`), a ścieżki placeholderów mają
  dodatkowo wiodący backslash. Przeglądarki normalizują to w adresach URL, więc slideshow działa,
  ale klienci inni niż przeglądarka muszą to uwzględnić.
- Gałąź `204 No Content` w `SlideshowController.GetPhoto` jest martwym kodem —
  `SlideshowService.GetImageAsync` nigdy nie zwraca `null`.
- `src/ui/PartyKing.UI/README.md` to niezmieniony szablon `create-next-app` — projekt korzysta z Vite,
  nie z Next.js.
