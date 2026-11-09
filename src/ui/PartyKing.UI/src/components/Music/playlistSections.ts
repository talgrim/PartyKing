import { PlaylistSection } from '@/service/endpoints/playlists/types';
import { i18n } from '@/i18n';

export type PlaylistSectionOption = {
  section: PlaylistSection;
  /** The text of the checkbox; one more section is one more entry here. */
  label: () => string;
  /** Whether the section is ticked when the dialog opens. */
  defaultChecked: boolean;
};

/** The parts of the queue that can go into a saved playlist, in the order the dialog lists them. */
export const PLAYLIST_SECTIONS: PlaylistSectionOption[] = [
  { section: PlaylistSection.History, label: () => i18n.t('savePlaylist.sections.history'), defaultChecked: false },
  { section: PlaylistSection.Current, label: () => i18n.t('savePlaylist.sections.current'), defaultChecked: true },
  { section: PlaylistSection.Queue, label: () => i18n.t('savePlaylist.sections.queue'), defaultChecked: true },
  { section: PlaylistSection.Fallback, label: () => i18n.t('savePlaylist.sections.fallback'), defaultChecked: false },
];
