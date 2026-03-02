import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React from 'react';
import {Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import {Card} from '../../src/components/ui';
import {getDeviceOutlineIcon} from '../../src/constants/deviceIcons';
import {colors, fontSize, fontWeight, spacing} from '../../src/constants/theme';
import {resetConfig} from '../../src/services/configService';
import {useConfigStore} from '../../src/stores/configStore';

export default function SettingsScreen() {
    const router = useRouter();
    const {serverUrl, deviceName, userName, deviceId, repositoryPath, isConfigured, deviceIcon} = useConfigStore();

    const handleReset = () => {
        Alert.alert(
            'Reset Configuration',
            'This will clear all your settings. Are you sure?',
            [
                {text: 'Cancel', style: 'cancel'},
                {
                    text: 'Reset',
                    style: 'destructive',
                    onPress: async () => {
                        await resetConfig();
                    }
                },
            ]
        );
    };

    return (
        <ScrollView style={styles.container} contentContainerStyle={styles.content}>
            <Card>
                <TouchableOpacity
                    style={styles.settingRow}
                    onPress={() => router.push('/settings/device')}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name={getDeviceOutlineIcon(deviceIcon) as any} size={24} color={colors.primary}/>
                        <View style={styles.settingText}>
                            <Text style={styles.settingLabel}>Device Configuration</Text>
                            <Text style={styles.settingValue} numberOfLines={1}>
                                {isConfigured ? deviceName || 'Configured' : 'Not configured'}
                            </Text>
                        </View>
                    </View>
                    <Ionicons name="chevron-forward" size={20} color={colors.textMuted}/>
                </TouchableOpacity>
            </Card>

            <Text style={styles.sectionTitle}>Server</Text>
            <Card>
                <View style={styles.settingRow}>
                    <View style={styles.settingInfo}>
                        <Ionicons name="server-outline" size={24} color={colors.primary}/>
                        <View style={styles.settingText}>
                            <Text style={styles.settingLabel}>Server URL</Text>
                            <Text style={styles.settingValue} numberOfLines={1}>{serverUrl}</Text>
                        </View>
                    </View>
                </View>
            </Card>

            <Text style={styles.sectionTitle}>Current Setup</Text>
            <Card>
                <View style={styles.infoRow}>
                    <Text style={styles.infoLabel}>User</Text>
                    <Text style={styles.infoValue}>{userName || 'Not set'}</Text>
                </View>
                <View style={styles.infoRow}>
                    <Text style={styles.infoLabel}>Device</Text>
                    <Text style={styles.infoValue}>{deviceName}</Text>
                </View>
                <View style={styles.infoRow}>
                    <Text style={styles.infoLabel}>Repository</Text>
                    <Text style={styles.infoValue} numberOfLines={2}>
                        {repositoryPath || 'Not set'}
                    </Text>
                </View>
                <View style={styles.infoRow}>
                    <Text style={styles.infoLabel}>Device ID</Text>
                    <Text style={styles.infoValue}>{deviceId || 'Not registered'}</Text>
                </View>
            </Card>

            <Text style={styles.sectionTitle}>Danger Zone</Text>
            <Card>
                <TouchableOpacity style={styles.dangerRow} onPress={handleReset}>
                    <Ionicons name="trash-outline" size={24} color={colors.error}/>
                    <Text style={styles.dangerText}>Reset All Configuration</Text>
                </TouchableOpacity>
            </Card>
        </ScrollView>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.backgroundDark,
    },
    content: {
        padding: spacing.md,
    },
    sectionTitle: {
        fontSize: fontSize.sm,
        fontWeight: fontWeight.semibold,
        color: colors.textMuted,
        marginTop: spacing.lg,
        marginBottom: spacing.sm,
        marginLeft: spacing.xs,
        textTransform: 'uppercase',
        letterSpacing: 1,
    },
    settingRow: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    settingInfo: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.md,
        flex: 1,
    },
    settingText: {
        flex: 1,
    },
    settingLabel: {
        fontSize: fontSize.md,
        fontWeight: fontWeight.medium,
        color: colors.text,
    },
    settingValue: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
        marginTop: 2,
    },
    infoRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        paddingVertical: spacing.sm,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
    },
    infoLabel: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
    },
    infoValue: {
        fontSize: fontSize.sm,
        color: colors.text,
        fontWeight: fontWeight.medium,
        maxWidth: '60%',
        textAlign: 'right',
    },
    dangerRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.md,
    },
    dangerText: {
        fontSize: fontSize.md,
        color: colors.error,
        fontWeight: fontWeight.medium,
    },
});