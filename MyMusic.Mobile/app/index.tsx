import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React, {useEffect} from 'react';
import {ActivityIndicator, ScrollView, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import {Button, Card, ProgressBar} from '../src/components/ui';
import {getDeviceIcon} from '../src/constants/deviceIcons';
import {colors, fontSize, fontWeight, spacing} from '../src/constants/theme';
import {initializeConfig} from '../src/services/configService';
import {useConfigStore} from '../src/stores/configStore';
import {useSyncStore} from '../src/stores/syncStore';

export default function HomeScreen() {
    const router = useRouter();
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
            <View style={styles.loadingContainer}>
                <ActivityIndicator size="large" color={colors.primary}/>
            </View>
        );
    }

    return (
        <ScrollView style={styles.container} contentContainerStyle={styles.content}>
            <View style={styles.header}>
                <Text style={styles.subtitle}>
                    {userName ? `Welcome, ${userName}` : 'Configure your device to sync music'}
                </Text>
            </View>

            <Card>
                <View style={styles.deviceHeader}>
                    <Ionicons name={getDeviceIcon(deviceIcon) as any} size={32} color={colors.primary}/>
                    <View style={styles.deviceInfo}>
                        <Text style={styles.deviceName}>{deviceName || 'No device configured'}</Text>
                        <Text style={styles.deviceStatus}>
                            {isConfigured ? 'Ready to sync' : 'Not configured'}
                        </Text>
                    </View>
                    <TouchableOpacity onPress={() => router.push('/settings/device')}>
                        <Ionicons name="chevron-forward" size={24} color={colors.textSecondary}/>
                    </TouchableOpacity>
                </View>

                {repositoryPath ? (
                    <View style={styles.repositoryInfo}>
                        <Ionicons name="folder-outline" size={16} color={colors.textMuted}/>
                        <Text style={styles.repositoryPath} numberOfLines={1}>
                            {repositoryPath}
                        </Text>
                    </View>
                ) : null}
            </Card>

            <Card>
                <View style={styles.syncStatusRow}>
                    <View>
                        <Text style={styles.syncLabel}>Last Sync</Text>
                        <Text style={styles.syncValue}>{formatLastSync(lastSyncAt)}</Text>
                    </View>
                    <View style={styles.statusBadge}>
                        <View
                            style={[styles.statusDot, {backgroundColor: isConfigured ? colors.success : colors.warning}]}/>
                        <Text style={styles.statusText}>{isConfigured ? 'Online' : 'Offline'}</Text>
                    </View>
                </View>
            </Card>

            {isRunning && (
                <Card>
                    <Text style={styles.syncProgressTitle}>Sync in Progress...</Text>
                    <ProgressBar
                        progress={progress.totalFiles > 0 ? progress.processedFiles / progress.totalFiles : 0}
                        style={styles.progressBar}
                    />
                    <View style={styles.progressDetails}>
                        <View style={styles.progressStat}>
                            <Text style={[styles.progressValue, {color: colors.success}]}>{progress.created}</Text>
                            <Text style={styles.progressLabel}>Created</Text>
                        </View>
                        <View style={styles.progressStat}>
                            <Text style={[styles.progressValue, {color: colors.info}]}>{progress.updated}</Text>
                            <Text style={styles.progressLabel}>Updated</Text>
                        </View>
                        <View style={styles.progressStat}>
                            <Text style={[styles.progressValue, {color: colors.textMuted}]}>{progress.skipped}</Text>
                            <Text style={styles.progressLabel}>Skipped</Text>
                        </View>
                        <View style={styles.progressStat}>
                            <Text
                                style={[styles.progressValue, {color: colors.syncDownload}]}>{progress.downloaded}</Text>
                            <Text style={styles.progressLabel}>Downloaded</Text>
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
                style={styles.syncButton}
            />

            <View style={styles.quickActions}>
                <TouchableOpacity
                    style={styles.quickAction}
                    onPress={() => router.push('/history')}
                >
                    <Ionicons name="time-outline" size={24} color={colors.primary}/>
                    <Text style={styles.quickActionText}>Sync History</Text>
                </TouchableOpacity>

                <TouchableOpacity
                    style={styles.quickAction}
                    onPress={() => router.push('/settings')}
                >
                    <Ionicons name="settings-outline" size={24} color={colors.primary}/>
                    <Text style={styles.quickActionText}>Settings</Text>
                </TouchableOpacity>
            </View>

            <View style={styles.serverInfo}>
                <Text style={styles.serverLabel}>Server</Text>
                <Text style={styles.serverUrl}>{serverUrl}</Text>
            </View>
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
    loadingContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: colors.backgroundDark,
    },
    header: {
        marginBottom: spacing.lg,
    },
    subtitle: {
        fontSize: fontSize.md,
        color: colors.textSecondary,
    },
    deviceHeader: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.md,
    },
    deviceInfo: {
        flex: 1,
    },
    deviceName: {
        fontSize: fontSize.lg,
        fontWeight: fontWeight.semibold,
        color: colors.text,
    },
    deviceStatus: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
    },
    repositoryInfo: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.xs,
        marginTop: spacing.md,
        paddingTop: spacing.md,
        borderTopWidth: 1,
        borderTopColor: colors.border,
    },
    repositoryPath: {
        flex: 1,
        fontSize: fontSize.sm,
        color: colors.textMuted,
    },
    syncStatusRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    syncLabel: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
    },
    syncValue: {
        fontSize: fontSize.md,
        fontWeight: fontWeight.medium,
        color: colors.text,
    },
    statusBadge: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.xs,
    },
    statusDot: {
        width: 8,
        height: 8,
        borderRadius: 4,
    },
    statusText: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
    },
    syncProgressTitle: {
        fontSize: fontSize.md,
        fontWeight: fontWeight.medium,
        color: colors.text,
        marginBottom: spacing.md,
    },
    progressBar: {
        marginBottom: spacing.md,
    },
    progressDetails: {
        flexDirection: 'row',
        justifyContent: 'space-between',
    },
    progressStat: {
        alignItems: 'center',
    },
    progressValue: {
        fontSize: fontSize.lg,
        fontWeight: fontWeight.bold,
    },
    progressLabel: {
        fontSize: fontSize.xs,
        color: colors.textMuted,
    },
    syncButton: {
        marginVertical: spacing.lg,
    },
    quickActions: {
        flexDirection: 'row',
        gap: spacing.md,
    },
    quickAction: {
        flex: 1,
        backgroundColor: colors.surface,
        borderRadius: 12,
        padding: spacing.md,
        alignItems: 'center',
        gap: spacing.xs,
    },
    quickActionText: {
        fontSize: fontSize.sm,
        color: colors.text,
    },
    serverInfo: {
        marginTop: spacing.xl,
        alignItems: 'center',
    },
    serverLabel: {
        fontSize: fontSize.xs,
        color: colors.textMuted,
    },
    serverUrl: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
    },
});