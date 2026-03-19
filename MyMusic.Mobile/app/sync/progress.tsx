import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React, {useEffect, useState} from 'react';
import {ActivityIndicator, Alert, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import {Button, ErrorDisplay, ProgressBar} from '../../src/components/ui';
import type {ErrorDetails} from '../../src/components/ui/ErrorDisplay';
import {createDevice, getDevices} from '../../src/api/devices';
import {getDeviceTypeIdByLabel} from '../../src/constants/deviceIcons';
import {useTheme} from '../../src/hooks/useTheme';
import {getDeviceIcon, getDeviceName, getImportOnPurchase, getNamingTemplate, setDeviceId} from '../../src/services/configService';
import {runSync, SyncCancelledError} from '../../src/services/syncService';
import {useConfigStore} from '../../src/stores/configStore';
import {useSyncStore} from '../../src/stores/syncStore';

export default function SyncProgressScreen() {
    const router = useRouter();
    const {colors, fontSize, fontWeight, spacing, borderRadius} = useTheme();
    const {progress, startSync, updateProgress, setError, completeSync, cancelSync, reset, setOptions} = useSyncStore();
    const {isConfigured, deviceId} = useConfigStore();
    const [syncError, setSyncError] = useState<string | null>(null);
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
        if (progress.totalFiles === 0 && progress.estimatedTotalFiles === 0) return 0;
        const total = progress.totalFiles > 0 ? progress.totalFiles : progress.estimatedTotalFiles;
        return progress.processedFiles / total;
    };

    const getEstimatedProgressValue = () => {
        if (progress.estimatedTotalFiles === 0) return 0;
        return progress.scannedFiles / progress.estimatedTotalFiles;
    };

    if (syncError || progress.phase === 'error') {
        return (
            <View style={[styles.container, {backgroundColor: colors.backgroundSecondary}]}>
                <View style={[styles.errorContainer, {padding: spacing.lg, width: '90%'}]}>
                    <Ionicons name="alert-circle" size={64} color={colors.error}/>
                    <Text style={[styles.errorTitle, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.text, marginTop: spacing.md}]}>Sync Failed</Text>
                    {errorDetails ? (
                        <View style={[styles.errorDisplayContainer, {marginTop: spacing.md, width: '100%', maxHeight: 400}]}>
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
                        <Text style={[styles.errorMessage, {fontSize: fontSize.md, color: colors.textSecondary, textAlign: 'center', marginTop: spacing.sm}]}>{syncError || progress.errorMessage}</Text>
                    )}
                    {!errorDetails && (
                        <Button
                            title="Go Back"
                            onPress={() => {
                                reset();
                                router.back();
                            }}
                            style={[styles.errorButton, {marginTop: spacing.xl, minWidth: 150}]}
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
        <View style={[styles.container, {backgroundColor: colors.backgroundSecondary, justifyContent: 'center', alignItems: 'center'}]}>
            <View style={[styles.content, {padding: spacing.lg, alignItems: 'center', width: '100%'}]}>
                <ActivityIndicator size="large" color={colors.primary}/>
                <Text style={[styles.phaseText, {fontSize: fontSize.lg, fontWeight: fontWeight.medium, color: colors.text, marginTop: spacing.md}]}>{getPhaseLabel()}</Text>

                {isScanning && progress.estimatedTotalFiles > 0 && (
                    <>
                        <ProgressBar
                            progress={getEstimatedProgressValue()}
                            height={10}
                            style={[styles.progressBar, {width: '100%', marginTop: spacing.lg}]}
                        />
                        <Text style={[styles.progressText, {fontSize: fontSize.sm, color: colors.textSecondary, marginTop: spacing.sm}]}>
                            {progress.scannedFiles} / ~{progress.estimatedTotalFiles} files
                        </Text>
                    </>
                )}

                {isScanning && progress.estimatedTotalFiles === 0 && progress.scannedFiles > 0 && (
                    <Text style={[styles.scannedText, {fontSize: fontSize.md, color: colors.primary, marginTop: spacing.sm, fontWeight: fontWeight.medium}]}>
                        {progress.scannedFiles} files found
                    </Text>
                )}

                {progress.totalFiles > 0 && !isScanning && (
                    <>
                        <ProgressBar
                            progress={getProgressValue()}
                            height={10}
                            style={[styles.progressBar, {width: '100%', marginTop: spacing.lg}]}
                        />
                        <Text style={[styles.progressText, {fontSize: fontSize.sm, color: colors.textSecondary, marginTop: spacing.sm}]}>
                            {progress.processedFiles} / {progress.totalFiles} files
                        </Text>
                    </>
                )}

                {progress.totalFiles === 0 && progress.estimatedTotalFiles === 0 && isScanning && (
                    <Text style={[styles.scanningHint, {fontSize: fontSize.sm, color: colors.textMuted, marginTop: spacing.sm, fontStyle: 'italic'}]}>
                        Scanning your music folder...
                    </Text>
                )}

                {progress.currentFile && (
                    <Text style={[styles.currentFile, {fontSize: fontSize.sm, color: colors.textMuted, marginTop: spacing.md, fontStyle: 'italic'}]} numberOfLines={2}>
                        {progress.currentFile.split('/').pop()}
                    </Text>
                )}

                <View style={[styles.statsContainer, {flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'center', gap: spacing.md, marginTop: spacing.xl}]}>
                    <View style={styles.stat}>
                        <Text style={[styles.statValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.success}]}>↑ {progress.created}</Text>
                    </View>
                    <View style={styles.stat}>
                        <Text style={[styles.statValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.info}]}>↑ {progress.updated}</Text>
                    </View>
                    <View style={styles.stat}>
                        <Text style={[styles.statValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.textMuted}]}>- {progress.skipped}</Text>
                    </View>
                    <View style={styles.stat}>
                        <Text style={[styles.statValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.syncDownload}]}>↓ {progress.downloaded}</Text>
                    </View>
                    {progress.removed > 0 && (
                        <View style={styles.stat}>
                            <Text style={[styles.statValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.error}]}>× {progress.removed}</Text>
                        </View>
                    )}
                    {progress.failed > 0 && (
                        <View style={styles.stat}>
                            <Text style={[styles.statValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.error}]}>! {progress.failed}</Text>
                        </View>
                    )}
                    {progress.conflicts > 0 && (
                        <View style={styles.stat}>
                            <Text style={[styles.statValue, {fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.warning}]}>⚠ {progress.conflicts}</Text>
                        </View>
                    )}
                </View>

                {isSyncing && (
                    <TouchableOpacity style={[styles.cancelButton, {marginTop: spacing.xl, paddingVertical: spacing.sm, paddingHorizontal: spacing.xl, borderWidth: 1, borderColor: colors.error, borderRadius: borderRadius.md}]} onPress={handleCancel}>
                        <Text style={[styles.cancelButtonText, {fontSize: fontSize.md, color: colors.error, fontWeight: fontWeight.medium}]}>Cancel</Text>
                    </TouchableOpacity>
                )}

                <View style={[styles.timerContainer, {flexDirection: 'row', alignItems: 'center', gap: spacing.xs, marginTop: spacing.lg}]}>
                    <Ionicons name="time-outline" size={16} color={colors.textMuted}/>
                    <Text style={[styles.timerText, {fontSize: fontSize.md, color: colors.textMuted}]}>{formatElapsedTime()}</Text>
                </View>
            </View>
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
    },
    content: {
        alignItems: 'center',
    },
    phaseText: {
        fontSize: 18,
    },
    scannedText: {
        fontSize: 14,
        fontWeight: '500',
    },
    scanningHint: {
        fontSize: 12,
        fontStyle: 'italic',
    },
    progressBar: {
        width: '100%',
    },
    progressText: {
        fontSize: 12,
    },
    currentFile: {
        fontSize: 12,
        fontStyle: 'italic',
    },
    statsContainer: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        justifyContent: 'center',
    },
    stat: {
        paddingHorizontal: 16,
        paddingVertical: 8,
    },
    statValue: {
        fontSize: 16,
    },
    timerContainer: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    timerText: {
        fontSize: 14,
    },
    cancelButton: {
        marginTop: 32,
    },
    cancelButtonText: {
        fontSize: 14,
        fontWeight: '500',
    },
    errorContainer: {
        alignItems: 'center',
    },
    errorDisplayContainer: {
        width: '100%',
    },
    errorTitle: {
        fontSize: 18,
        fontWeight: '700',
    },
    errorMessage: {
        fontSize: 14,
        textAlign: 'center',
    },
    errorButton: {
        marginTop: 32,
        minWidth: 150,
    },
});
