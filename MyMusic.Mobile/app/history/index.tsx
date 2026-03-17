import {Ionicons} from '@expo/vector-icons';
import {useNavigation, useRouter} from 'expo-router';
import React, {useCallback, useEffect, useState} from 'react';
import {ActivityIndicator, Alert, Modal, Pressable, RefreshControl, ScrollView, StyleSheet, Switch, Text, TouchableOpacity, View} from 'react-native';
import type {SyncSessionItem} from '../../src/api/types';
import {pruneSessions} from '../../src/api/sync';
import {ErrorDisplay} from '../../src/components/ui';
import type {ErrorDetails} from '../../src/components/ui/ErrorDisplay';
import {useTheme} from '../../src/hooks/useTheme';
import {fetchSyncHistory} from '../../src/services/syncService';
import {useConfigStore} from '../../src/stores/configStore';

export default function HistoryListScreen() {
    const router = useRouter();
    const {colors, fontSize, fontWeight, spacing, borderRadius, withAlpha} = useTheme();
    const {deviceId, isConfigured} = useConfigStore();
    const [sessions, setSessions] = useState<SyncSessionItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);
    const [pruneModalVisible, setPruneModalVisible] = useState(false);
    const [pruneAll, setPruneAll] = useState(false);
    const [pruning, setPruning] = useState(false);
    const [error, setError] = useState<ErrorDetails | null>(null);

    const loadSessions = useCallback(async () => {
        if (!deviceId || !isConfigured) {
            setLoading(false);
            return;
        }

        try {
            const response = await fetchSyncHistory(deviceId, 20);
            setSessions(response.sessions);
            setError(null);
        } catch (err: any) {
            console.error('Failed to load sessions:', err);
            setError({
                status: err?.status,
                message: err?.message || 'Failed to load sessions',
                url: err?.url,
                responseBody: err?.details || err?.responseBody,
                stack: err?.stack,
            });
        } finally {
            setLoading(false);
            setRefreshing(false);
        }
    }, [deviceId, isConfigured]);

    useEffect(() => {
        loadSessions();
    }, [loadSessions]);

    const navigation = useNavigation();

    React.useEffect(() => {
        if (!isConfigured || !deviceId || sessions.length === 0) return;

        navigation.setOptions({
            headerRight: () => (
                <TouchableOpacity onPress={() => setPruneModalVisible(true)} style={{padding: spacing.sm}}>
                    <Ionicons name="trash-outline" size={22} color={colors.text}/>
                </TouchableOpacity>
            ),
        });
    }, [navigation, isConfigured, deviceId, sessions.length]);

    const handlePrune = async () => {
        if (!deviceId) return;

        setPruning(true);
        try {
            await pruneSessions(deviceId, {all: pruneAll});
            setPruneModalVisible(false);
            loadSessions();
        } catch (err: any) {
            const pruneError: ErrorDetails = {
                status: err?.status,
                message: err?.message || 'Failed to prune sessions',
                url: err?.url,
                responseBody: err?.details || err?.responseBody,
                stack: err?.stack,
            };
            Alert.alert(
                'Error',
                'Failed to prune sessions',
                [
                    {text: 'OK'},
                    {text: 'Details', onPress: () => Alert.alert('Error Details', formatErrorDetails(pruneError))},
                ]
            );
        } finally {
            setPruning(false);
        }
    };

    const formatErrorDetails = (err: ErrorDetails): string => {
        let details = '';
        if (err.status) details += `Status: ${err.status}\n`;
        if (err.message) details += `Message: ${err.message}\n`;
        if (err.url) details += `URL: ${err.url}\n`;
        if (err.responseBody) details += `Response: ${err.responseBody}\n`;
        if (err.stack) details += `Stack: ${err.stack}\n`;
        return details || 'No details available';
    };

    const calculateSessionsToPrune = () => {
        if (pruneAll) return sessions.length;
        const cutoffDate = new Date(Date.now() - 24 * 60 * 60 * 1000);
        const keepThreshold = sessions.length > 10 ? sessions[9].startedAt : null;
        return sessions.filter(s => {
            const startedAt = new Date(s.startedAt);
            return startedAt < cutoffDate || (keepThreshold && startedAt < new Date(keepThreshold));
        }).length;
    };

    const sessionsToPrune = calculateSessionsToPrune();

    const onRefresh = () => {
        setRefreshing(true);
        loadSessions();
    };

    const formatDate = (dateStr: string) => {
        const date = new Date(dateStr);
        return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], {hour: '2-digit', minute: '2-digit'});
    };

    const getStatusColor = (status: string) => {
        switch (status) {
            case 'Completed':
                return colors.success;
            case 'InProgress':
                return colors.warning;
            case 'Cancelled':
                return colors.error;
            default:
                return colors.textMuted;
        }
    };

    const getStatusIcon = (status: string) => {
        switch (status) {
            case 'Completed':
                return 'checkmark-circle';
            case 'InProgress':
                return 'time';
            case 'Cancelled':
                return 'close-circle';
            default:
                return 'help-circle';
        }
    };

    if (loading) {
        return (
            <View style={[styles.loadingContainer, {backgroundColor: colors.backgroundSecondary, justifyContent: 'center', alignItems: 'center'}]}>
                <ActivityIndicator size="large" color={colors.primary}/>
            </View>
        );
    }

    if (error) {
        return (
            <View style={[styles.container, {backgroundColor: colors.backgroundSecondary}]}>
                <View style={[styles.errorContainer, {padding: spacing.sm, justifyContent: 'center'}]}>
                    <ErrorDisplay
                        error={error}
                        onRetry={loadSessions}
                        onDismiss={() => setError(null)}
                    />
                </View>
            </View>
        );
    }

    if (!isConfigured || !deviceId) {
        return (
            <View style={[styles.emptyContainer, {backgroundColor: colors.backgroundSecondary, justifyContent: 'center', alignItems: 'center', padding: spacing.lg}]}>
                <Ionicons name="settings-outline" size={48} color={colors.textMuted}/>
                <Text style={[styles.emptyText, {fontSize: fontSize.lg, color: colors.text, marginTop: spacing.md}]}>Configure your device to view sync history</Text>
                <TouchableOpacity style={[styles.configButton, {backgroundColor: colors.primary, paddingHorizontal: spacing.lg, paddingVertical: spacing.sm, borderRadius: borderRadius.md, marginTop: spacing.lg}]} onPress={() => router.push('/settings/device')}>
                    <Text style={[styles.configButtonText, {color: colors.textInverse, fontWeight: fontWeight.semibold}]}>Configure Device</Text>
                </TouchableOpacity>
            </View>
        );
    }

    if (sessions.length === 0) {
        return (
            <View style={[styles.emptyContainer, {backgroundColor: colors.backgroundSecondary, justifyContent: 'center', alignItems: 'center'}]}>
                <Ionicons name="time-outline" size={48} color={colors.textMuted}/>
                <Text style={[styles.emptyText, {fontSize: fontSize.lg, color: colors.text}]}>No sync sessions yet</Text>
                <Text style={[styles.emptySubtext, {fontSize: fontSize.sm, color: colors.textSecondary, marginTop: spacing.xs}]}>Start a sync to see your history here</Text>
            </View>
        );
    }

    return (
        <>
        <ScrollView
            style={[styles.container, {backgroundColor: colors.backgroundSecondary}]}
            contentContainerStyle={[styles.content, {padding: spacing.md}]}
            refreshControl={
                <RefreshControl
                    refreshing={refreshing}
                    onRefresh={onRefresh}
                    tintColor={colors.primary}
                />
            }
        >
            {sessions.map((session) => (
                <TouchableOpacity
                    key={session.id}
                    style={[styles.sessionItem, {backgroundColor: colors.card, borderRadius: borderRadius.lg, padding: spacing.md, marginBottom: spacing.md}]}
                    onPress={() => router.push(`/history/${session.id}`)}
                >
                    <View style={styles.sessionHeader}>
                        <View style={[styles.sessionInfo, {gap: spacing.sm}]}>
                            <View style={[styles.statusBadge, {flexDirection: 'row', alignItems: 'center', gap: spacing.xs}]}>
                                <Ionicons
                                    name={getStatusIcon(session.status) as any}
                                    size={16}
                                    color={getStatusColor(session.status)}
                                />
                                <Text style={[styles.statusText, {fontSize: fontSize.sm, fontWeight: fontWeight.medium, color: getStatusColor(session.status)}]}>
                                    {session.status}
                                </Text>
                            </View>
                            {session.isDryRun && (
                                <View style={[styles.dryRunBadge, {backgroundColor: withAlpha('warning', 0.12), paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: borderRadius.sm}]}>
                                    <Text style={[styles.dryRunText, {fontSize: fontSize.xs, color: colors.warning, fontWeight: fontWeight.medium}]}>Dry Run</Text>
                                </View>
                            )}
                        </View>
                        <Ionicons name="chevron-forward" size={20} color={colors.cardTextMuted}/>
                    </View>

                    <Text style={[styles.sessionDate, {fontSize: fontSize.sm, color: colors.cardTextMuted, marginTop: spacing.xs}]}>{formatDate(session.startedAt)}</Text>

                    <View style={[styles.sessionStats, {flexDirection: 'row', justifyContent: 'space-between', marginTop: spacing.md, paddingTop: spacing.md, borderTopWidth: 1, borderTopColor: colors.cardBorder}]}>
                        <View style={styles.statItem}>
                            <Text style={[styles.statValue, {fontSize: fontSize.md, fontWeight: fontWeight.bold, color: colors.success}]}>{session.createdCount}</Text>
                            <Text style={[styles.statLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Created</Text>
                        </View>
                        <View style={styles.statItem}>
                            <Text style={[styles.statValue, {fontSize: fontSize.md, fontWeight: fontWeight.bold, color: colors.info}]}>{session.updatedCount}</Text>
                            <Text style={[styles.statLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Updated</Text>
                        </View>
                        <View style={styles.statItem}>
                            <Text style={[styles.statValue, {fontSize: fontSize.md, fontWeight: fontWeight.bold, color: colors.cardTextMuted}]}>{session.skippedCount}</Text>
                            <Text style={[styles.statLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Skipped</Text>
                        </View>
                        <View style={styles.statItem}>
                            <Text
                                style={[styles.statValue, {fontSize: fontSize.md, fontWeight: fontWeight.bold, color: colors.syncDownload}]}>{session.downloadedCount}</Text>
                            <Text style={[styles.statLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Downloaded</Text>
                        </View>
                        {session.errorCount > 0 && (
                            <View style={styles.statItem}>
                                <Text style={[styles.statValue, {fontSize: fontSize.md, fontWeight: fontWeight.bold, color: colors.error}]}>{session.errorCount}</Text>
                                <Text style={[styles.statLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Errors</Text>
                            </View>
                        )}
                    </View>
                </TouchableOpacity>
            ))}
        </ScrollView>

        <Modal
            visible={pruneModalVisible}
            transparent
            animationType="fade"
            onRequestClose={() => setPruneModalVisible(false)}
        >
            <Pressable style={[styles.modalOverlay, {backgroundColor: 'rgba(0, 0, 0, 0.7)', justifyContent: 'center', alignItems: 'center'}]} onPress={() => setPruneModalVisible(false)}>
                <Pressable style={[styles.modalContent, {backgroundColor: colors.card, borderRadius: borderRadius.lg, padding: spacing.lg, width: '85%', maxWidth: 400}]} onPress={() => {}}>
                    <Text style={[styles.modalTitle, {fontSize: fontSize.lg, fontWeight: fontWeight.semibold, color: colors.cardText, marginBottom: spacing.md}]}>Prune Sessions</Text>
                    <Text style={[styles.modalText, {fontSize: fontSize.md, color: colors.cardTextSecondary, marginBottom: spacing.lg, lineHeight: 22}]}>
                        This will remove {sessionsToPrune} session(s) older than 1 day{!pruneAll && sessions.length > 10 ? ' and beyond the 10th most recent' : ''}.
                    </Text>

                    <View style={[styles.switchRow, {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing.lg, paddingVertical: spacing.sm}]}>
                        <Text style={[styles.switchLabel, {fontSize: fontSize.md, color: colors.cardText}]}>All sessions</Text>
                        <Switch
                            value={pruneAll}
                            onValueChange={setPruneAll}
                            trackColor={{false: colors.cardBorder, true: colors.primary}}
                            thumbColor={colors.cardText}
                        />
                    </View>

                    <View style={[styles.modalButtons, {flexDirection: 'row', gap: spacing.md}]}>
                        <TouchableOpacity
                            style={[styles.modalButton, {backgroundColor: colors.cardSecondary, flex: 1, paddingVertical: spacing.md, borderRadius: borderRadius.md, alignItems: 'center'}]}
                            onPress={() => setPruneModalVisible(false)}
                        >
                            <Text style={[styles.modalButtonCancelText, {color: colors.cardText, fontWeight: fontWeight.medium}]}>Cancel</Text>
                        </TouchableOpacity>
                        <TouchableOpacity
                            style={[styles.modalButton, {backgroundColor: colors.primary, flex: 1, paddingVertical: spacing.md, borderRadius: borderRadius.md, alignItems: 'center'}]}
                            onPress={handlePrune}
                            disabled={pruning || sessionsToPrune === 0}
                        >
                            <Text style={[styles.modalButtonConfirmText, {color: colors.textInverse, fontWeight: fontWeight.semibold}]}>
                                {pruning ? 'Pruning...' : 'Prune'}
                            </Text>
                        </TouchableOpacity>
                    </View>
                </Pressable>
            </Pressable>
        </Modal>
        </>
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
    errorContainer: {
        padding: 4,
        justifyContent: 'center',
    },
    emptyContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
    emptyText: {
        fontSize: 18,
    },
    emptySubtext: {
        fontSize: 12,
    },
    configButton: {
        marginTop: 24,
    },
    configButtonText: {
        fontWeight: '600',
    },
    sessionItem: {
        marginBottom: 16,
    },
    sessionHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    sessionInfo: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    statusBadge: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    statusText: {
        fontSize: 12,
        fontWeight: '500',
    },
    dryRunBadge: {
        paddingHorizontal: 8,
        paddingVertical: 2,
    },
    dryRunText: {
        fontSize: 10,
        fontWeight: '500',
    },
    sessionDate: {
        fontSize: 12,
    },
    sessionStats: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        borderTopWidth: 1,
    },
    statItem: {
        alignItems: 'center',
    },
    statValue: {
        fontSize: 14,
        fontWeight: '700',
    },
    statLabel: {
        fontSize: 10,
    },
    modalOverlay: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
    modalContent: {
        width: '85%',
        maxWidth: 400,
    },
    modalTitle: {
        fontSize: 16,
    },
    modalText: {
        fontSize: 14,
        lineHeight: 22,
    },
    switchRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    switchLabel: {
        fontSize: 14,
    },
    modalButtons: {
        flexDirection: 'row',
    },
    modalButton: {
        flex: 1,
    },
    modalButtonCancelText: {
        fontWeight: '500',
    },
    modalButtonConfirmText: {
        fontWeight: '600',
    },
});
