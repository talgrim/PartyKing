# The Resistance: rules and expansions

Rules of the Resistance game as implemented in PartyKing, the Plot Cards add-on, and notes on the expansions not implemented yet. Rules are in Polish, implementation notes in English. Source: [consolidated rules v1.1](https://gamers-hq.de/media/pdf/78/dd/0a/The_Resisance_consolidated_rules_v1-1.pdf).

## The Resistance (Opór)

The Resistance (autor: Don Eskridge, wydawca Indie Boards & Cards) to karciana gra towarzyska oparta na dedukcji społecznej, dla 5-10 graczy, ok. 30 minut. Nikt nie odpada z gry. Źródła: Wikipedia (The Resistance (game)), oficjalna instrukcja (the-resistance-rulebook.pdf).

### Cel gry

Gracze są potajemnie podzieleni na dwie drużyny: **Ruch Oporu** (dobrzy) i **Szpiedzy** (źli). Gra składa się z maksymalnie 5 misji. Drużyna, która pierwsza zdobędzie 3 punkty misji, wygrywa. Ruch Oporu nie wie, kto jest szpiegiem (zna tylko liczbę szpiegów), a szpiedzy znają się nawzajem.

### Przygotowanie

- Szpiedzy to 1/3 graczy zaokrąglona w górę, reszta to Ruch Oporu. Role są losowane i znane tylko właścicielowi (karty tożsamości nigdy nie są ujawniane; wyjątek: karty Intryg z dodatku, poza zakresem).
- Podział ról wg liczby graczy:

| Graczy | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|
| Ruch Oporu | 3 | 4 | 4 | 5 | 6 | 6 |
| Szpiedzy | 2 | 2 | 3 | 3 | 3 | 4 |

- Faza "otwarcia oczu": wszyscy zamykają oczy, szpiedzy otwierają je, żeby się zobaczyć, zamykają ponownie, potem wszyscy otwierają oczy. Przy aplikacji: każdy gracz widzi tylko swoją rolę, a szpieg dodatkowo listę pozostałych szpiegów.
- Losowy gracz zostaje pierwszym **Liderem misji**.
- Każdy gracz ma dwie karty głosowania/misji: Sukces i Porażka (do głosowania nad drużyną służą Zatwierdź/Odrzuć).

### Przebieg rundy (jedna misja)

1. **Przekazanie przywództwa**: Liderem zostaje gracz po lewej stronie poprzedniego Lidera (w aplikacji: następny w ustalonej kolejności graczy; przy pierwszej misji lider losowy).
2. **Wybór drużyny**: Lider wskazuje graczy na misję (może wybrać siebie). Wymagany rozmiar drużyny zależy od numeru misji i liczby graczy:

| Misja | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|
| 1 | 2 | 2 | 2 | 3 | 3 | 3 |
| 2 | 3 | 3 | 3 | 4 | 4 | 4 |
| 3 | 2 | 4 | 3 | 4 | 4 | 4 |
| 4 | 3 | 3 | 4 | 5 | 5 | 5 |
| 5 | 3 | 4 | 4 | 5 | 5 | 5 |

3. **Dyskusja**: wszyscy gracze swobodnie dyskutują o propozycji Lidera, mogą mówić cokolwiek i kiedykolwiek (łącznie z kłamstwem). Dyskusja odbywa się poza aplikacją (na żywo).
4. **Głosowanie nad drużyną**: wszyscy gracze (także Lider i członkowie drużyny) głosują jednocześnie i jawnie: Zatwierdź albo Odrzuć. Po odkryciu widać, kto jak zagłosował.
   - Większość "za" zatwierdza drużynę.
   - Większość "przeciw" **lub remis** odrzuca drużynę: przywództwo przechodzi na następnego gracza i Lider wybiera nową drużynę (ta sama misja, ten sam wymagany rozmiar).
   - **Pięć odrzuconych drużyn z rzędu w jednej misji = natychmiastowa wygrana Szpiegów.** Licznik odrzuceń zeruje się po zatwierdzeniu drużyny (i zaczyna od zera w kolejnej misji).
5. **Wykonanie misji**: każdy członek zatwierdzonej drużyny wybiera potajemnie jedną kartę, Sukces albo Porażka.
   - Ruch Oporu **musi** zagrać Sukces.
   - Szpieg może zagrać Sukces (żeby się nie zdradzić) albo Porażka (sabotaż).
   - Karty są tasowane i odkrywane razem, bez informacji, kto którą zagrał (w aplikacji: pokazać tylko liczbę kart Sukces i Porażka, w losowej kolejności lub tylko liczniki).
6. **Wynik misji**: misja kończy się **porażką**, jeśli zagrano choć jedną kartę Porażka. Wyjątek: **w misji 4 przy 7 lub więcej graczach potrzebne są co najmniej dwie karty Porażka**. W przeciwnym razie misja kończy się sukcesem.
   - Porażka misji = punkt dla Szpiegów; sukces = punkt dla Ruchu Oporu.
7. Wynik jest zapisywany (tor misji 1-5), przywództwo przechodzi dalej, zaczyna się kolejna misja.

### Koniec gry

- **Ruch Oporu wygrywa**, gdy zdobędzie 3 udane misje.
- **Szpiedzy wygrywają**, gdy zdobędą 3 nieudane misje albo gdy 5 drużyn z rzędu zostanie odrzuconych w jednej misji.
- Po zakończeniu gry ujawniane są wszystkie role.

### Uwagi do implementacji

- Informacje ukryte per gracz: rola (i lista szpiegów tylko dla szpiegów), wybrana karta misji. Serwer nie może wysyłać tych danych innym graczom (osobny widok per gracz, nie jedna wspólna odpowiedź). Dotyczy to też push SignalR: wspólny `...Changed` bez danych, a klient pobiera swój widok.
- Głosowanie nad drużyną jest jednoczesne: wyniki jawne dopiero gdy zagłosują wszyscy.
- Karty misji: serwer nie zdradza, kto zagrał Porażka, tylko liczbę kart Porażka/Sukces.
- Gracze w aplikacji nie mają kont, trzeba będzie ich jakoś identyfikować w sesji (np. po urządzeniu/ciasteczku), co różni tę grę od Ringo, które jest dla hosta/admina.
- Poza zakresem: dodatki (karty Intryg, Hidden Agenda) i wariant Avalon (Merlin, Assassin itd.).

## The Resistance: Plot Cards (Karty Intryg / Fabuły) - dodatek

Źródło: fizyczna instrukcja (cytat użytkownika, tekst kart poniżej jest autorytatywny), uzupełnione Wikipedią (The Resistance (game)) i opisami z wątków QT3 (The Plot Thickens). Karty były w 2. edycji podstawki, w 3. edycji wróciły jako moduł *The Plot Thickens*. Dodatek jest opcjonalny, wyłączony domyślnie.

### Idea

Każdy Lider misji na początku rundy **dobiera** Karty Intryg i **rozdaje je innym graczom** (jedyny wyjątek, karta zagrywana na siebie: Establish Confidence). Karta to jednorazowa umiejętność (wyjątek: Opinion Maker, działa do końca gry), którą właściciel zagrywa w odpowiednim momencie rundy. Karty dają informacje albo zmieniają przebieg rundy (przejęcie przywództwa, unieważnienie głosowania).

### Teksty kart (oryginał, PL)

- **Jesteś naszym okiem i uchem** (Overheard Conversation) – zagraj natychmiast – gracz, któremu Przywódca przekazał tę kartę, musi obejrzeć kartę Tożsamości jednego z graczy siedzących bezpośrednio obok niego.
- **Liczymy się z twoim zdaniem** (Opinion Maker) – karta o działaniu stałym – gracz, któremu Przywódca przekazał tę kartę, musi wybrać i ujawnić kartę Głosowania zanim pozostali gracze wybiorą karty Głosowania. Karta pozostaje w grze do końca rozgrywki. Jeśli w grze znajdują się dwie takie karty, posiadający je gracze odsłaniają swoje karty Głosowania równocześnie.
- **To kwestia zaufania** (Establish Confidence) – zagraj natychmiast – Przywódca musi ujawnić swoją kartę Tożsamości innemu graczowi. Uwaga – to jedyna karta, którą Przywódca zagrywa na siebie.
- **Tylko tobie zaufam** (Open Up) – zagraj natychmiast – gracz, któremu Przywódca przekazał tę kartę, musi ujawnić swoją kartę Tożsamości innemu graczowi (może być nim również Przywódca).
- **Nie ufajcie mu** (No Confidence) – karta jednorazowa – gracz, któremu Przywódca przekazał tę kartę, może użyć jej, aby odrzucić pozytywny wynik głosowania, którego skutkiem było zatwierdzenie Drużyny. Zagranie tej karty powoduje, że głosowanie uznaje się za nieudane. Należy zaznaczyć to na liczniku Nieudanych Głosowań. Przywódca przekazuje żeton Przywódcy kolejnemu graczowi (zgodnie z ruchem wskazówek zegara) i faza Zbierania Drużyny jest powtarzana.
- **Mam cię na oku** (Keeping a Close Eye on You) – karta jednorazowa – gracz, któremu Przywódca przekazał tę kartę, może użyć jej, aby podejrzeć zagraną kartę Misji. Gracz nie musi obwieszczać, że chce użyć tej karty, zanim gracze wybiorą karty Misji, może zagrać tę kartę zaraz po tym, gdy gracze zagrają karty Misji. Jeśli w grze jest kilka kart Mam cię na oku, to w trakcie rundy może być sprawdzonych kilka kart Misji, lecz pojedyncza karta Misji może zostać sprawdzona tylko przez jednego gracza. Innymi słowy każdy gracz musi sprawdzić inną kartę Misji.
- **Charyzmatyczny Przywódca** (Strong Leader) – karta jednorazowa – gracz, któremu Przywódca przekazał tę kartę, może użyć jej, aby przejąć Przywództwo. Użycie tej karty należy ogłosić, zanim aktualny Przywódca podejmie jakiekolwiek działania (takie jak np. dobranie kart Intrygi, wyznaczenie składu Drużyny). Kiedy karta Charyzmatyczny Przywódca zostaje zagrana, następna taka karta może zostać zagrana dopiero po najbliższym Głosowaniu.
- **Pod obserwacją** (In the Spotlight) – karta jednorazowa – gracz, któremu Przywódca przekazał tę kartę, może zmusić innego gracza, aby ten zagrał kartę Misji jawnie. Gracz musi ogłosić użycie tej karty oraz wskazać gracza będącego celem jej działania, zanim gracze wchodzący w skład Drużyny wybiorą karty Misji.
- **Przejmujesz odpowiedzialność** (Take Responsibility) – karta jednorazowa – gracz, któremu Przywódca przekazał tę kartę, musi wziąć kartę Intrygi leżącą przed innym graczem.

### Talia (15 kart)

| Karta | Ilość | Efekt | Moment zagrania |
|---|---|---|---|
| Overheard Conversation (Jesteś naszym okiem i uchem) | 2 | Właściciel obejrzy kartę Tożsamości jednego z graczy siedzących bezpośrednio obok (wybiera lewego lub prawego). | Natychmiast |
| Opinion Maker (Liczymy się z twoim zdaniem) | 2 | Właściciel wybiera i ujawnia głos przed pozostałymi do końca gry; dwóch takich graczy ujawnia głosy równocześnie. | Stały efekt |
| Establish Confidence (To kwestia zaufania) | 1 | Lider ujawnia swoją kartę Tożsamości wybranemu graczowi. Jedyna karta, którą Lider zagrywa na siebie. | Natychmiast |
| Open Up (Tylko tobie zaufam) | 1 | Właściciel ujawnia swoją kartę Tożsamości wybranemu graczowi (może nim być Lider). | Natychmiast |
| No Confidence (Nie ufajcie mu) | 3 | Odrzuca zatwierdzone głosowanie: liczy się jako nieudane (licznik odrzuceń +1), żeton Lidera idzie do następnego gracza, faza zbierania drużyny jest powtarzana. | Po zatwierdzeniu drużyny |
| Keeping a Close Eye on You (Mam cię na oku) | 2 | Właściciel podgląda jedną zagraną kartę Misji. Jedna karta Misji może być podejrzana przez kilku graczy (odstępstwo od fizycznej gry), ale nie dwa razy przez tego samego. | Zaraz po zagraniu kart Misji (bez wcześniejszej zapowiedzi) |
| Strong Leader (Charyzmatyczny Przywódca) | 2 | Właściciel przejmuje Przywództwo. Następny Strong Leader może zostać zagrany dopiero po najbliższym Głosowaniu. | Przed jakimkolwiek działaniem Lidera (także przed dobraniem kart Intrygi) |
| In the Spotlight (Pod obserwacją) | 1 | Wskazany gracz musi zagrać kartę Misji jawnie. Właściciel ogłasza kartę i cel. | Przed wyborem kart Misji |
| Take Responsibility (Przejmujesz odpowiedzialność) | 1 | Właściciel musi wziąć kartę Intrygi leżącą przed innym graczem. | Wg tekstu karty (nieokreślone, patrz uwagi) |

Karty "natychmiastowe" (Overheard Conversation, Establish Confidence, Open Up) działają od razu po rozdaniu, przed wyborem drużyny.

### Ile kart dobiera Lider

- 5-6 graczy: 1 karta, 7-8 graczy: 2 karty, 9-10 graczy: 3 karty (na rundę).
- Zawsze gra cała talia 15 kart (nie ma podzbioru dla 5-6 graczy; wcześniejszy pomysł z podziałem z pierwszego wydania odpadł).

### Przebieg rundy z kartami (zmiany względem podstawki)

1. Wybór Lidera (tu może paść Strong Leader, przed jakimkolwiek działaniem Lidera).
2. Lider dobiera karty intryg i rozdaje je innym graczom.
3. Natychmiastowe karty się rozpatrują.
4. Wybór drużyny, dyskusja, głosowanie (Opinion Maker ujawnia głos pierwszy).
5. Po zatwierdzeniu drużyny: No Confidence (unieważnia zatwierdzenie, następny lider, licznik odrzuceń +1).
6. Przed misją: In the Spotlight. Po zagraniu kart misji: Keeping a Close Eye on You.

### Uwagi do implementacji

- Wszystkie efekty informacyjne (podgląd roli, podgląd karty misji) muszą być widokiem per gracz, nigdy wspólną odpowiedzią; push SignalR nadal bez danych (`ResistanceChanged`), klient pobiera swój widok. Podgląd karty misji wymaga, by serwer pamiętał `MissionCard` do końca rundy (dziś jest nigdy niesłana, ale zapisana).
- Potrzebne nowe fazy/okna: dobranie i rozdanie kart po wyborze Lidera, okno na zagranie No Confidence po wyniku głosowania (dziś głosowanie rozstrzyga się od razu przy ostatnim głosie, więc trzeba dodać krótką fazę lub timer) i okno na Strong Leader. **Decyzja: okna kończą się jawnym potwierdzeniem**, bez timera: faza trwa, aż każdy gracz, który ma jeszcze zagrywalną kartę (No Confidence, Strong Leader), zagra ją albo potwierdzi "nie zagrywam"; gracze bez takiej karty nie potwierdzają nic. Potwierdzenia nie ujawniają, kto ma kartę (inaczej wiedzielibyśmy to po tym, kogo faza czeka): serwer musi czekać na wszystkich albo pokazywać faktyczną liczbę oczekujących bez imion; do rozstrzygnięcia przy implementacji.
- No Confidence liczy się jak odrzucona drużyna (`RejectedTeams`, piąta = wygrana Szpiegów) i przekazuje przywództwo dalej.
- Snapshot zasad (`ResistanceGame.Rules`) powinien zawierać flagę włączenia dodatku i skład talii; włączanie tylko przed otwarciem gry, jak odświeżanie zasad.
- **Decyzja: zagranie karty jest jawne** (wszyscy widzą, kto jaką kartę zagrał i na kogo), a efekt informacyjny widzi tylko jego odbiorca. Karty natychmiastowe (Overheard Conversation, Establish Confidence, Open Up) **wymagają potwierdzenia odbiorcy**, że zobaczył informację; dopiero potem gra idzie dalej.
- **Decyzja: Take Responsibility** (odstępstwo od fizycznej karty, która każe wziąć kartę "leżącą przed innym graczem"): gracz z tą kartą wskazuje **gracza**. Jedyną widoczną kartą jest zagrany Opinion Maker (pozostałe karty są jednorazowe i po zagraniu odpadają), reszta jest w ręku i ukryta. Jeśli cel ma przed sobą Opinion Makera, zabierający wybiera: weźmie go (widoczną kartę) albo wylosowaną przez serwer kartę z ręki celu. Jeśli cel nie ma Opinion Makera, serwer automatycznie losuje kartę z ręki. Nikt nie widzi kart w ręku innych graczy, więc zabierający nie wybiera konkretnej karty z ręki i widzi tylko zabraną. Zabrana karta działa u nowego właściciela: Opinion Maker obowiązuje go natychmiast (od następnego głosowania ujawnia głos pierwszy), pozostałe karty może zagrać w swoim oknie. **Decyzja:** liczba kart Intrygi w ręku każdego gracza jest jawna dla wszystkich (same karty pozostają ukryte). Cel bez kart w ręku i bez Opinion Makera: zagranie Take Responsibility jest błędem gracza i karta przepada bez skutku (aplikacja nie blokuje wyboru). Cel z samym Opinion Makerem (pusta ręka): zabierany automatycznie.
- **Decyzja: Keeping a Close Eye** (odstępstwo od fizycznej karty, wg której pojedynczą kartę Misji może sprawdzić tylko jeden gracz): tę samą kartę Misji może podejrzeć kilku graczy, bo mogą chcieć sprawdzić, czy ten, kto ją obwieścił, nie skłamał. Ten sam gracz nie podgląda dwa razy tej samej karty (np. mając drugą, zabraną kartę Mam cię na oku). Każdy widzi tylko własny podgląd.
- **Decyzja: Establish Confidence** liczy się do kart dobranych przez Lidera (1/2/3). Lider zagrywa ją na siebie, więc nie rozdaje jej innym: z N dobranych kart rozdaje pozostałe, a ta jest rozpatrywana od razu (Lider wybiera gracza, któremu ujawnia swoją kartę Tożsamości; gracz potwierdza).
- **Decyzja:** jeden gracz nie zagra dwóch kart naraz, bo Strong Leader, No Confidence i In the Spotlight kończą swoje okna (po zagraniu faza idzie dalej); po Strong Leaderze następny można zagrać dopiero po najbliższym Głosowaniu.
- Poza zakresem: Hidden Agenda, Hostile Intent (poza modułem Inquisitor, patrz niżej).


## The Resistance: niezaimplementowane dodatki i warianty (stan na 2026-11-05)

Źródło: skonsolidowana instrukcja (Indie Boards & Cards, "The Resistance consolidated rules with Hostile Intent, Hidden Agenda and The Plot Thickens, v1.1", 25 stron, https://gamers-hq.de/media/pdf/78/dd/0a/The_Resisance_consolidated_rules_v1-1.pdf) + wyszukiwania (Hidden Agenda = Assassin, Defector, Trapper; Hostile Intent = Inquisitor, Hunter, Plot Thickens; Rogue Agent, Sergeant i Reverser pochodzą z promocji/innych dodatków, których nie udało się jednoznacznie przypisać). Tekst PDF wyciągnięty ręcznie, więc **brakuje**: skryptów "Spies Reveal" (obrazki), części tabeli składu postaci dla Huntera (wyliczona poniżej z zasad), jednego zdania w Rogue Agent (patrz pytania otwarte). Tam, gdzie to ważne, trzeba sprawdzić fizyczną instrukcję przed implementacją.

Zaimplementowane: podstawka, Plot Thickens, Inquisitor, Blind Spies i Reverser (patrz wyżej i niżej). Plot Thickens: PDF mówi, że w grze 5-6 graczy używa się 7 kart z talii; **aplikacja celowo gra zawsze całą talią 15 kart** (decyzja wyżej, nie zmieniać). PDF mówi też: dodatki można łączyć, ale zaleca się grać każdy osobno i bez kart Intryg.

### Zasady wspólne dla dodatków

- **Karty Lojalności zamiast kart Postaci.** Każdy wariant, w którym gracz pokazuje innemu "kartę Tożsamości" (Overheard Conversation, Establish Confidence, Open Up z Plot Cards, Inquisitor, Hunter: Investigation), po połączeniu z dodatkiem z nowymi postaciami (Assassin, Rogue, Reverser, Defector, Hunter) pokazuje **lojalność** (szpieg albo ruch oporu), a nie postać. Defector pokazuje lojalność odpowiadającą jego *obecnej* przynależności. Dziś `yourReveals` pokazuje rolę, więc po dodaniu postaci trzeba to rozdzielić (patrz fundament niżej).
- Wszystkie dodatki włączane w snapshocie zasad (`ResistanceRules`, jak `PlotCardsEnabled`), tylko przed otwarciem gry, z walidacją zgodności.

### Moduły (zasady skrótowo, PL)

**Assassin / Commander (Hidden Agenda)**: zamiast jednej postaci Ruchu Oporu i jednej Szpiega wchodzą **Commander** (Opór, zna szpiegów) i **Assassin** (szpieg). Po 3 udanych misjach szpiedzy mają ostatnią szansę: Assassin (po naradzie, bez pokazywania kart) wskazuje, kto jest Commanderem; trafienie = wygrana Szpiegów, pudło = wygrana Oporu. 3 nieudane misje: Szpiedzy wygrywają jak zwykle. Opcjonalne postacie (dowolna kombinacja, najlepiej po jednej naraz):
- **Body Guard** (Opór): zna Commandera od początku. Przy 5 graczach trzeba dodać Deep Cover albo False Commander.
- **Deep Cover** (szpieg): Commander go **nie** widzi.
- **Blind Spy** (szpieg): nie zna innych szpiegów i oni nie znają jego.
- **False Commander** (szpieg): dla Body Guarda wygląda jak Commander.

**Trapper (Hidden Agenda)**: Lider dobiera do drużyny **jednego dodatkowego gracza** (jeden żeton więcej, rozmiar drużyny +1; głosowanie bez zmian). Po zagraniu kart misji Lider wybiera jedną zagraną kartę, **podgląda ją i odkłada**: nie wlicza się do wyniku. Może o niej mówić, nie może jej pokazać. (Fizycznie przy 8+ graczach dochodzi dodatkowy zestaw kart i żeton.)

**Defector (Hidden Agenda)**: dwie postacie Defector (jedna Opór, jedna Szpieg) zastępują po jednej standardowej. Talia 5 kart zmiany (3x No Change, 2x Change Allegiance). Na początku **3. rundy** i każdej kolejnej odkrywa się jedną kartę; przy Change Allegiance obaj Defectorzy zmieniają przynależność (szpieg-Defector staje się Oporem i odwrotnie), dotyczy to wszystkiego: warunków zwycięstwa i reguł kart misji (Opór musi grać Sukces). Karty postaci się nie zamieniają ani nie pokazują. Szpieg-Defector w fazie "Spies reveal" nie otwiera oczu, tylko pokazuje kciuk, żeby inni szpiedzy go znali. Wariant: Defectorzy znają się nawzajem (zalecany dla dużych grup). Przy łączeniu z odkrywaniem kart pokazują lojalność aktualną.

**Rogue Agent**: dwie postacie Rogue (Rogue-Opór i/lub Rogue-Szpieg), żeton Watch, 6 kart "Rogue Success". Rogue-Szpieg w fazie reveal nie otwiera oczu: nie zna szpiegów i oni nie znają jego (z Commanderem też się mu nie ujawnia). Po zatwierdzeniu drużyny Lider daje **Watch** jednemu członkowi drużyny (5-6 graczy: nie przy dwóch pierwszych zatwierdzonych misjach). Każdy członek drużyny dostaje trzy karty (Success, Fail, Rogue Success); Rogue Success może zagrać tylko Rogue-Opór i liczy się jak Success; jeśli Watch ma Rogue-Opór, nie może jej zagrać. **Rogue-Opór wygrywa sam**, gdy trzecia udana misja zawiera jego Rogue Success i zagrał ją też w co najmniej jednej wcześniejszej misji. **Rogue-Szpieg wygrywa sam**, gdy w trzeciej nieudanej misji zagrał Fail i zagrał Fail w co najmniej jednej wcześniejszej. Z Commanderem: faza zamachu pomijana, jeśli wygrał Rogue. Z Hunterem: faza Accusation pomijana, jeśli wygrał Rogue (jeśli nie, przebiega normalnie i Rogue-Opór może dostać drugą szansę).

**Sergeant**: żeton dla jednego gracza w drużynie. Lider **przed głosowaniem** wyznacza Sierżanta spośród członków drużyny (nie siebie; jawne). Po tym jak wszyscy zagrali kartę misji (rozłożone zakryte przed graczami, wiadomo, kto którą zagrał), Sierżant **może** kazać innemu graczowi zamienić karty (niezagrana karta staje się zagraną), po czym **podgląda kartę, którą ten gracz zagrał pierwotnie** (więc wie, czy zamiana pomogła, czy zaszkodziła). Z Plot Cards: Sierżant działa przed Keeping a Close Eye; nie może zamienić kart zagranych jawnie przez In the Spotlight. **Niezgodny** z dodatkami używającymi specjalnych kart misji (Rogue; Hunter przy 7+; z tego samego powodu Reverser).

**Inquisitor (Hostile Intent)** - ZAIMPLEMENTOWANY (opcjonalny moduł, domyślnie wyłączony, włączany przed startem gry jak karty Intryg): żeton Inquisitora na starcie dostaje gracz po prawej od pierwszego Lidera (jawnie). Zaraz po rozstrzygnięciu misji **2., 3. i 4.** (po oknie podglądu kart Intryg, przed "Dalej") posiadacz wybiera gracza, który przekazuje mu kartę (lojalność), Inquisitor ją ogląda (może o niej mówić, nie pokazuje), a badany **dostaje żeton**. Użyty tylko 3 razy; gracz, który miał żeton (także pierwszy posiadacz), nie może zostać zbadany. Najlepszy dla 7+ graczy, ale dozwolony od 5. Decyzje implementacyjne: "po prawej" to gracz o jedno miejsce wcześniej w kolejności siedzenia (przywództwo przechodzi na następnego); aplikacja zna lojalność z roli, więc badany niczego nie wybiera ani nie potwierdza; żeton przechodzi na badanego od razu, a badający potwierdza "Widziałem/am"; gra nie ma Inquisition po misji, która kończy grę, ani po misji 5; brak obejścia dla nieobecnego posiadacza (host zwalnia miejsce). Szczegóły: `resistance-implementation.md`.

**Reverser** - ZAIMPLEMENTOWANY (opcjonalny moduł, domyślnie wyłączony; decyzje: host wybiera, które strony dostają Reversera (domyślnie obie, po jednym na stronę; można tylko Opór albo tylko Szpiegów); w misji 4 z dwiema wymaganymi Porażkami jedna Porażka + jedno Odwrócenie to **porażka**, bo sama Porażka nie oblałaby misji; po misji pokazywane są wszystkie zagrane karty (także Odwrócenie) bez informacji, kto je zagrał; zakaz łączenia z Rogue, Hunterem 7+, Sierżantem i Trapperem dopiero gdy te moduły powstaną; szczegóły w `resistance-implementation.md`): jedna lub dwie postacie Reverser (Opór i/lub Szpieg), 6 kart "Reverse". Każdy członek drużyny dostaje Success, Fail, Reverse; Reverse zagrywa tylko Reverser; **Reverser-Szpieg nie może zagrać Fail** (tylko Success albo Reverse). Wynik: misja udana, gdy same Success i 0 lub 2 Reverse, albo gdy jest Fail i dokładnie 1 Reverse; nieudana, gdy jest Fail i 0 lub 2 Reverse, albo same Success i 1 Reverse. Przykład: Success, Success, Fail, Reverse = udana. Opcja: Reverser-Szpieg nie ujawnia się w fazie reveal - ZAIMPLEMENTOWANA jako ustawienie hosta „Reverser-szpieg zna pozostałych szpiegów” (domyślnie włączone; po wyłączeniu Reverser-szpieg nie zna szpiegów i oni nie znają jego, symetrycznie jak przy stole).

**Hunter (Hostile Intent)**: obie strony muszą wskazać Szefa drugiej. Postacie (10 kart: 4x Chief, 2x Hunter, Dummy Agent, Coordinator, Deep Agent, Pretender). Skład (z zasad: zawsze res Chief, res Hunter, spy Chief, spy Hunter, reszta standardowi): 5 graczy: + 1 std Opór; 6: + 2 std Opór; 7: + 2 std Opór, + 1 std szpieg; 8: **drugi** res Chief, + 2 std Opór, + 1 std szpieg; 9: 2 res Chief, + 3 std Opór, + 1 std szpieg; 10: 2 res Chief, **drugi** spy Chief, + 3 std Opór, + 1 std szpieg (do weryfikacji z fizyczną instrukcją; sprawdzić sumy 3/4/4/5/6/6 i 2/2/3/3/3/4). Karty Lojalności: 5-6 graczy "Chief" i "Not a Chief"; 7+ "spy Chief", "resistance Chief", "Not a Chief" oraz karty misji **Chief Fail**. Reveal: szpiedzy znają się i swojego Chiefa; przy 8+ res Chiefowie znają się nawzajem.
- *Team building*: Lider daje żeton **Investigator** graczowi spoza drużyny (nie sobie); jeśli drużyna zostanie zatwierdzona i misja się nie uda, ten gracz jest Investigatorem. Pomijane w misji 5.
- *Karty misji (7+)*: Chief gra tylko Success albo Chief Fail (nie zwykły Fail), Chief Fail tylko spy Chief; misja nieudana przy Fail lub Chief Fail (misja 4: dwa). Przy 5-6 graczach Chief Fail nie istnieje, Chief używa zwykłego Fail.
- *Early End Game Accusation* (raz na grę): gdy spy Chief oblał misję (5-6: zagrał Fail; 7+: zagrany Chief Fail, a w misji 4 misja musi też być nieudana), spy Hunter może się ujawnić i wskazać res Chiefa **przed Investigation**. Trafi = wygrana Szpiegów; pudło = wynik misji odwrócony na sukces, wszyscy znają spy Huntera, gra toczy się dalej. Pudło może dać 3. udaną misję: wtedy normalne Accusation (res Hunter musi wskazać spy Chiefa, fałszywe oskarżenie znów odwraca misję na porażkę).
- *Investigation*: po udanej misji bada Lider, po nieudanej Investigator, dowolnego gracza (nie siebie). Badany wybiera ze zbioru kart Lojalności tę, która mu odpowiada i oddaje badającemu: res Chief pokazuje Chief (5-6) / resistance Chief (7+), spy Chief pokazuje Chief albo spy Chief (do wyboru), wszyscy inni Not a Chief. Przy 3. udanej/3. nieudanej misji Investigation jest odroczone do po Accusation.
- *Accusation*: po 3. udanej misji res Hunter ujawnia się i wskazuje spy Chiefa; badany odpowiada szczerze; trafi: **sukces zmienia się w porażkę** (żeton odwrócony), kończy się Investigation i gra toczy się dalej z jawnym res Hunterem. Po 3. nieudanej misji spy Hunter wskazuje res Chiefa: trafi = wygrana Szpiegów, pudło = porażka zmienia się w sukces i gra toczy się dalej. Po **5. misji** fałszywe oskarżenie daje szansę drugiej stronie i tak na zmianę aż do trafienia.
- *Postacie dodatkowe*: **Dummy Agent** (Opór; badany może pokazać kartę Chiefa; oskarżony = oskarżenie fałszywe), **Coordinator** (Opór; res Chiefowie znają go od początku; badany pokazuje Not a Chief; oskarżony przez spy Huntera = wygrana Szpiegów), **Deep Agent** (szpieg; nie zna szpiegów, oni znają jego), **Pretender** (Opór; dla szpiegów wygląda jak Deep Agent), opcjonalna reguła **Blame** (raz na grę, przed głosowaniem Deep Agent ujawnia się i wskazuje Pretendera; trafi = Deep Agent i Pretender zamieniają się kartami i przynależnością, pudło = Deep Agent staje się jawny).

**Warianty**: **Targeting**: misje nie muszą iść po kolei; Lider wybiera numer misji przed wyborem drużyny (rozmiar drużyny z tej misji), głosowanie dotyczy misji i drużyny; każda misja najwyżej raz; żadna nie jest obowiązkowa; misja 5. dopiero po dwóch udanych. **Blind Spies**: pominąć krok Spies Reveal (szpiedzy się nie znają).

### Tips and risks for implementation (EN)

**Cross-cutting foundation (do this first, before any module).** Several modules each need the same three changes, so building them one by one means repeating the migration pain:
1. *Split character from allegiance.* (Done: `Character` exists, `Role` is the current allegiance, every reveal goes through `ResistanceKnowledge.Loyalty` and the Defector flips it. Still to extend: `RogueSuccess`/`ChiefFail`, `KnowsAbout`.) Today `ResistancePlayer.Role` is a two-value enum (`Resistance`/`Spy`) and `ResistanceGameViewBuilder`, `ResistanceKnowledge`, the `MustSucceed` rule and the win check all read it. Add a `Character` enum (default `Standard`) and keep `Role` as the **current allegiance** (Defector flips it). Reveals (`yourReveals`, Inquisitor, Investigation) must return *loyalty*, never the character. Append-only migration with a default, keep Domain/Contract enum names identical (`ResistanceEnumMappings`, Mapperly fails the build otherwise).
2. *Mission card* (done with Reverser): `ResistanceMissionCardKind` (`Fail` 0, `Success` 1, `Reverse` 2: the first two keep the old boolean's stored values) is stored as a nullable int, the body of `PUT mission` is `{card}` (a breaking API change, done together with the UI), and `ResistanceMissionRules.HasSucceeded` is the pure result function. Still to append: `RogueSuccess` and `ChiefFail`, and to extend `HasSucceeded` with their modifiers.
3. *Mission results become mutable.* Hunter flips an already recorded result (Accusation, Early End Game), Trapper/Sergeant change what is counted. `ResistanceMissionResults` rows need a `Reversed` flag or original-vs-final, and the 3-successes/3-failures check must run on the final value, after any pending Accusation.
Also (started as `ResistanceKnowledge.KnowsSpy` with Blind Spies; the rest below still to come): a single `KnowsAbout(viewer, target)` Domain function that returns what a viewer may see of each player (Commander sees spies minus Deep Cover; Body Guard sees Commander and False Commander alike; spies see each other minus Blind Spy/Rogue/Deep Agent, plus Defector-spy; res Chiefs see each other and the Coordinator). Today `fellowSpies` is a list of ids built for spies only; with modules it becomes viewer-dependent for both sides. Test it as a full viewer x target table per module (like `ResistanceFailureErrorsTests.Golden`), because a leak here is a security bug (CLAUDE.md: views stay hand-written).

**Conventions to follow.** New phases are *appended* to `ResistancePhase` (stored as int; `VoteResult` stays unused to keep numbers stable) and `ResistanceEnumMappings` needs them. Every new `ResistanceRules` property must be copied in `Clone`, and old stored JSON must read with defaults (modules off). Every refusal gets its own `ResistanceFailure` member and a row in `ResistanceFailureErrorsTests.Golden`. Actions end with `Succeeded()`; events carry no secrets (`ResistanceVoteCast` pattern: who, not what). `ResistanceDealer.Reset` (Restart) must clear all new state (tokens, Defector deck, used flags, Accusation state). New text needs keys in both `en.json` and `pl.json`; the phase line in `resistancePhase.ts` must never name the viewer's character. The TV tab shares the player cookie, so every new private field (`yourCommanderView`, Inquisitor result, Sergeant's seen card, ...) must be blanked for `?tv=1` like `yourReveals`. Seat takeover is safe (the private data lives on the seat), but check that a new hidden field is not stored on the connection or in client state only. E2E: roles are random, so helpers must read them from the screens (see `e2e/fixtures/resistance.ts`); modules with hidden characters (Hunter, Assassin) need a deterministic way to start a game for tests, e.g. a Development-only seeded `Random` or a test hook, otherwise specs stay flaky; do not add one that works in production.

**Per module.**
- *Blind Spies* (implemented, see `resistance-implementation.md`): trivial flag; hide `fellowSpies` for everyone. Risk: nothing server-side stops spies talking, and the Rogue/Deep Agent/Blind Spy cards already do this per character, so implement it as the same `KnowsAbout` rule, not a special case. Start here.
- *Inquisitor* (implemented, see `resistance-implementation.md`): public holder, an `Inquisition` phase after missions 2-4 (after the peek window, before "Dalej"), a private result for the examiner only, per-player `WasInquisitor`. The revealed loyalty goes through `ResistanceKnowledge.Loyalty` (done with the allegiance split), so Commander/Defector/Hunter combinations show loyalty, not character.
- *Trapper*: team size +1 touches `RequiredTeamSize`/`Rules.TeamSize` and the UI picker (it reads the same numbers from `rules`; add the bonus to the DTO field, not in the client). The Leader needs a private view of *who played which card* for the discard choice, which breaks the "mission cards are never sent" invariant on purpose: send only the one card the leader chose, and only after they choose, but they must pick without seeing (the physical rule says the Leader selects a played card face-down, then looks at it). That is a new phase between "last card played" and resolution, which today happen in one save (`ResolveMission` runs in the last `PlayMissionCard`): the same restructuring Sergeant needs; build it once as a generic "post-cards window" (`CardsPlayed` phase with pending actions). Unclear: whether the Leader, when not on the team, still discards; whether the discard counts toward Keeping a Close Eye / In the Spotlight (a face-up card discarded?). Ask before building.
- *Sergeant*: same post-cards window as Trapper, ordered Sergeant -> (Trapper?) -> Keeping a Close Eye. Needs the "other" card of each player: a resistance member also holds Fail, so a swap can force a Fail on a loyal player; model it as "swap the played kind with the unplayed one". Sergeant sees the *original* card (private, TV blank). Public: who is Sergeant. Validation: Leader cannot be Sergeant, Sergeant must be on the team, swap target must be someone else on the team, and a card played face up (In the Spotlight) cannot be swapped (`OpenMissionPlayerId` already exists). Incompatibility with Rogue/Reverser/Hunter 7+ must be enforced in rules validation.
- *Reverser* (implemented, see `resistance-implementation.md`; the notes below are the original analysis, with the ambiguity resolved as described there): `baseFail = fails >= requiredFails`, `reversed = reverseCount % 2 == 1`, `succeeded = baseFail == reversed`. **Ambiguity**: mission 4 with 7+ players (two fails needed) plus exactly one Fail and one Reverse; the text ("one or more Fail with one Reverse = success") and the formula disagree there. Ask. Spy Reverser cannot play Fail (a `MustSucceed`-like failure). The mission result shows only the outcome, never the Reverse count (or everyone learns a Reverser played). Currently the DTO shows "fail count"; with Reverser that number is misleading and itself leaks, so hide it when the module is on.
- *Rogue Agent*: the hardest hidden-info case after Hunter. A revealed Rogue Success card could expose the Rogue to everyone if the revealed cards are listed by kind; decide whether the result may show "N cards of Success type" only (suggest: count Rogue Success as Success in everything public). Individual win conditions need per-player history (`RogueSuccessMissions`, `RogueFailMissions`) kept private and never in `revealedRoles` before the end; the Watch token holder is public. 5-6 players: no Watch on the first two **approved** missions (ambiguity: approved missions or rounds 1-2; a rejected team does not start a mission). The immediate win bypasses the normal winner logic, so `Winner` needs a third value or a separate `RogueWinnerPlayerId`; UI "Game over" and `ResistanceEnumMappings` must learn it. Combination with Assassin/Hunter changes the end sequence (see below).
- *Assassin / Commander* (implemented with Body Guard, Deep Cover, Blind Spy and False Commander, see `resistance-implementation.md`): the first module that makes `GameOver` not the end. After the 3rd success the phase must be `Assassination` with `Winner` still null, and the DTO/UI must not treat "3 successes" as "Resistance won" (check every place that derives the winner from mission results; `revealedRoles` only at `GameOver`). Assassin chooses a target; the other spies discuss outside the app (no in-app vote, consistent with "discussion happens outside"). Only the Assassin acts. Role count validation: at 5 players spies = 2, one is the Assassin, so only **one** of Deep Cover / Blind Spy / False Commander fits; Resistance side: Commander, Body Guard, one standard. Body Guard at 5 needs Deep Cover or False Commander (reject other configs in `ResistanceCountRules`-style validation with `ResistanceRulesFailure`s, worded by the configuration validator). Seat takeover/restart: Commander info must survive a claim.
- *Defector* (implemented, see `resistance-implementation.md`): the shuffled switch deck is flipped at the start of rounds 3-5 in `BeginMission` (not after a rejected vote), the flips are public (decision; the PDF only says "flip one card"), the spy Defector does not open their eyes (knows no spy, is known to the spies while a spy who started as one) and the Defector who started as resistance is never known to the spies. `KnowsSpy` is pinned for 0, 1 and 2 switches; `revealedRoles` shows the final allegiance and the character; "Defectors know each other" is a rules flag (`DefectorsKnowEachOther`).
- *Hunter*: the largest module, treat it as its own project after the foundation. Needs: results mutable (above); a staged end sequence (Early End Game Accusation, Investigation, Accusation, alternating accusations after mission 5) as explicit phases with a "pending winner"; characters Chief/Hunter/Dummy/Coordinator/Deep Agent/Pretender with a viewer-dependent knowledge matrix; two kinds of loyalty cards; Investigator token; Chief Fail (7+) as another `MissionCardKind`. Keep the *Investigation* answer private to the investigator and let the investigated player **choose** the card (spy Chief may show Chief or spy Chief, Dummy Agent may bluff): this is an input from the investigated player, so the investigation needs a "waiting for the answer" step and a real endpoint, not a server-computed value. Known rule gaps: (1) alternating accusations after mission 5 can in principle loop forever; add a rule that a player already wrongly accused cannot be accused again (and document it as ours), (2) what happens when Hunters are dead-ends in a Rogue/Assassin combination, (3) the 10-player composition and the Blame swap effect on the Rogue/win checks. Recommend **not** supporting Hunter combined with Assassin (two competing end-game guesses, no rule in the PDF) and not with Sergeant.
- *Targeting*: `MissionNumber` is a linear counter used by team size, results, plot cards (`PlayedInMission`), the rejection tracker. A free choice means a chosen-mission state, a vote on mission+team, a "missions already played" set, and `RejectedTeams` semantics (per round? unspecified: ask). Constraint "5th mission only after two successes" is simple. The mission board UI assumes order; the leader-pass and plot-card draw ("at the start of each mission") need a defined meaning. Risky retrofit; do it last or not at all.

**Compatibility (suggested for validation, from the PDF).** Plot Cards combine with everything except: Sergeant must act before Keeping a Close Eye and cannot touch In the Spotlight cards. Sergeant excludes Rogue, Reverser, Hunter (7+; simplest is all Hunter). Assassin + Hunter: unsupported. Assassin is also rejected (our decision) together with Defector, Reverser and Blind Spies, both ways. Rogue + Assassin/Hunter: documented in the PDF (skip the end-guess when a Rogue won). Reverser + Rogue/Hunter/Trapper: not addressed in the PDF; Reverser + Rogue and Reverser + Hunter 7+ both add special mission cards, so recommend forbidding them at first. The PDF recommends playing each module on its own first; the UI could warn when combining.

**Suggested order.** (1) Foundation: allegiance/character split, `MissionCardKind`, pure result function, mutable results, `KnowsAbout`. (2) Blind Spies (done, together with Inquisitor). (3) Reverser (done). (4) Post-cards window, then Trapper and Sergeant. (5) Assassin/Commander with its optional characters (introduces "winner pending"; done, with the optional characters). (6) Defector (done). (7) Rogue Agent. (8) Hunter. (9) Targeting if ever wanted.

**Open questions (need the physical rulebook or a decision).**
1. Rogue Agent: one sentence in the PDF text is lost ("... mission but the[y] were not able to meet their individual victory conditions"): presumably the Rogue's team wins/loses normally; confirm.
2. ~~Reverser + two-fail mission 4 with one Fail and one Reverse~~ - decided: a failure (the formula).
3. ~~Defector: are switch-card flips public? Does anybody besides the Defectors learn of a switch?~~ - decided: the flips are public, everybody learns that a switch happened, nobody who the Defectors are.
4. Trapper: does the Leader choose the discard when not on the team; is the discarded card still reachable by Keeping a Close Eye?
5. Targeting: how do rejected teams and "no mission is required" work.
6. Hunter: loop after mission 5, and the Reveal scripts (images in the PDF).
7. Rogue Agent Watch token: "first two approved missions" vs first two rounds.
