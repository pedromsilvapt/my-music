import {Stack} from 'expo-router';
import {StatusBar} from 'expo-status-bar';
import {StyleSheet} from 'react-native';
import {SafeAreaProvider, SafeAreaView} from 'react-native-safe-area-context';
import {useTheme} from '../src/hooks/useTheme';
import {useThemeStore} from '../src/stores/themeStore';

export default function RootLayout() {
    useThemeStore();
    
    const {colors, statusBarStyle} = useTheme();

    return (
        <SafeAreaProvider>
            <SafeAreaView style={[styles.container, {backgroundColor: colors.backgroundSecondary}]}>
                <StatusBar style={statusBarStyle}/>
                <Stack
                    screenOptions={{
                        headerStyle: {
                            backgroundColor: colors.backgroundSecondary,
                        },
                        headerTintColor: colors.text,
                        headerTitleStyle: {
                            fontWeight: '600',
                        },
                        contentStyle: {
                            backgroundColor: colors.backgroundSecondary,
                        },
                    }}
                >
                    <Stack.Screen
                        name="index"
                        options={{
                            title: 'MyMusic',
                        }}
                    />
                    <Stack.Screen
                        name="settings/index"
                        options={{
                            title: 'Settings',
                        }}
                    />
                    <Stack.Screen
                        name="settings/device"
                        options={{
                            title: 'Configure Device',
                        }}
                    />
                    <Stack.Screen
                        name="history/index"
                        options={{
                            title: 'Sync History',
                        }}
                    />
                    <Stack.Screen
                        name="history/[sessionId]"
                        options={{
                            title: 'Session Details',
                        }}
                    />
                    <Stack.Screen
                        name="sync/index"
                        options={{
                            title: 'Sync Options',
                        }}
                    />
                    <Stack.Screen
                        name="sync/progress"
                        options={{
                            title: 'Syncing...',
                            headerBackVisible: false,
                            gestureEnabled: false,
                        }}
                    />
                </Stack>
            </SafeAreaView>
        </SafeAreaProvider>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
    },
});
