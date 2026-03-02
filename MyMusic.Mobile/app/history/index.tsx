import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React, {useCallback, useEffect, useState} from 'react';
import {ActivityIndicator, RefreshControl, ScrollView, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import type {SyncSessionItem} from '../../src/api/types';
import {borderRadius, colors, fontSize, fontWeight, spacing} from '../../src/constants/theme';
import {fetchSyncHistory} from '../../src/services/syncService';
import {useConfigStore} from '../../src/stores/configStore';

export default function HistoryListScreen() {
    const router = useRouter();
    const {deviceId, isConfigured} = useConfigStore();
    const [sessions, setSessions] = useState<SyncSessionItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);

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
});