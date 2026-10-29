/** Summary of the rules shown in the info dialog of the Resistance page. */
export const RESISTANCE_RULES: { title: string; text: string }[] = [
  {
    title: 'Cel gry',
    text: 'Gracze są potajemnie podzieleni na Ruch Oporu i Szpiegów. Rozgrywa się maksymalnie 5 misji, a drużyna, która pierwsza zdobędzie 3 punkty, wygrywa. Szpiedzy znają się nawzajem, Ruch Oporu nie wie, kto jest szpiegiem.',
  },
  {
    title: 'Przebieg misji',
    text: 'Lider wskazuje drużynę o wymaganym rozmiarze (może wybrać siebie). Wszyscy dyskutują, a potem jednocześnie i jawnie głosują: Zatwierdź albo Odrzuć. Remis odrzuca drużynę, a przywództwo przechodzi dalej.',
  },
  {
    title: 'Wykonanie misji',
    text: 'Każdy członek zatwierdzonej drużyny potajemnie gra kartę Sukces albo Porażka. Ruch Oporu musi zagrać Sukces, szpieg może zagrać obie. Misja kończy się porażką, jeśli padła choć jedna karta Porażka (w misji 4 przy 7 lub więcej graczach potrzebne są dwie).',
  },
  {
    title: 'Koniec gry',
    text: 'Ruch Oporu wygrywa po 3 udanych misjach. Szpiedzy wygrywają po 3 nieudanych misjach albo gdy 5 drużyn z rzędu zostanie odrzuconych w jednej misji.',
  },
];
