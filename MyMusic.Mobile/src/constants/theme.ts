import {Appearance} from 'react-native';

const isDark = Appearance.getColorScheme() === 'dark';

export const colors = {
    primary: '#6366f1',
    primaryDark: '#4f46e5',
    primaryLight: '#818cf8',

    background: isDark ? '#0f0f1a' : '#ffffff',
    backgroundDark: '#1a1a2e',
    surface: isDark ? '#1e1e32' : '#f8f9fa',
    surfaceDark: '#252542',

    text: isDark ? '#ffffff' : '#1a1a2e',
    textSecondary: isDark ? '#a0a0b0' : '#6b7280',
    textMuted: isDark ? '#6b6b7b' : '#9ca3af',

    border: isDark ? '#2a2a42' : '#e5e7eb',
    borderDark: '#3a3a52',

    success: '#10b981',
    warning: '#f59e0b',
    error: '#ef4444',
    info: '#3b82f6',

    syncUpload: '#6366f1',
    syncDownload: '#3b82f6',
    syncSkip: '#9ca3af',
    syncRemove: '#ef4444',
};

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