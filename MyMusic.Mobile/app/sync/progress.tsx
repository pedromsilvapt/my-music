import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React, {useEffect, useState} from 'react';
import {ActivityIndicator, StyleSheet, Text, View} from 'react-native';
import {Button, ProgressBar} from '../../src/components/ui';
import {colors, fontSize, fontWeight, spacing} from '../../src/constants/theme';
import {runSync} from '../../src/services/syncService';
import {useConfigStore} from '../../src/stores/configStore';
import {useSyncStore} from '../../src/stores/syncStore';

export default function SyncProgressScreen() {
    const router = useRouter();
    const {progress, startSync, updateProgress, setError, completeSync, reset, setOptions} = useSyncStore();
    const {isConfigured, deviceId} = useConfigStore();
    const [error, setSyncError] = useState<string | null>(null);
    const [isSyncing, setIsSyncing] = useState(true);
    const [startTime] = useState(Date.now());

    useEffect(() => {
        return () => {
            setOptions({force: false, dryRun: false, autoConfirm: false});
        };
    }, []);

    useEffect(() => {
        if (!isConfigured || !deviceId) {
            setSyncError('Please configure your device first');
            setIsSyncing(false);
            return;
        }

        startSync({});

        const doSync = async () => {
            try {
                const result = await runSync((prog) => {
                    updateProgress(prog);
                });

                completeSync();

                router.replace(`/history/${result.sessionId}`);
            } catch (err: any) {
                console.error('Sync failed:', err);
                setSyncError(err.message || 'Sync failed');
                setError(err.message || 'Sync failed');
            } finally {
                setIsSyncing(false);
            }
        };

        doSync();
    }, []);

    const formatElapsedTime = () => {
        const elapsed = Math.floor((Date.now() - startTime) / 1000);
        const mins = Math.floor(elapsed / 60);
        const secs = elapsed % 60;
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    };

    const getPhaseLabel = () => {
        switch (progress.phase) {
            case 'scanning':
                return 'Scanning files...';
            case 'upload':
                return 'Uploading files...';
            case 'server':
                return 'Processing server actions...';
            case 'completing':
                return 'Finalizing sync...';
            case 'completed':
                return 'Sync complete!';
            case 'error':
                return 'Error occurred';
            default:
                return 'Preparing...';
        }
    };

    const getProgressValue = () => {
        if (progress.totalFiles === 0) return 0;
        return progress.processedFiles / progress.totalFiles;
    };

    if (error || progress.phase === 'error') {
        return (
            <View style={styles.container}>
                <View style={styles.errorContainer}>
                    <Ionicons name="alert-circle" size={64} color={colors.error}/>
                    <Text style={styles.errorTitle}>Sync Failed</Text>
                    <Text style={styles.errorMessage}>{error || progress.errorMessage}</Text>
                    <Button
                        title="Go Back"
                        onPress={() => {
                            reset();
                            router.back();
                        }}
                        style={styles.errorButton}
                    />
                </View>
            </View>
        );
    }

    return (
        <View style={styles.container}>
            <View style={styles.content}>
                <ActivityIndicator size="large" color={colors.primary}/>
                <Text style={styles.phaseText}>{getPhaseLabel()}</Text>

                {progress.totalFiles > 0 && (
                    <>
                        <ProgressBar
                            progress={getProgressValue()}
                            height={10}
                            style={styles.progressBar}
                        />
                        <Text style={styles.progressText}>
                            {progress.processedFiles} / {progress.totalFiles} files
                        </Text>
                    </>
                )}

                {progress.currentFile && (
                    <Text style={styles.currentFile} numberOfLines={1}>
                        {progress.currentFile.split('/').pop()}
                    </Text>
                )}

                <View style={styles.statsContainer}>
                    <View style={styles.stat}>
                        <Text style={[styles.statValue, {color: colors.success}]}>↑ {progress.created}</Text>
                    </View>
                    <View style={styles.stat}>
                        <Text style={[styles.statValue, {color: colors.info}]}>↑ {progress.updated}</Text>
                    </View>
                    <View style={styles.stat}>
                        <Text style={[styles.statValue, {color: colors.textMuted}]}>- {progress.skipped}</Text>
                    </View>
                    <View style={styles.stat}>
                        <Text style={[styles.statValue, {color: colors.syncDownload}]}>↓ {progress.downloaded}</Text>
                    </View>
                    {progress.removed > 0 && (
                        <View style={styles.stat}>
                            <Text style={[styles.statValue, {color: colors.error}]}>× {progress.removed}</Text>
                        </View>
                    )}
                    {progress.failed > 0 && (
                        <View style={styles.stat}>
                            <Text style={[styles.statValue, {color: colors.error}]}>! {progress.failed}</Text>
                        </View>
                    )}
                </View>

                <View style={styles.timerContainer}>
                    <Ionicons name="time-outline" size={16} color={colors.textMuted}/>
                    <Text style={styles.timerText}>{formatElapsedTime()}</Text>
                </View>
            </View>
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.backgroundDark,
        justifyContent: 'center',
        alignItems: 'center',
    },
    content: {
        padding: spacing.lg,
        alignItems: 'center',
        width: '100%',
    },
    phaseText: {
        fontSize: fontSize.lg,
        fontWeight: fontWeight.medium,
        color: colors.text,
        marginTop: spacing.md,
    },
    progressBar: {
        width: '100%',
        marginTop: spacing.lg,
    },
    progressText: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
        marginTop: spacing.sm,
    },
    currentFile: {
        fontSize: fontSize.sm,
        color: colors.textMuted,
        marginTop: spacing.md,
        fontStyle: 'italic',
    },
    statsContainer: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        justifyContent: 'center',
        gap: spacing.md,
        marginTop: spacing.xl,
    },
    stat: {
        paddingHorizontal: spacing.md,
        paddingVertical: spacing.sm,
    },
    statValue: {
        fontSize: fontSize.lg,
        fontWeight: fontWeight.bold,
    },
    timerContainer: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.xs,
        marginTop: spacing.lg,
    },
    timerText: {
        fontSize: fontSize.md,
        color: colors.textMuted,
    },
    errorContainer: {
        alignItems: 'center',
        padding: spacing.lg,
    },
    errorTitle: {
        fontSize: fontSize.xl,
        fontWeight: fontWeight.bold,
        color: colors.text,
        marginTop: spacing.md,
    },
    errorMessage: {
        fontSize: fontSize.md,
        color: colors.textSecondary,
        textAlign: 'center',
        marginTop: spacing.sm,
    },
    errorButton: {
        marginTop: spacing.xl,
        minWidth: 150,
    },
});