import {Ionicons} from '@expo/vector-icons';
import {useNavigation, useRouter} from 'expo-router';
import React, {useCallback, useEffect, useState} from 'react';
import {ActivityIndicator, Alert, Modal, Pressable, RefreshControl, ScrollView, StyleSheet, Switch, Text, TouchableOpacity, View} from 'react-native';
import type {SyncSessionItem} from '../../src/api/types';
import {pruneSessions} from '../../src/api/sync';
import {borderRadius, colors, fontSize, fontWeight, spacing} from '../../src/constants/theme';
import {fetchSyncHistory} from '../../src/services/syncService';
import {useConfigStore} from '../../src/stores/configStore';

export default function HistoryListScreen() {
    const router = useRouter();
    const {deviceId, isConfigured} = useConfigStore();
    const [sessions, setSessions] = useState<SyncSessionItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);
    const [pruneModalVisible, setPruneModalVisible] = useState(false);
    const [pruneAll, setPruneAll] = useState(false);
    const [pruning, setPruning] = useState(false);

    const loadSessions = useCallback(async () => {
        if (!deviceId || !isConfigured) {
            setLoading(false);
            return;
        }

        try {
            const response = await fetchSyncHistory(deviceId, 20);
            setSessions(response.sessions);
        } catch (error) {
            console.error('Failed to load sessions:', error);
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
                    <Ionicons name="cut-outline" size={22} color={colors.text}/>
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
        } catch (error) {
            Alert.alert('Error', 'Failed to prune sessions');
        } finally {
            setPruning(false);
        }
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
            <View style={styles.loadingContainer}>
                <ActivityIndicator size="large" color={colors.primary}/>
            </View>
        );
    }

    if (!isConfigured || !deviceId) {
        return (
            <View style={styles.emptyContainer}>
                <Ionicons name="settings-outline" size={48} color={colors.textMuted}/>
                <Text style={styles.emptyText}>Configure your device to view sync history</Text>
                <TouchableOpacity style={styles.configButton} onPress={() => router.push('/settings/device')}>
                    <Text style={styles.configButtonText}>Configure Device</Text>
                </TouchableOpacity>
            </View>
        );
    }

    if (sessions.length === 0) {
        return (
            <View style={styles.emptyContainer}>
                <Ionicons name="time-outline" size={48} color={colors.textMuted}/>
                <Text style={styles.emptyText}>No sync sessions yet</Text>
                <Text style={styles.emptySubtext}>Start a sync to see your history here</Text>
            </View>
        );
    }

    return (
        <>
        <ScrollView
            style={styles.container}
            contentContainerStyle={styles.content}
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
                    style={styles.sessionItem}
                    onPress={() => router.push(`/history/${session.id}`)}
                >
                    <View style={styles.sessionHeader}>
                        <View style={styles.sessionInfo}>
                            <View style={styles.statusBadge}>
                                <Ionicons
                                    name={getStatusIcon(session.status) as any}
                                    size={16}
                                    color={getStatusColor(session.status)}
                                />
                                <Text style={[styles.statusText, {color: getStatusColor(session.status)}]}>
                                    {session.status}
                                </Text>
                            </View>
                            {session.isDryRun && (
                                <View style={styles.dryRunBadge}>
                                    <Text style={styles.dryRunText}>Dry Run</Text>
                                </View>
                            )}
                        </View>
                        <Ionicons name="chevron-forward" size={20} color={colors.textMuted}/>
                    </View>

                    <Text style={styles.sessionDate}>{formatDate(session.startedAt)}</Text>

                    <View style={styles.sessionStats}>
                        <View style={styles.statItem}>
                            <Text style={[styles.statValue, {color: colors.success}]}>{session.createdCount}</Text>
                            <Text style={styles.statLabel}>Created</Text>
                        </View>
                        <View style={styles.statItem}>
                            <Text style={[styles.statValue, {color: colors.info}]}>{session.updatedCount}</Text>
                            <Text style={styles.statLabel}>Updated</Text>
                        </View>
                        <View style={styles.statItem}>
                            <Text style={[styles.statValue, {color: colors.textMuted}]}>{session.skippedCount}</Text>
                            <Text style={styles.statLabel}>Skipped</Text>
                        </View>
                        <View style={styles.statItem}>
                            <Text
                                style={[styles.statValue, {color: colors.syncDownload}]}>{session.downloadedCount}</Text>
                            <Text style={styles.statLabel}>Downloaded</Text>
                        </View>
                        {session.errorCount > 0 && (
                            <View style={styles.statItem}>
                                <Text style={[styles.statValue, {color: colors.error}]}>{session.errorCount}</Text>
                                <Text style={styles.statLabel}>Errors</Text>
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
            <Pressable style={styles.modalOverlay} onPress={() => setPruneModalVisible(false)}>
                <Pressable style={styles.modalContent} onPress={() => {}}>
                    <Text style={styles.modalTitle}>Prune Sessions</Text>
                    <Text style={styles.modalText}>
                        This will remove {sessionsToPrune} session(s) older than 1 day{!pruneAll && sessions.length > 10 ? ' and beyond the 10th most recent' : ''}.
                    </Text>

                    <View style={styles.switchRow}>
                        <Text style={styles.switchLabel}>All sessions</Text>
                        <Switch
                            value={pruneAll}
                            onValueChange={setPruneAll}
                            trackColor={{false: colors.border, true: colors.primary}}
                            thumbColor={colors.surface}
                        />
                    </View>

                    <View style={styles.modalButtons}>
                        <TouchableOpacity
                            style={[styles.modalButton, styles.modalButtonCancel]}
                            onPress={() => setPruneModalVisible(false)}
                        >
                            <Text style={styles.modalButtonCancelText}>Cancel</Text>
                        </TouchableOpacity>
                        <TouchableOpacity
                            style={[styles.modalButton, styles.modalButtonConfirm]}
                            onPress={handlePrune}
                            disabled={pruning || sessionsToPrune === 0}
                        >
                            <Text style={styles.modalButtonConfirmText}>
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
    emptyContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: colors.backgroundDark,
        padding: spacing.lg,
    },
    emptyText: {
        fontSize: fontSize.lg,
        color: colors.text,
        marginTop: spacing.md,
    },
    emptySubtext: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
        marginTop: spacing.xs,
    },
    configButton: {
        backgroundColor: colors.primary,
        paddingHorizontal: spacing.lg,
        paddingVertical: spacing.sm,
        borderRadius: borderRadius.md,
        marginTop: spacing.lg,
    },
    configButtonText: {
        color: '#fff',
        fontWeight: fontWeight.semibold,
    },
    sessionCard: {
        padding: spacing.md,
    },
    sessionItem: {
        backgroundColor: colors.surface,
        borderRadius: borderRadius.lg,
        padding: spacing.md,
        marginBottom: spacing.md,
    },
    sessionHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    sessionInfo: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.sm,
    },
    statusBadge: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.xs,
    },
    statusText: {
        fontSize: fontSize.sm,
        fontWeight: fontWeight.medium,
    },
    dryRunBadge: {
        backgroundColor: colors.warning + '20',
        paddingHorizontal: spacing.sm,
        paddingVertical: 2,
        borderRadius: borderRadius.sm,
    },
    dryRunText: {
        fontSize: fontSize.xs,
        color: colors.warning,
        fontWeight: fontWeight.medium,
    },
    sessionDate: {
        fontSize: fontSize.sm,
        color: colors.textMuted,
        marginTop: spacing.xs,
    },
    sessionStats: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        marginTop: spacing.md,
        paddingTop: spacing.md,
        borderTopWidth: 1,
        borderTopColor: colors.border,
    },
    statItem: {
        alignItems: 'center',
    },
    statValue: {
        fontSize: fontSize.md,
        fontWeight: fontWeight.bold,
    },
    statLabel: {
        fontSize: fontSize.xs,
        color: colors.textMuted,
    },
    modalOverlay: {
        flex: 1,
        backgroundColor: 'rgba(0, 0, 0, 0.7)',
        justifyContent: 'center',
        alignItems: 'center',
    },
    modalContent: {
        backgroundColor: colors.surface,
        borderRadius: borderRadius.lg,
        padding: spacing.lg,
        width: '85%',
        maxWidth: 400,
    },
    modalTitle: {
        fontSize: fontSize.lg,
        fontWeight: fontWeight.semibold,
        color: colors.text,
        marginBottom: spacing.md,
    },
    modalText: {
        fontSize: fontSize.md,
        color: colors.textSecondary,
        marginBottom: spacing.lg,
        lineHeight: 22,
    },
    switchRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: spacing.lg,
        paddingVertical: spacing.sm,
    },
    switchLabel: {
        fontSize: fontSize.md,
        color: colors.text,
    },
    modalButtons: {
        flexDirection: 'row',
        gap: spacing.md,
    },
    modalButton: {
        flex: 1,
        paddingVertical: spacing.md,
        borderRadius: borderRadius.md,
        alignItems: 'center',
    },
    modalButtonCancel: {
        backgroundColor: colors.border,
    },
    modalButtonCancelText: {
        color: colors.text,
        fontWeight: fontWeight.medium,
    },
    modalButtonConfirm: {
        backgroundColor: colors.primary,
    },
    modalButtonConfirmText: {
        color: '#fff',
        fontWeight: fontWeight.semibold,
    },
});