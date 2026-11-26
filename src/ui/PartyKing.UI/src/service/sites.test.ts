import { BRAND, currentSiteName, homeUrl, privacyUrl, switcherSites } from './sites';
import { pageTitle } from './title';

describe('the sites', () => {
  it('name this site in the language of the page, English for anything unknown', () => {
    expect(currentSiteName('en')).toBe('PartyKing');
    expect(currentSiteName('pl')).toBe('PartyKing');
    expect(currentSiteName(undefined)).toBe('PartyKing');
    expect(currentSiteName('de')).toBe('PartyKing');
  });

  it('link the brand to the Polish home page only for a Polish page', () => {
    expect(homeUrl('en')).toBe('https://talgrim.dev/');
    expect(homeUrl('pl')).toBe('https://talgrim.dev/pl/');
  });

  it('link the privacy policy on the home site, in the language of the page and at a section of it', () => {
    expect(privacyUrl('en')).toBe('https://talgrim.dev/privacy');
    expect(privacyUrl('pl')).toBe('https://talgrim.dev/pl/privacy');
    expect(privacyUrl('pl', 'partyking')).toBe('https://talgrim.dev/pl/privacy#partyking');
    expect(privacyUrl(undefined)).toBe('https://talgrim.dev/privacy');
  });

  it('list home, PartyKing and Notes in the switcher and leave out the sites that are not in it', () => {
    expect(switcherSites('en').map((site) => site.id)).toEqual(['home', 'party', 'notes']);
    expect(switcherSites('pl').map((site) => site.name)).toEqual(['Strona główna', 'PartyKing', 'Notatki']);
  });

  it('mark only this site as the current one', () => {
    expect(
      switcherSites('en')
        .filter((site) => site.current)
        .map((site) => site.id),
    ).toEqual(['party']);
  });
});

describe('the tab title', () => {
  it('is the site and talgrim.dev on the main page', () => {
    expect(pageTitle(null, 'PartyKing')).toBe(`PartyKing · ${BRAND}`);
  });

  it('puts the page first, from the specific to the general', () => {
    expect(pageTitle('Games', 'PartyKing')).toBe('Games · PartyKing · talgrim.dev');
  });
});
