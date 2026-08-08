import i18n from 'i18next';
import {initReactI18next} from 'react-i18next';
import enCommon from './en/common.json';
import enSettings from './en/settings.json';
import ptCommon from './pt/common.json';
import ptSettings from './pt/settings.json';

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
        en: {common: enCommon, settings: enSettings},
        pt: {common: ptCommon, settings: ptSettings},
    },
    lng: DEFAULT_LANGUAGE,
    fallbackLng: DEFAULT_LANGUAGE,
    defaultNS: 'common',
    ns: ['common', 'settings'],
    interpolation: {escapeValue: false},
});

export {i18n};