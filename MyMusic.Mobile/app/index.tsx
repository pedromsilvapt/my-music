import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React, {useEffect} from 'react';
import {ActivityIndicator, ScrollView, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import {Button, Card, ProgressBar} from '../src/components/ui';
import {getDeviceIcon} from '../src/constants/deviceIcons';
import {useTheme} from '../src/hooks/useTheme';
import {initializeConfig} from '../src/services/configService';
import {useConfigStore} from '../src/stores/configStore';
import {useSyncStore} from '../src/stores/syncStore';

export default function HomeScreen() {
    const router = useRouter();
    const {colors, fontSize, fontWeight, spacing} = useTheme();
    const {
        isLoading: uiLoading,
        setLoading,
        userName,
        deviceName,
        deviceIcon,
        isConfigured,
        repositoryPath,
        serverUrl,
        lastSyncAt,
    } = useConfigStore();
    const {isRunning, progress} = useSyncStore();

    useEffect(() => {
        const loadConfig = async () => {
            await initializeConfig();
            setLoading(false);
        };
        loadConfig();
    }, []);

    const handleStartSync = () => {
        if (!isConfigured) {
            router.push('/settings/device');
            return;
        }
        router.push('/sync');
    };

    const formatLastSync = (dateStr: string | null) => {
        if (!dateStr) return 'Never';
        const date = new Date(dateStr);
        return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], {hour: '2-digit', minute: '2-digit'});
    };

    const isLoading = uiLoading;

    if (isLoading) {
        return (
            <View style={[styles.loadingContainer, {backgroundColor: colors.backgroundSecondary}]}>
                <ActivityIndicator size="large" color={colors.primary}/>
            </View>
        );
    }

    return (
        <ScrollView style={[styles.container, {backgroundColor: colors.backgroundSecondary}]} contentContainerStyle={[styles.content, {padding: spacing.md}]}>
            <View style={[styles.header, {marginBottom: spacing.lg}]}>
                <Text style={[styles.subtitle, {fontSize: fontSize.md, color: colors.textSecondary}]}>
                    {userName ? `Welcome, ${userName}` : 'Configure your device to sync music'}
                </Text>
            </View>

            <Card>
                <View style={[styles.deviceHeader, {gap: spacing.md}]}>
                    <Ionicons name={getDeviceIcon(deviceIcon) as any} size={32} color={colors.primary}/>
                    <View style={styles.deviceInfo}>
                        <Text style={[styles.deviceName, {fontSize: fontSize.lg, fontWeight: fontWeight.semibold, color: colors.cardText}]}>{deviceName || 'No device configured'}</Text>
                        <Text style={[styles.deviceStatus, {fontSize: fontSize.sm, color: colors.cardTextSecondary}]}>
                            {isConfigured ? 'Ready to sync' : 'Not configured'}
                        </Text>
                    </View>
                    <TouchableOpacity onPress={() => router.push('/settings/device')}>
                        <Ionicons name="chevron-forward" size={24} color={colors.cardTextSecondary}/>
                    </TouchableOpacity>
                </View>

                {repositoryPath ? (
                    <View style={[styles.repositoryInfo, {gap: spacing.xs, marginTop: spacing.md, paddingTop: spacing.md, borderTopWidth: 1, borderTopColor: colors.cardBorder}]}>
                        <Ionicons name="folder-outline" size={16} color={colors.cardTextMuted}/>
                        <Text style={[styles.repositoryPath, {fontSize: fontSize.sm, color: colors.cardTextMuted}]} numberOfLines={1}>
                            {repositoryPath}
                        </Text>
                    </View>
                ) : null}
            </Card>

            <Card>
                <View style={styles.syncStatusRow}>
                    <View>
                        <Text style={[styles.syncLabel, {fontSize: fontSize.sm, color: colors.cardTextSecondary}]}>Last Sync</Text>
                        <Text style={[styles.syncValue, {fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText}]}>{formatLastSync(lastSyncAt)}</Text>
                    </View>
                    <View style={[styles.statusBadge, {gap: spacing.xs}]}>
                        <View
                            style={[styles.statusDot, {backgroundColor: isConfigured ? colors.success : colors.warning}]}/>
                        <Text style={[styles.statusText, {fontSize: fontSize.sm, color: colors.cardTextSecondary}]}>{isConfigured ? 'Online' : 'Offline'}</Text>
                    </View>
                </View>
            </Card>

            {isRunning && (
                <Card>
                    <Text style={[styles.syncProgressTitle, {fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText, marginBottom: spacing.md}]}>Sync in Progress...</Text>
                    <ProgressBar
                        progress={progress.totalFiles > 0 ? progress.processedFiles / progress.totalFiles : 0}
                        style={styles.progressBar}
                    />
                    <View style={styles.progressDetails}>
                        <View style={styles.progressStat}>
                            <Text style={[styles.progressValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.success}]}>{progress.created}</Text>
                            <Text style={[styles.progressLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Created</Text>
                        </View>
                        <View style={styles.progressStat}>
                            <Text style={[styles.progressValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.info}]}>{progress.updated}</Text>
                            <Text style={[styles.progressLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Updated</Text>
                        </View>
                        <View style={styles.progressStat}>
                            <Text style={[styles.progressValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.cardTextMuted}]}>{progress.skipped}</Text>
                            <Text style={[styles.progressLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Skipped</Text>
                        </View>
                        <View style={styles.progressStat}>
                            <Text
                                style={[styles.progressValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.syncDownload}]}>{progress.downloaded}</Text>
                            <Text style={[styles.progressLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Downloaded</Text>
                        </View>
                    </View>
                </Card>
            )}

            <Button
                title={isRunning ? 'Sync in Progress...' : 'Start Sync'}
                onPress={handleStartSync}
                disabled={isRunning}
                loading={isRunning}
                size="large"
                style={[styles.syncButton, {marginVertical: spacing.lg}]}
            />

            <View style={[styles.quickActions, {gap: spacing.md}]}>
                <TouchableOpacity
                    style={[styles.quickAction, {backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, gap: spacing.xs}]}
                    onPress={() => router.push('/history')}
                >
                    <Ionicons name="time-outline" size={24} color={colors.primary}/>
                    <Text style={[styles.quickActionText, {fontSize: fontSize.sm, color: colors.text}]}>Sync History</Text>
                </TouchableOpacity>

                <TouchableOpacity
                    style={[styles.quickAction, {backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, gap: spacing.xs}]}
                    onPress={() => router.push('/settings')}
                >
                    <Ionicons name="settings-outline" size={24} color={colors.primary}/>
                    <Text style={[styles.quickActionText, {fontSize: fontSize.sm, color: colors.text}]}>Settings</Text>
                </TouchableOpacity>
            </View>

            <View style={[styles.serverInfo, {marginTop: spacing.xl}]}>
                <Text style={[styles.serverLabel, {fontSize: fontSize.xs, color: colors.textMuted}]}>Server</Text>
                <Text style={[styles.serverUrl, {fontSize: fontSize.sm, color: colors.textSecondary}]}>{serverUrl}</Text>
            </View>
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
    loadingContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
    header: {
        marginBottom: 24,
    },
    subtitle: {
        fontSize: 14,
    },
    deviceHeader: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    deviceInfo: {
        flex: 1,
    },
    deviceName: {
        fontSize: 16,
    },
    deviceStatus: {
        fontSize: 12,
    },
    repositoryInfo: {
        flexDirection: 'row',
        alignItems: 'center',
        borderTopWidth: 1,
    },
    repositoryPath: {
        flex: 1,
        fontSize: 12,
    },
    syncStatusRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    syncLabel: {
        fontSize: 12,
    },
    syncValue: {
        fontSize: 14,
    },
    statusBadge: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    statusDot: {
        width: 8,
        height: 8,
        borderRadius: 4,
    },
    statusText: {
        fontSize: 12,
    },
    syncProgressTitle: {
        fontSize: 14,
        marginBottom: 16,
    },
    progressBar: {
        marginBottom: 16,
    },
    progressDetails: {
        flexDirection: 'row',
        justifyContent: 'space-between',
    },
    progressStat: {
        alignItems: 'center',
    },
    progressValue: {
        fontSize: 16,
    },
    progressLabel: {
        fontSize: 10,
    },
    syncButton: {
        marginVertical: 24,
    },
    quickActions: {
        flexDirection: 'row',
    },
    quickAction: {
        flex: 1,
        alignItems: 'center',
    },
    quickActionText: {
        fontSize: 12,
    },
    serverInfo: {
        marginTop: 32,
        alignItems: 'center',
    },
    serverLabel: {
        fontSize: 10,
    },
    serverUrl: {
        fontSize: 12,
    },
});
