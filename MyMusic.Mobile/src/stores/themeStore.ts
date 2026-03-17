import {Appearance, type ColorSchemeName} from 'react-native';
import {create} from 'zustand';

interface ThemeState {
    colorScheme: ColorSchemeName;
    setColorScheme: (scheme: ColorSchemeName) => void;
}

const initialColorScheme = Appearance.getColorScheme();

export const useThemeStore = create<ThemeState>((set) => ({
    colorScheme: initialColorScheme,
    setColorScheme: (colorScheme) => set({colorScheme}),
}));

Appearance.addChangeListener(({colorScheme}) => {
    useThemeStore.getState().setColorScheme(colorScheme);
});
