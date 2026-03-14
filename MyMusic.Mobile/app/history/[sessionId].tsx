import {Ionicons} from '@expo/vector-icons';
import {useLocalSearchParams, useNavigation, useRouter} from 'expo-router';
import React, {useEffect, useLayoutEffect, useMemo, useState} from 'react';
import {ActivityIndicator, Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import type {SyncRecordResponseItem, SyncSessionItem} from '../../src/api/types';
import {deleteSession} from '../../src/api/sync';
import {borderRadius, colors, fontSize, fontWeight, spacing} from '../../src/constants/theme';
import {fetchSessionDetails, fetchSyncHistory} from '../../src/services/syncService';
import {useConfigStore} from '../../src/stores/configStore';

type FilterType = 'all' | 'created' | 'updated' | 'skipped' | 'downloaded' | 'removed' | 'error' | 'conflict';

function formatFilePath(path: string, repositoryPath: string | null | undefined): string {
    let formatted = path;

    if (repositoryPath && formatted.startsWith(repositoryPath)) {
        formatted = formatted.slice(repositoryPath.length);
    }

    if (formatted.startsWith('/')) {
        formatted = formatted.slice(1);
    }

    try {
        formatted = decodeURIComponent(formatted);
    } catch {
        // Keep original if decode fails
    }

    return formatted;
}

export default function SessionDetailScreen() {
    const {sessionId} = useLocalSearchParams<{ sessionId: string }>();
    const navigation = useNavigation();
    const {deviceId} = useConfigStore();

    const [session, setSession] = useState<SyncSessionItem | null>(null);
    const [records, setRecords] = useState<SyncRecordResponseItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [filter, setFilter] = useState<FilterType>('all');

    useEffect(() => {
        if (!deviceId || !sessionId) return;

        const loadData = async () => {
            try {
                const sessionsResponse = await fetchSyncHistory(deviceId, 100);
                const foundSession = sessionsResponse.sessions.find(s => s.id === parseInt(sessionId));
                setSession(foundSession || null);

                if (foundSession) {
                    const recordsResponse = await fetchSessionDetails(deviceId, parseInt(sessionId));
                    setRecords(recordsResponse.records);
                }
            } catch (error) {
                console.error('Failed to load session:', error);
            } finally {
                setLoading(false);
            }
        };

        loadData();
    }, [deviceId, sessionId]);

    const router = useRouter();

    const handleDelete = () => {
        if (!deviceId || !sessionId) return;

        Alert.alert(
            'Delete Session',
            'Are you sure you want to delete this session? This action cannot be undone.',
            [
                {text: 'Cancel', style: 'cancel'},
                {
                    text: 'Delete',
                    style: 'destructive',
                    onPress: async () => {
                        try {
                            await deleteSession(deviceId, parseInt(sessionId));
                            router.back();
                        } catch (error) {
                            Alert.alert('Error', 'Failed to delete session');
                        }
                    },
                },
            ]
        );
    };

    useLayoutEffect(() => {
        navigation.setOptions({
            headerRight: () => (
                <TouchableOpacity onPress={handleDelete} style={{padding: spacing.sm}}>
                    <Ionicons name="trash-outline" size={22} color={colors.error}/>
                </TouchableOpacity>
            ),
        });
    }, [navigation, handleDelete]);

    const filteredRecords = useMemo(() => {
        if (filter === 'all') return records;
        return records.filter(r => r.action.toLowerCase() === filter);
    }, [records, filter]);

    const groupedRecords = useMemo(() => {
        const groups: Record<string, SyncRecordResponseItem[]> = {};
        for (const record of filteredRecords) {
            if (!groups[record.action]) {
                groups[record.action] = [];
            }
            groups[record.action].push(record);
        }
        return groups;
    }, [filteredRecords]);

    const sortedGroupEntries = useMemo(() => {
        const entries = Object.entries(groupedRecords);
        return entries.sort(([actionA], [actionB]) => {
            if (actionA.toLowerCase() === 'skipped') return 1;
            if (actionB.toLowerCase() === 'skipped') return -1;
            return actionA.localeCompare(actionB);
        });
    }, [groupedRecords]);

    const getActionColor = (action: string) => {
        switch (action.toLowerCase()) {
            case 'created':
                return colors.success;
            case 'updated':
                return colors.info;
            case 'skipped':
                return colors.textMuted;
            case 'downloaded':
                return colors.syncDownload;
            case 'removed':
                return colors.error;
            case 'error':
                return colors.error;
            case 'conflict':
                return colors.warning;
            default:
                return colors.textSecondary;
        }
    };

    const getActionIcon = (action: string, source: string) => {
        if (action.toLowerCase() === 'error') return 'alert-circle';
        if (action.toLowerCase() === 'skipped') return 'skip-forward';
        if (action.toLowerCase() === 'conflict') return 'warning';
        if (source === 'Server') return 'cloud-download';
        return 'cloud-upload';
    };

    const formatDate = (dateStr: string) => {
        return new Date(dateStr).toLocaleString();
    };

    const filters: { key: FilterType; label: string }[] = [
        {key: 'all', label: 'All'},
        {key: 'created', label: 'Created'},
        {key: 'updated', label: 'Updated'},
        {key: 'skipped', label: 'Skipped'},
        {key: 'downloaded', label: 'Down'},
        {key: 'removed', label: 'Removed'},
        {key: 'error', label: 'Errors'},
        {key: 'conflict', label: 'Conflicts'},
    ];

    if (loading) {
        return (
            <View style={styles.loadingContainer}>
                <ActivityIndicator size="large" color={colors.primary}/>
            </View>
        );
    }

    if (!session) {
        return (
            <View style={styles.errorContainer}>
                <Text style={styles.errorText}>Session not found</Text>
            </View>
        );
    }

    return (
        <View style={styles.container}>
            <ScrollView>
                <View style={styles.header}>
                    <View style={styles.headerRow}>
                        <View style={styles.statusBadge}>
                            <Text style={styles.statusText}>{session.status}</Text>
                        </View>
                        {session.isDryRun && (
                            <View style={styles.dryRunBadge}>
                                <Text style={styles.dryRunText}>Dry Run</Text>
                            </View>
                        )}
                    </View>
                    <Text style={styles.dateText}>Started: {formatDate(session.startedAt)}</Text>
                    {session.completedAt && (
                        <Text style={styles.dateText}>Completed: {formatDate(session.completedAt)}</Text>
                    )}
                </View>

                <View style={styles.summaryCard}>
                    <Text style={styles.summaryTitle}>Summary</Text>
                    <View style={styles.summaryGrid}>
                        <View style={styles.summaryItem}>
                            <Text style={[styles.summaryValue, {color: colors.success}]}>{session.createdCount}</Text>
                            <Text style={styles.summaryLabel}>Created</Text>
                        </View>
                        <View style={styles.summaryItem}>
                            <Text style={[styles.summaryValue, {color: colors.info}]}>{session.updatedCount}</Text>
                            <Text style={styles.summaryLabel}>Updated</Text>
                        </View>
                        <View style={styles.summaryItem}>
                            <Text style={[styles.summaryValue, {color: colors.textMuted}]}>{session.skippedCount}</Text>
                            <Text style={styles.summaryLabel}>Skipped</Text>
                        </View>
                        <View style={styles.summaryItem}>
                            <Text
                                style={[styles.summaryValue, {color: colors.syncDownload}]}>{session.downloadedCount}</Text>
                            <Text style={styles.summaryLabel}>Downloaded</Text>
                        </View>
                        <View style={styles.summaryItem}>
                            <Text style={[styles.summaryValue, {color: colors.error}]}>{session.removedCount}</Text>
                            <Text style={styles.summaryLabel}>Removed</Text>
                        </View>
                        <View style={styles.summaryItem}>
                            <Text style={[styles.summaryValue, {color: colors.error}]}>{session.errorCount}</Text>
                            <Text style={styles.summaryLabel}>Errors</Text>
                        </View>
                    </View>
                </View>

                <View style={styles.filterContainer}>
                    {filters.map(f => (
                        <TouchableOpacity
                            key={f.key}
                            style={[styles.filterButton, filter === f.key && styles.filterButtonActive]}
                            onPress={() => setFilter(f.key)}
                        >
                            <Text style={[styles.filterText, filter === f.key && styles.filterTextActive]}>
                                {f.label}
                            </Text>
                        </TouchableOpacity>
                    ))}
                </View>

                <View style={styles.recordsContainer}>
                    {sortedGroupEntries.map(([action, actionRecords]) => (
                        <View key={action} style={styles.recordGroup}>
                            <View style={styles.recordGroupHeader}>
                                <View style={[styles.actionBadge, {backgroundColor: getActionColor(action) + '20'}]}>
                                    <Ionicons
                                        name={getActionIcon(action, actionRecords[0]?.source || '') as any}
                                        size={14}
                                        color={getActionColor(action)}
                                    />
                                    <Text style={[styles.actionText, {color: getActionColor(action)}]}>
                                        {action} ({actionRecords.length})
                                    </Text>
                                </View>
                            </View>
                            {actionRecords.map((record, idx) => (
                                <View key={idx} style={styles.recordItem}>
                                    <View style={styles.recordPathRow}>
                                        <Ionicons
                                            name={record.source === 'Server' ? 'arrow-down' : 'arrow-up'}
                                            size={12}
                                            color={getActionColor(action)}
                                        />
                                        <Text style={styles.recordPath} numberOfLines={1}>
                                            {formatFilePath(record.filePath, session?.repositoryPath)}
                                        </Text>
                                    </View>
                                    {record.errorMessage && (
                                        <Text style={styles.recordError} numberOfLines={2}>
                                            {record.errorMessage}
                                        </Text>
                                    )}
                                    {record.reason && (
                                        <Text style={styles.recordReason} numberOfLines={2}>
                                            {record.reason}
                                        </Text>
                                    )}
                                </View>
                            ))}
                        </View>
                    ))}

                    {filteredRecords.length === 0 && (
                        <View style={styles.emptyRecords}>
                            <Text style={styles.emptyText}>No records match this filter</Text>
                        </View>
                    )}
                </View>
            </ScrollView>
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.backgroundDark,
    },
    loadingContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: colors.backgroundDark,
    },
    errorContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: colors.backgroundDark,
    },
    errorText: {
        color: colors.textSecondary,
        fontSize: fontSize.lg,
    },
    header: {
        padding: spacing.md,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
    },
    headerRow: {
        flexDirection: 'row',
        gap: spacing.sm,
    },
    statusBadge: {
        backgroundColor: colors.success + '20',
        paddingHorizontal: spacing.sm,
        paddingVertical: spacing.xs,
        borderRadius: borderRadius.sm,
    },
    statusText: {
        color: colors.success,
        fontWeight: fontWeight.medium,
        fontSize: fontSize.sm,
    },
    dryRunBadge: {
        backgroundColor: colors.warning + '20',
        paddingHorizontal: spacing.sm,
        paddingVertical: spacing.xs,
        borderRadius: borderRadius.sm,
    },
    dryRunText: {
        color: colors.warning,
        fontWeight: fontWeight.medium,
        fontSize: fontSize.sm,
    },
    dateText: {
        color: colors.textSecondary,
        fontSize: fontSize.sm,
        marginTop: spacing.xs,
    },
    summaryCard: {
        backgroundColor: colors.surface,
        margin: spacing.md,
        borderRadius: borderRadius.lg,
        padding: spacing.md,
    },
    summaryTitle: {
        fontSize: fontSize.md,
        fontWeight: fontWeight.semibold,
        color: colors.text,
        marginBottom: spacing.md,
    },
    summaryGrid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
    },
    summaryItem: {
        width: '33%',
        alignItems: 'center',
        paddingVertical: spacing.sm,
    },
    summaryValue: {
        fontSize: fontSize.xl,
        fontWeight: fontWeight.bold,
    },
    summaryLabel: {
        fontSize: fontSize.xs,
        color: colors.textMuted,
    },
    filterContainer: {
        flexDirection: 'row',
        paddingHorizontal: spacing.md,
        gap: spacing.xs,
        marginBottom: spacing.md,
    },
    filterButton: {
        paddingHorizontal: spacing.sm,
        paddingVertical: spacing.xs,
        borderRadius: borderRadius.full,
        backgroundColor: colors.surface,
    },
    filterButtonActive: {
        backgroundColor: colors.primary,
    },
    filterText: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
    },
    filterTextActive: {
        color: '#fff',
        fontWeight: fontWeight.medium,
    },
    recordsContainer: {
        padding: spacing.md,
        paddingTop: 0,
    },
    recordGroup: {
        marginBottom: spacing.md,
    },
    recordGroupHeader: {
        marginBottom: spacing.xs,
    },
    actionBadge: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.xs,
        paddingHorizontal: spacing.sm,
        paddingVertical: spacing.xs,
        borderRadius: borderRadius.sm,
        alignSelf: 'flex-start',
    },
    actionText: {
        fontSize: fontSize.sm,
        fontWeight: fontWeight.medium,
    },
    recordItem: {
        backgroundColor: colors.surface,
        borderRadius: borderRadius.md,
        padding: spacing.sm,
        marginBottom: spacing.xs,
        marginLeft: spacing.md,
    },
    recordPathRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.xs,
    },
    recordPath: {
        flex: 1,
        fontSize: fontSize.sm,
        color: colors.text,
    },
    recordError: {
        fontSize: fontSize.sm,
        color: colors.error,
        marginTop: spacing.xs,
    },
    recordReason: {
        fontSize: fontSize.xs,
        color: colors.textMuted,
        marginTop: spacing.xs,
        fontStyle: 'italic',
    },
    emptyRecords: {
        alignItems: 'center',
        padding: spacing.xl,
    },
    emptyText: {
        color: colors.textMuted,
    },
});