import {useMemo} from 'react';
import {darkColors, lightColors, spacing, borderRadius, fontSize, fontWeight, fontFamily, withAlpha as withAlphaFn} from '../constants/theme';
import {useThemeStore} from '../stores/themeStore';

export function useTheme() {
    const colorScheme = useThemeStore((state) => state.colorScheme);

    const isDark = colorScheme === 'dark';

    const colors = useMemo(() => {
        return isDark ? darkColors : lightColors;
    }, [isDark]);

    const withAlpha = (colorKey: keyof typeof colors, alpha: number): string => {
        const color = colors[colorKey];
        return withAlphaFn(color, alpha);
    };

    const statusBarStyle = isDark ? 'light' : 'dark';

    return {
        colors,
        isDark,
        colorScheme,
        statusBarStyle,
        withAlpha,
        spacing,
        borderRadius,
        fontSize,
        fontWeight,
        fontFamily,
    };
}
