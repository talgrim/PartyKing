/** Summary of the rules shown in the info dialog of the Resistance page. */
export type RuleSection = {
  title: string;
  text: string;
  /** Shown as a list under the text, each with a bold lead-in. */
  items?: { name: string; text: string }[];
  /** A closing note after the list. */
  footer?: string;
};

export const RESISTANCE_RULES: RuleSection[] = [
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
    title: 'Karty Intryg (dodatek)',
    text: 'Gdy host włączy dodatek przed otwarciem gry, Lider na początku każdej misji (nie po odrzuceniu drużyny) dobiera karty Intryg: 1 przy 5-6 graczach, 2 przy 7-8, 3 przy 9-10, i rozdaje je innym graczom (karta „To kwestia zaufania” zostaje u Lidera). Liczba kart w ręku każdego gracza jest jawna, same karty są tajne, a każde zagranie karty widzą wszyscy.',
    items: [
      { name: 'Kreator opinii', text: 'trafia od razu na stół. Jego właściciel głosuje nad drużyną pierwszy, jawnie i bez zmiany; dwóch kreatorów głosuje jednocześnie.' },
      { name: 'Jesteś naszym okiem i uchem, To kwestia zaufania, Tylko tobie zaufam', text: 'zagrywa się od razu. Wskazany gracz (albo sąsiad) widzi kartę Tożsamości i potwierdza to przyciskiem.' },
      { name: 'Charyzmatyczny Przywódca', text: 'przejmuje przywództwo, zanim Lider cokolwiek zrobi.' },
      { name: 'Nie ufajcie mu', text: 'odrzuca zatwierdzoną drużynę; liczy się jak odrzucona drużyna.' },
      { name: 'Pod obserwacją', text: 'zmusza członka drużyny do zagrania karty Misji jawnie.' },
      { name: 'Przejmujesz odpowiedzialność', text: 'zabiera kartę Intrygi innemu graczowi (widocznego kreatora opinii albo losową kartę z ręki).' },
      { name: 'Mam cię na oku', text: 'po misji (przed „Dalej”) pozwala podejrzeć kartę Misji innego gracza; każdą kartę może podejrzeć tylko jeden gracz.' },
    ],
    footer: 'Gdy ktoś ma „Charyzmatycznego Przywódcę” lub „Nie ufajcie mu”, przed wyborem drużyny albo po zatwierdzeniu wszyscy potwierdzają, że nie zagrywają karty, więc nie widać, kto ją ma.',
  },
  {
    title: 'Koniec gry',
    text: 'Ruch Oporu wygrywa po 3 udanych misjach. Szpiedzy wygrywają po 3 nieudanych misjach albo gdy 5 drużyn z rzędu zostanie odrzuconych w jednej misji.',
  },
];
