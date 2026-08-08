import i18n from 'i18next';
import {initReactI18next} from 'react-i18next';
import enAlbums from './en/albums.json';
import enArtists from './en/artists.json';
import enAudits from './en/audits.json';
import enCollection from './en/collection.json';
import enCommon from './en/common.json';
import enDevices from './en/devices.json';
import enFilters from './en/filters.json';
import enHistory from './en/history.json';
import enPlayer from './en/player.json';
import enPlaylists from './en/playlists.json';
import enPurchases from './en/purchases.json';
import enQueue from './en/queue.json';
import enSettings from './en/settings.json';
import enSharing from './en/sharing.json';
import enSongs from './en/songs.json';
import enSources from './en/sources.json';
import enWishlist from './en/wishlist.json';
import ptAlbums from './pt/albums.json';
import ptArtists from './pt/artists.json';
import ptAudits from './pt/audits.json';
import ptCollection from './pt/collection.json';
import ptCommon from './pt/common.json';
import ptDevices from './pt/devices.json';
import ptFilters from './pt/filters.json';
import ptHistory from './pt/history.json';
import ptPlayer from './pt/player.json';
import ptPlaylists from './pt/playlists.json';
import ptPurchases from './pt/purchases.json';
import ptQueue from './pt/queue.json';
import ptSettings from './pt/settings.json';
import ptSharing from './pt/sharing.json';
import ptSongs from './pt/songs.json';
import ptSources from './pt/sources.json';
import ptWishlist from './pt/wishlist.json';

export const SUPPORTED_LANGUAGES = ['en', 'pt'] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];

export const DEFAULT_LANGUAGE: SupportedLanguage = 'en';

export const LANGUAGE_OPTIONS: ReadonlyArray<{value: SupportedLanguage; label: string}> = [
    {value: 'en', label: 'English'},
    {value: 'pt', label: 'Português'},
];

export function isSupportedLanguage(value: string | undefined | null): value is SupportedLanguage {
    return value !== null && value !== undefined && (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

void i18n.use(initReactI18next).init({
    resources: {
        en: {
            common: enCommon,
            settings: enSettings,
            songs: enSongs,
            player: enPlayer,
            devices: enDevices,
            audits: enAudits,
            sources: enSources,
            playlists: enPlaylists,
            sharing: enSharing,
            queue: enQueue,
            history: enHistory,
            purchases: enPurchases,
            wishlist: enWishlist,
            albums: enAlbums,
            artists: enArtists,
            collection: enCollection,
            filters: enFilters,
        },
        pt: {
            common: ptCommon,
            settings: ptSettings,
            songs: ptSongs,
            player: ptPlayer,
            devices: ptDevices,
            audits: ptAudits,
            sources: ptSources,
            playlists: ptPlaylists,
            sharing: ptSharing,
            queue: ptQueue,
            history: ptHistory,
            purchases: ptPurchases,
            wishlist: ptWishlist,
            albums: ptAlbums,
            artists: ptArtists,
            collection: ptCollection,
            filters: ptFilters,
        },
    },
    lng: DEFAULT_LANGUAGE,
    fallbackLng: DEFAULT_LANGUAGE,
    defaultNS: 'common',
    ns: [
        'common',
        'settings',
        'songs',
        'player',
        'devices',
        'audits',
        'sources',
        'playlists',
        'sharing',
        'queue',
        'history',
        'purchases',
        'wishlist',
        'albums',
        'artists',
        'collection',
        'filters',
    ],
    interpolation: {escapeValue: false},
});

export {i18n};