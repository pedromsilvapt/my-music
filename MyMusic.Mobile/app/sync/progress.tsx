import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React, {useEffect, useState} from 'react';
import {ActivityIndicator, Alert, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import {Button, ErrorDisplay, ProgressBar} from '../../src/components/ui';
import type {ErrorDetails} from '../../src/components/ui/ErrorDisplay';
import {colors, fontSize, fontWeight, spacing} from '../../src/constants/theme';
import {createDevice, getDevices} from '../../src/api/devices';
import {getDeviceTypeIdByLabel} from '../../src/constants/deviceIcons';
import {getDeviceIcon, getDeviceName, getImportOnPurchase, getNamingTemplate, setDeviceId} from '../../src/services/configService';
import {runSync, SyncCancelledError} from '../../src/services/syncService';
import {useConfigStore} from '../../src/stores/configStore';
import {useSyncStore} from '../../src/stores/syncStore';

export default function SyncProgressScreen() {
    const router = useRouter();
    const {progress, startSync, updateProgress, setError, completeSync, cancelSync, reset, setOptions} = useSyncStore();
    const {isConfigured, deviceId} = useConfigStore();
    const [error, setSyncError] = useState<string | null>(null);
    const [errorDetails, setErrorDetails] = useState<ErrorDetails | null>(null);
    const [isSyncing, setIsSyncing] = useState(true);
    const [startTime] = useState(Date.now());

    useEffect(() => {
        return () => {
            setOptions({force: false, dryRun: false, autoConfirm: false, treatConflictsAsErrors: false});
        };
    }, []);

    useEffect(() => {
        if (!isConfigured || !deviceId) {
            setSyncError('Please configure your device first');
            setIsSyncing(false);
            return;
        }

        startSync({});

        const isDeviceNotFoundError = (err: any): boolean => {
            return err?.status === 404 || err?.status === 500 ||
                   (err?.message && /device.*not found|not found.*device/i.test(err.message));
        };

        const reacquireDeviceId = async (): Promise<boolean> => {
            try {
                const deviceName = getDeviceName();
                const devicesResponse = await getDevices();
                const existingDevice = devicesResponse.devices.find(d => d.name === deviceName);

                if (existingDevice) {
                    await setDeviceId(existingDevice.id);
                    return true;
                }

                const newDevice = await createDevice({
                    name: deviceName,
                    icon: getDeviceTypeIdByLabel(getDeviceIcon()),
                    namingTemplate: getNamingTemplate() || undefined,
                    importOnPurchase: getImportOnPurchase(),
                });
                await setDeviceId(newDevice.device.id);
                return true;
            } catch (reacquireErr) {
                console.error('Failed to re-acquire device ID:', reacquireErr);
                return false;
            }
        };

        const doSync = async () => {
            try {
                const result = await runSync((prog) => {
                    updateProgress(prog);
                });

                if (result.cancelled) {
                    reset();
                    router.back();
                    return;
                }

                completeSync();

                router.replace(`/history/${result.sessionId}`);
            } catch (err: any) {
                if (err instanceof SyncCancelledError) {
                    reset();
                    router.back();
                    return;
                }

                if (isDeviceNotFoundError(err)) {
                    const shouldReacquire = await new Promise<boolean>((resolve) => {
                        Alert.alert(
                            'Device Not Found',
                            'This device is not registered on the server. Would you like to register it now?',
                            [
                                {text: 'Yes', onPress: () => resolve(true)},
                                {text: 'No', style: 'cancel', onPress: () => resolve(false)},
                            ]
                        );
                    });

                    if (shouldReacquire) {
                        const reacquired = await reacquireDeviceId();
                        if (reacquired) {
                            setIsSyncing(true);
                            const retryResult = await runSync((prog) => {
                                updateProgress(prog);
                            });

                            if (retryResult.cancelled) {
                                reset();
                                router.back();
                                return;
                            }

                            completeSync();
                            router.replace(`/history/${retryResult.sessionId}`);
                            return;
                        }
                    }
                }

                console.error('Sync failed:', err);
                const detailedError: ErrorDetails = {
                    status: err?.status,
                    message: err.message || 'Sync failed',
                    url: err?.url,
                    responseBody: err?.details || err?.responseBody,
                    stack: err?.stack,
                };
                setSyncError(err.message || 'Sync failed');
                setErrorDetails(detailedError);
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
            case 'resolving':
                return 'Resolving conflicts...';
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
                    {errorDetails ? (
                        <View style={styles.errorDisplayContainer}>
                            <ErrorDisplay
                                error={errorDetails}
                                onDismiss={() => {
                                    setErrorDetails(null);
                                    setSyncError(null);
                                    reset();
                                    router.back();
                                }}
                            />
                        </View>
                    ) : (
                        <Text style={styles.errorMessage}>{error || progress.errorMessage}</Text>
                    )}
                    {!errorDetails && (
                        <Button
                            title="Go Back"
                            onPress={() => {
                                reset();
                                router.back();
                            }}
                            style={styles.errorButton}
                        />
                    )}
                </View>
            </View>
        );
    }

    const handleCancel = () => {
        cancelSync();
    };

    const isScanning = progress.phase === 'scanning';

    return (
        <View style={styles.container}>
            <View style={styles.content}>
                <ActivityIndicator size="large" color={colors.primary}/>
                <Text style={styles.phaseText}>{getPhaseLabel()}</Text>

                {isScanning && progress.scannedFiles > 0 && (
                    <Text style={styles.scannedText}>
                        {progress.scannedFiles} files found
                    </Text>
                )}

                {progress.totalFiles > 0 && !isScanning && (
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

                {progress.totalFiles === 0 && isScanning && (
                    <Text style={styles.scanningHint}>
                        Scanning your music folder...
                    </Text>
                )}

                {progress.currentFile && !isScanning && (
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
                    {progress.conflicts > 0 && (
                        <View style={styles.stat}>
                            <Text style={[styles.statValue, {color: colors.warning}]}>⚠ {progress.conflicts}</Text>
                        </View>
                    )}
                </View>

                {isSyncing && (
                    <TouchableOpacity style={styles.cancelButton} onPress={handleCancel}>
                        <Text style={styles.cancelButtonText}>Cancel</Text>
                    </TouchableOpacity>
                )}

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
    scannedText: {
        fontSize: fontSize.md,
        color: colors.primary,
        marginTop: spacing.sm,
        fontWeight: fontWeight.medium,
    },
    scanningHint: {
        fontSize: fontSize.sm,
        color: colors.textMuted,
        marginTop: spacing.sm,
        fontStyle: 'italic',
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
    cancelButton: {
        marginTop: spacing.xl,
        paddingVertical: spacing.sm,
        paddingHorizontal: spacing.xl,
        borderWidth: 1,
        borderColor: colors.error,
        borderRadius: 8,
    },
    cancelButtonText: {
        fontSize: fontSize.md,
        color: colors.error,
        fontWeight: fontWeight.medium,
    },
    errorContainer: {
        alignItems: 'center',
        padding: spacing.lg,
        width: '90%',
    },
    errorDisplayContainer: {
        marginTop: spacing.md,
        width: '100%',
        maxHeight: 400,
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
