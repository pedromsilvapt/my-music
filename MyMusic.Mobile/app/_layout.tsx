import {Stack} from 'expo-router';
import {StatusBar} from 'expo-status-bar';
import {StyleSheet, View} from 'react-native';
import {colors} from '../src/constants/theme';

export default function RootLayout() {
    return (
        <View style={styles.container}>
            <StatusBar style="light"/>
            <Stack
                screenOptions={{
                    headerStyle: {
                        backgroundColor: colors.backgroundDark,
                    },
                    headerTintColor: colors.text,
                    headerTitleStyle: {
                        fontWeight: '600',
                    },
                    contentStyle: {
                        backgroundColor: colors.backgroundDark,
                    },
                }}
            >
                <Stack.Screen
                    name="index"
                    options={{
                        title: 'MyMusic',
                        headerLargeTitle: true,
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
                    name="sync/progress"
                    options={{
                        title: 'Syncing...',
                        headerBackVisible: false,
                        gestureEnabled: false,
                    }}
                />
            </Stack>
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.backgroundDark,
    },
});