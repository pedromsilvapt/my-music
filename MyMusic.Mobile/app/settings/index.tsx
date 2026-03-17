import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React from 'react';
import {Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import {Card} from '../../src/components/ui';
import {getDeviceOutlineIcon} from '../../src/constants/deviceIcons';
import {useTheme} from '../../src/hooks/useTheme';
import {resetConfig} from '../../src/services/configService';
import {useConfigStore} from '../../src/stores/configStore';

export default function SettingsScreen() {
    const router = useRouter();
    const {colors, fontSize, fontWeight, spacing} = useTheme();
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
        <ScrollView style={[styles.container, {backgroundColor: colors.backgroundSecondary}]} contentContainerStyle={[styles.content, {padding: spacing.md}]}>
            <Card>
                <TouchableOpacity
                    style={styles.settingRow}
                    onPress={() => router.push('/settings/device')}
                >
                    <View style={[styles.settingInfo, {gap: spacing.md}]}>
                        <Ionicons name={getDeviceOutlineIcon(deviceIcon) as any} size={24} color={colors.primary}/>
                        <View style={styles.settingText}>
                            <Text style={[styles.settingLabel, {fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText}]}>Device Configuration</Text>
                            <Text style={[styles.settingValue, {fontSize: fontSize.sm, color: colors.cardTextSecondary}]} numberOfLines={1}>
                                {isConfigured ? deviceName || 'Configured' : 'Not configured'}
                            </Text>
                        </View>
                    </View>
                    <Ionicons name="chevron-forward" size={20} color={colors.cardTextMuted}/>
                </TouchableOpacity>
            </Card>

            <Text style={[styles.sectionTitle, {fontSize: fontSize.sm, fontWeight: fontWeight.semibold, color: colors.textMuted, marginTop: spacing.lg, marginBottom: spacing.sm, marginLeft: spacing.xs}]}>Server</Text>
            <Card>
                <View style={styles.settingRow}>
                    <View style={[styles.settingInfo, {gap: spacing.md}]}>
                        <Ionicons name="server-outline" size={24} color={colors.primary}/>
                        <View style={styles.settingText}>
                            <Text style={[styles.settingLabel, {fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText}]}>Server URL</Text>
                            <Text style={[styles.settingValue, {fontSize: fontSize.sm, color: colors.cardTextSecondary}]} numberOfLines={1}>{serverUrl}</Text>
                        </View>
                    </View>
                </View>
            </Card>

            <Text style={[styles.sectionTitle, {fontSize: fontSize.sm, fontWeight: fontWeight.semibold, color: colors.textMuted, marginTop: spacing.lg, marginBottom: spacing.sm, marginLeft: spacing.xs}]}>Current Setup</Text>
            <Card>
                <View style={[styles.infoRow, {paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.cardBorder}]}>
                    <Text style={[styles.infoLabel, {fontSize: fontSize.sm, color: colors.cardTextSecondary}]}>User</Text>
                    <Text style={[styles.infoValue, {fontSize: fontSize.sm, fontWeight: fontWeight.medium, color: colors.cardText}]}>{userName || 'Not set'}</Text>
                </View>
                <View style={[styles.infoRow, {paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.cardBorder}]}>
                    <Text style={[styles.infoLabel, {fontSize: fontSize.sm, color: colors.cardTextSecondary}]}>Device</Text>
                    <Text style={[styles.infoValue, {fontSize: fontSize.sm, fontWeight: fontWeight.medium, color: colors.cardText}]}>{deviceName}</Text>
                </View>
                <View style={[styles.infoRow, {paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.cardBorder}]}>
                    <Text style={[styles.infoLabel, {fontSize: fontSize.sm, color: colors.cardTextSecondary}]}>Repository</Text>
                    <Text style={[styles.infoValue, {fontSize: fontSize.sm, fontWeight: fontWeight.medium, color: colors.cardText}]} numberOfLines={2}>
                        {repositoryPath || 'Not set'}
                    </Text>
                </View>
                <View style={[styles.infoRow, {paddingVertical: spacing.sm, borderBottomWidth: 0}]}>
                    <Text style={[styles.infoLabel, {fontSize: fontSize.sm, color: colors.cardTextSecondary}]}>Device ID</Text>
                    <Text style={[styles.infoValue, {fontSize: fontSize.sm, fontWeight: fontWeight.medium, color: colors.cardText}]}>{deviceId || 'Not registered'}</Text>
                </View>
            </Card>

            <Text style={[styles.sectionTitle, {fontSize: fontSize.sm, fontWeight: fontWeight.semibold, color: colors.textMuted, marginTop: spacing.lg, marginBottom: spacing.sm, marginLeft: spacing.xs}]}>Danger Zone</Text>
            <Card>
                <TouchableOpacity style={[styles.dangerRow, {gap: spacing.md}]} onPress={handleReset}>
                    <Ionicons name="trash-outline" size={24} color={colors.error}/>
                    <Text style={[styles.dangerText, {fontSize: fontSize.md, color: colors.error, fontWeight: fontWeight.medium}]}>Reset All Configuration</Text>
                </TouchableOpacity>
            </Card>
        </ScrollView>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
    },
    content: {
        padding: 16,
    },
    sectionTitle: {
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
        flex: 1,
    },
    settingText: {
        flex: 1,
    },
    settingLabel: {
        fontSize: 14,
    },
    settingValue: {
        fontSize: 12,
        marginTop: 2,
    },
    infoRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        borderBottomWidth: 1,
    },
    infoLabel: {
        fontSize: 12,
    },
    infoValue: {
        fontSize: 12,
        maxWidth: '60%',
        textAlign: 'right',
    },
    dangerRow: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    dangerText: {
        fontSize: 14,
    },
});
