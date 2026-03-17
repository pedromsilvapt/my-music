import {type ColorSchemeName} from 'react-native';

export const lightColors = {
    primary: '#6366f1',
    primaryDark: '#4f46e5',
    primaryLight: '#818cf8',
    onPrimary: '#ffffff',

    background: '#ffffff',
    backgroundSecondary: '#f8f9fa',
    backgroundTertiary: '#f1f3f4',

    surface: '#f8f9fa',
    surfaceSecondary: '#eef0f2',

    card: '#e8e8e8',
    cardSecondary: '#ffffff',

    cardText: '#1a1a2e',
    cardTextSecondary: '#4b5563',
    cardTextMuted: '#9ca3af',

    cardBorder: '#e0e0e0',

    text: '#1a1a2e',
    textSecondary: '#4b5563',
    textMuted: '#9ca3af',
    textInverse: '#ffffff',

    border: '#e5e7eb',
    borderSecondary: '#d1d5db',

    success: '#10b981',
    warning: '#f59e0b',
    error: '#ef4444',
    info: '#3b82f6',

    syncUpload: '#6366f1',
    syncDownload: '#3b82f6',
    syncSkip: '#9ca3af',
    syncRemove: '#ef4444',
};

export const darkColors = {
    primary: '#6366f1',
    primaryDark: '#4f46e5',
    primaryLight: '#818cf8',
    onPrimary: '#ffffff',

    background: '#0f0f1a',
    backgroundSecondary: '#1a1a2e',
    backgroundTertiary: '#252542',

    surface: '#1e1e32',
    surfaceSecondary: '#252542',

    card: '#ffffff',
    cardSecondary: '#f8f9fa',

    cardText: '#1a1a2e',
    cardTextSecondary: '#4b5563',
    cardTextMuted: '#9ca3af',

    cardBorder: '#e8e8ed',

    text: '#ffffff',
    textSecondary: '#a0a0b0',
    textMuted: '#6b6b7b',
    textInverse: '#1a1a2e',

    border: '#2a2a42',
    borderSecondary: '#3a3a52',

    success: '#10b981',
    warning: '#f59e0b',
    error: '#ef4444',
    info: '#3b82f6',

    syncUpload: '#6366f1',
    syncDownload: '#3b82f6',
    syncSkip: '#9ca3af',
    syncRemove: '#ef4444',
};

export function withAlpha(color: string, alpha: number): string {
    const hex = color.replace('#', '');
    const r = parseInt(hex.substring(0, 2), 16);
    const g = parseInt(hex.substring(2, 4), 16);
    const b = parseInt(hex.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

export function getColorsForScheme(scheme: ColorSchemeName) {
    return scheme === 'dark' ? darkColors : lightColors;
}

export const spacing = {
    xs: 4,
    sm: 8,
    md: 16,
    lg: 24,
    xl: 32,
    xxl: 48,
};

export const borderRadius = {
    sm: 4,
    md: 8,
    lg: 12,
    xl: 16,
    full: 999,
};

export const fontSize = {
    xs: 10,
    sm: 12,
    md: 14,
    lg: 16,
    xl: 18,
    xxl: 24,
    xxxl: 32,
};

export const fontWeight = {
    normal: '400' as const,
    medium: '500' as const,
    semibold: '600' as const,
    bold: '700' as const,
};

export const fontFamily = {
    monospace: 'Courier',
};

const isDark = false;
export const colors = isDark ? darkColors : lightColors;
