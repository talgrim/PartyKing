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
- [API](#api)
- [Testy](#testy)
- [Znane ograniczenia](#znane-ograniczenia)

---

## Funkcje

### Slideshow zdjęć

- Goście wchodzą na `/image-upload`, wybierają zdjęcia z telefonu i wysyłają je na serwer
  (wiele plików naraz, obsługiwane rozszerzenia: `.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`).
- Opcja **„Usuń po wyświetleniu"** — zdjęcie zostaje skasowane (plik + rekord w bazie)
  natychmiast po tym, jak zejdzie z ekranu.
- Ekran prezentacyjny (`/slideshow`) pokazuje zdjęcia jedno po drugim, w kolejności wrzucania.
- Gdy nie ma nic nowego do pokazania, serwer wyświetla wbudowane zdjęcia zastępcze
  (*placeholdery*) z katalogu `wwwroot/SampleImages/Placeholder`, w pętli.
- Każde zdjęcie jest oznaczane jako wyświetlone (`WasPresented`), więc slideshow **wznawia się
  w tym samym miejscu po restarcie serwera**.
- Zachowanie steruje się ustawieniami trzymanymi w bazie: `AutoRepeat` (zapętlenie po dojściu do
  końca), `StartFromBeginning` (pokazuj też zdjęcia już wyświetlone), `SlideTime` (czas slajdu).

### Kolejka muzyki

- Jedna wspólna kolejka (YouTube + Spotify), trzymana w bazie SQLite — przeżywa restart serwera.
- Goście wchodzą na `/music-queue` (bez logowania) i dodają utwory przez **wyszukiwanie w Spotify**
  albo **wklejenie linku** (link/URI utworu ze Spotify lub adres YouTube). Poniżej widzą listę
  „następne w kolejce".
- Gospodarz loguje się kontem Spotify (wymagany **Premium**) i otwiera `/music`: strona odtwarza
  kolejkę — utwory ze Spotify przez Web Playback SDK, utwory z YouTube przez odtwarzacz YouTube —
  i automatycznie przechodzi do następnego utworu.
- Jest jedno konto gospodarza; goście nigdy nie logują się do Spotify. Backend używa tokena
  gospodarza do wyszukiwania i pobierania danych utworów w imieniu gości.

## Jak to działa

Kluczowa decyzja architektoniczna: **o tym, które zdjęcie jest aktualnie „na ekranie", decyduje
serwer**, a nie przeglądarka. Klient pyta co 5 sekund o aktualny slajd i dostaje jego nazwę razem
z datą wygaśnięcia. Dzięki temu kilka ekranów podłączonych jednocześnie pokazuje to samo zdjęcie,
a stan prezentacji przeżywa odświeżenie strony.

```
[telefon gościa]                              [ekran prezentacyjny]
 /image-upload                                  /slideshow
      |                                              |
      | PUT /upload-photo/{deleteAfterPresentation}  | GET /get-photo  (co 5 s)
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
| `PartyKing.Persistence` | `DbContext`, konfiguracje encji, migracje, dane startowe |
| `PartyKing.Domain` | Encje, enumy, modele read/write |
| `PartyKing.Application.Tests` | Testy jednostkowe (xUnit) |
| `PartyKing.UI` | SPA w React (osobny projekt npm w `src/ui`, poza `.sln`) |

Kierunek zależności: `API → Application → Infrastructure (+ Infrastructure.Spotify) → Persistence → Domain`,
przy czym `Contract` jest używany przez `API` i `Application`.

W warstwie danych odczyt jest rozdzielony od zapisu — istnieją dwa konteksty,
`ReaderDbContext` i `WriterDbContext`, oba dziedziczące po `PartyKingDbContextBase`.

Strony frontendu (`src/ui/PartyKing.UI/src/pages`):

| Ścieżka | Strona | Opis |
|---|---|---|
| `/slideshow` | `Slideshow` | Ekran prezentacyjny — to okno pokazuje się na telewizorze / ekranie LED |
| `/image-upload` | `ImageUploader` | Wysyłanie zdjęć z podglądem miniatur i paskiem postępu |
| `/music-queue` | `MusicQueue` | Dla gości: wyszukiwanie / wklejanie linku i lista kolejki (bez logowania) |
| `/music` | `Music` | Dla gospodarza: odtwarzacz (Spotify + YouTube) z automatycznym przechodzeniem dalej |
| `/login`, `/callback` | `Login`, `Callback` | Logowanie gospodarza do Spotify; po zalogowaniu przekierowanie na `/music` |
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

> **Adres API.** Domyślnie API nasłuchuje na `http://localhost:4200`, a frontend łączy się z tym
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
`src/api/PartyKing.API/Properties/launchSettings.json` (domyślnie `http://localhost:4200`) —
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
| `http://<host-ui>:2137/slideshow` | Ekran prezentacyjny |
| `http://<host-ui>:2137/image-upload` | Strona do wrzucania zdjęć (ten link dajesz gościom) |
| `http://<host-ui>:2137/music-queue` | Kolejka muzyki dla gości (drugi link dla gości) |
| `http://127.0.0.1:2137/login` | Logowanie gospodarza do Spotify, potem `/music` z odtwarzaczem |
| `http://<host-api>/swagger` | Swagger UI (tylko w Development) |
| `https://localhost:17218` | Dashboard Aspire (wariant A) |

## Konfiguracja

### Adres API

Adres backendu jest ustawiony **w dwóch miejscach** i w obu musi być taki sam (domyślnie
`http://localhost:4200`):

1. `src/api/PartyKing.API/Properties/launchSettings.json` → `applicationUrl`
2. `src/ui/PartyKing.UI/src/envVariables.ts` → wartość `_apiUrl`

Żeby telefony gości mogły połączyć się z serwerem, w obu miejscach użyj adresu IP maszyny
w lokalnej sieci zamiast `localhost` (tej zmiany nie commituj). API ma politykę CORS przepuszczającą
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
Spotify. Używa `127.0.0.1`, więc gospodarz musi logować się przez `http://127.0.0.1:2137/login`
(nie `localhost`). Pierwsze zapisane konto Spotify jest traktowane jako konto gospodarza.

### Ustawienia slideshow

`src/api/PartyKing.API/appsettings.json`:

```json
{
  "SlideshowSettings": {
    "UploadedPhotosDirectory": "SampleImages\\Uploaded",
    "PlaceholderPhotosDirectory": "SampleImages\\Placeholder",
    "SlideTime": "00:00:05"
  }
}
```

| Klucz | Znaczenie |
|---|---|
| `UploadedPhotosDirectory` | Katalog (względem `wwwroot`), do którego trafiają zdjęcia gości |
| `PlaceholderPhotosDirectory` | Katalog ze zdjęciami zastępczymi, skanowany rekurencyjnie przy starcie |
| `SlideTime` | Domyślny czas jednego slajdu — używany, gdy `slideTime` nie zostanie podany w `POST /update-configuration` |

Uwaga: **obowiązujący** czas slajdu, `AutoRepeat` i `StartFromBeginning` są przechowywane
w bazie danych (tabela `SlideshowSettings`, jeden wiersz) i zmienia się je przez API —
`SlideTime` z `appsettings.json` służy tylko jako wartość domyślna.

Lokalne nadpisania można trzymać w `appsettings.Local.json` lub `appsettings.Development.json`
(pierwszy plik jest wykluczony z repozytorium przez `.gitignore`).

## Baza danych

SQLite, plik `PartyKing.db` tworzony w katalogu roboczym procesu API
(przy `dotnet run --project src/api/PartyKing.API` to `src/api/PartyKing.API/`).

- Migracje (`src/api/PartyKing.Persistence/Migrations`) są **stosowane automatycznie przy starcie API**
  przez `app.Services.MigrateDb()` — nie trzeba nic robić ręcznie.
- Dane startowe: 19 przykładowych zdjęć (`SlideshowImageSeedHelper`) oraz domyślny wiersz
  ustawień slideshow (`SlideTime` = 20 s, `AutoRepeat` i `StartFromBeginning` wyłączone).
- Aby zacząć od czystej bazy, wystarczy usunąć plik `PartyKing.db` i zrestartować API.

Dodanie nowej migracji (z katalogu `src/api`; wymaga `dotnet tool install --global dotnet-ef`) — ponieważ w projekcie
są dwa konteksty, trzeba wskazać ten służący do zapisu:

```bash
dotnet ef migrations add NazwaMigracji \
  --project PartyKing.Persistence \
  --startup-project PartyKing.Persistence \
  --context WriterDbContext
```

## API

Kontrolery nie mają prefiksu trasy — **wszystkie endpointy są w korzeniu**, np.
`GET http://<host-api>/get-photo`. W przykładach poniżej:

```bash
API=http://localhost:4200   # podmień na własny adres z launchSettings.json
```

### Slideshow

#### `PUT /upload-photo/{deleteAfterPresentation}`

Wysłanie jednego lub wielu zdjęć. Dane w formacie `multipart/form-data`, pole **`files`**
(powtórzone dla każdego pliku). Parametr `deleteAfterPresentation` (`true`/`false`) decyduje,
czy zdjęcia mają być usuwane po wyświetleniu.

```bash
curl -X PUT "$API/upload-photo/false" \
  -F "files=@zdjecie1.jpg" \
  -F "files=@zdjecie2.png"
```

Odpowiedzi: `200 OK` (puste ciało) albo `400 Bad Request` z tekstem błędu.

#### `GET /get-photo`

Zwraca zdjęcie, które ma być aktualnie na ekranie, wraz z momentem jego wygaśnięcia
(UTC). To ten endpoint odpytuje strona `/slideshow` co 5 sekund.

```bash
curl "$API/get-photo"
```

```json
{
  "fileName": "SampleImages\\Uploaded\\01988512-f059-73dd-ae67-29b2edf9f268.jpeg",
  "expirationDate": "2026-09-22T20:15:42.1234567+00:00"
}
```

`fileName` to ścieżka względna wewnątrz `wwwroot`, więc sam plik pobiera się bezpośrednio:

```bash
curl -O "$API/SampleImages/Uploaded/01988512-f059-73dd-ae67-29b2edf9f268.jpeg"
```

Jeśli nie ma nowych zdjęć do pokazania (wszystkie zostały już wyświetlone, a `AutoRepeat`
i `StartFromBeginning` są wyłączone), zwracane jest zdjęcie zastępcze. Ścieżki placeholderów
zawierają **wiodący separator**, bo są wyliczane względem katalogu `wwwroot`:

```json
{
  "fileName": "\\SampleImages\\Placeholder\\IMG_3383.JPG",
  "expirationDate": "2026-09-22T10:08:49.2013336+00:00"
}
```

Endpoint w praktyce zawsze zwraca `200 OK` — przewidziana w kontrolerze odpowiedź
`204 No Content` jest nieosiągalna, bo serwis zawsze ma w zapasie placeholdery.

#### `GET /get-all`

Lista plików znajdujących się w katalogu wrzuconych zdjęć (czytana z dysku, nie z bazy).

```bash
curl "$API/get-all"
```

```json
[
  { "fileName": "IMG_2332.png",  "path": "SampleImages\\Uploaded\\IMG_2332.png" },
  { "fileName": "IMG_7758.jpeg", "path": "SampleImages\\Uploaded\\IMG_7758.jpeg" }
]
```

#### `GET /get-configuration`

Konfiguracja wczytana z `appsettings.json`.

```bash
curl "$API/get-configuration"
```

```json
{
  "uploadedPhotosDirectory": "SampleImages\\Uploaded",
  "placeholderPhotosDirectory": "SampleImages\\Placeholder",
  "slideTime": "00:00:05"
}
```

#### `POST /update-configuration`

Zapisuje ustawienia slideshow w bazie danych.

```bash
curl -X POST "$API/update-configuration" \
  -H "Content-Type: application/json" \
  -d '{ "autoRepeat": true, "startFromBeginning": false, "slideTime": "00:00:10" }'
```

| Pole | Typ | Znaczenie |
|---|---|---|
| `autoRepeat` | `bool` | Po dojściu do ostatniego zdjęcia zacznij od początku |
| `startFromBeginning` | `bool` | Pokazuj również zdjęcia już wyświetlone |
| `slideTime` | `string?` (`hh:mm:ss`) | Czas jednego slajdu; `null` → wartość z `appsettings.json` |

Odpowiedź: `200 OK`. Nowy czas slajdu zaczyna obowiązywać przy kolejnej zmianie zdjęcia.

### Muzyka

#### `POST /queue/songs`

Dodaje utwór do wspólnej kolejki. Bez logowania.

```bash
curl -X POST "$API/queue/songs" \
  -H "Content-Type: application/json" \
  -d '{ "input": "https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC" }'
```

`input` to link lub URI utworu ze Spotify (`spotify:track:...`) albo adres YouTube
(`youtube.com`, `m.`, `music.`, `youtu.be`). Odpowiedź `200 OK`:

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

`source` to enum serializowany jako liczba: `0` = Spotify, `1` = YouTube. Dla YouTube `name`
to na razie sam adres. Błędy: `400` (nierozpoznane wejście), `404` (nie ma takiego utworu),
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
  na stronie `/music` (trzeba kliknąć „Start", żeby przeglądarka pozwoliła na dźwięk).
- Gospodarz to pierwszy zapisany profil Spotify; nie ma wyboru ani wylogowania gospodarza.
  Gdy refresh token wygaśnie, gospodarz musi zalogować się ponownie.
- Utwory z YouTube są w kolejce tytułowane samym adresem (brak pobierania tytułu).
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
