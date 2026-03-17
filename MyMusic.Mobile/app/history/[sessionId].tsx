import {Ionicons} from '@expo/vector-icons';
import {useLocalSearchParams, useNavigation, useRouter} from 'expo-router';
import React, {useEffect, useLayoutEffect, useMemo, useState} from 'react';
import {ActivityIndicator, Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import type {SyncRecordResponseItem, SyncSessionItem} from '../../src/api/types';
import {deleteSession} from '../../src/api/sync';
import {Card, ErrorDisplay} from '../../src/components/ui';
import type {ErrorDetails} from '../../src/components/ui/ErrorDisplay';
import {useTheme} from '../../src/hooks/useTheme';
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
    const {colors, fontSize, fontWeight, spacing, borderRadius, withAlpha} = useTheme();
    const {deviceId} = useConfigStore();

    const [session, setSession] = useState<SyncSessionItem | null>(null);
    const [records, setRecords] = useState<SyncRecordResponseItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [filter, setFilter] = useState<FilterType>('all');
    const [error, setError] = useState<ErrorDetails | null>(null);

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
                setError(null);
            } catch (err: any) {
                console.error('Failed to load session:', err);
                setError({
                    status: err?.status,
                    message: err?.message || 'Failed to load session',
                    url: err?.url,
                    responseBody: err?.details || err?.responseBody,
                    stack: err?.stack,
                });
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
                        } catch (err: any) {
                            const deleteError: ErrorDetails = {
                                status: err?.status,
                                message: err?.message || 'Failed to delete session',
                                url: err?.url,
                                responseBody: err?.details || err?.responseBody,
                                stack: err?.stack,
                            };
                            Alert.alert(
                                'Error',
                                'Failed to delete session',
                                [
                                    {text: 'OK'},
                                    {text: 'Details', onPress: () => Alert.alert('Error Details', formatErrorDetails(deleteError))},
                                ]
                            );
                        }
                    },
                },
            ]
        );
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

    useLayoutEffect(() => {
        navigation.setOptions({
            headerRight: () => (
                <TouchableOpacity onPress={handleDelete} style={{padding: spacing.sm}}>
                    <Ionicons name="trash-outline" size={22} color={colors.text}/>
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

    const getActionColorKey = (action: string): keyof typeof colors => {
        switch (action.toLowerCase()) {
            case 'created':
                return 'success';
            case 'updated':
                return 'info';
            case 'skipped':
                return 'textMuted';
            case 'downloaded':
                return 'syncDownload';
            case 'removed':
                return 'error';
            case 'error':
                return 'error';
            case 'conflict':
                return 'warning';
            default:
                return 'textSecondary';
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
            <View style={[styles.loadingContainer, {backgroundColor: colors.backgroundSecondary, justifyContent: 'center', alignItems: 'center'}]}>
                <ActivityIndicator size="large" color={colors.primary}/>
            </View>
        );
    }

    if (error) {
        return (
            <View style={[styles.container, {backgroundColor: colors.backgroundSecondary}]}>
                <View style={[styles.errorDisplayContainer, {flex: 1, padding: spacing.md}]}>
                    <ErrorDisplay 
                        error={error} 
                        onDismiss={() => router.back()}
                    />
                </View>
            </View>
        );
    }

    if (!session) {
        return (
            <View style={[styles.errorContainer, {backgroundColor: colors.backgroundSecondary, justifyContent: 'center', alignItems: 'center'}]}>
                <Text style={[styles.errorText, {color: colors.textSecondary, fontSize: fontSize.lg}]}>Session not found</Text>
            </View>
        );
    }

    return (
        <View style={[styles.container, {backgroundColor: colors.backgroundSecondary}]}>
            <ScrollView>
                <View style={[styles.header, {padding: spacing.md, borderBottomWidth: 1, borderBottomColor: colors.border}]}>
                    <View style={[styles.headerRow, {flexDirection: 'row', gap: spacing.sm}]}>
                        <View style={[styles.statusBadge, {backgroundColor: withAlpha('success', 0.12), paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, borderRadius: borderRadius.sm}]}>
                            <Text style={[styles.statusText, {color: colors.success, fontWeight: fontWeight.medium, fontSize: fontSize.sm}]}>{session.status}</Text>
                        </View>
                        {session.isDryRun && (
                            <View style={[styles.dryRunBadge, {backgroundColor: withAlpha('warning', 0.12), paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, borderRadius: borderRadius.sm}]}>
                                <Text style={[styles.dryRunText, {color: colors.warning, fontWeight: fontWeight.medium, fontSize: fontSize.sm}]}>Dry Run</Text>
                            </View>
                        )}
                    </View>
                    <Text style={[styles.dateText, {color: colors.textSecondary, fontSize: fontSize.sm, marginTop: spacing.xs}]}>Started: {formatDate(session.startedAt)}</Text>
                    {session.completedAt && (
                        <Text style={[styles.dateText, {color: colors.textSecondary, fontSize: fontSize.sm, marginTop: spacing.xs}]}>Completed: {formatDate(session.completedAt)}</Text>
                    )}
                </View>

                <Card style={{marginHorizontal: spacing.md}}>
                    <Text style={[styles.summaryTitle, {fontSize: fontSize.md, fontWeight: fontWeight.semibold, color: colors.cardText, marginBottom: spacing.md}]}>Summary</Text>
                    <View style={[styles.summaryGrid, {flexDirection: 'row', flexWrap: 'wrap'}]}>
                        <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                            <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.success}]}>{session.createdCount}</Text>
                            <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Created</Text>
                        </View>
                        <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                            <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.info}]}>{session.updatedCount}</Text>
                            <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Updated</Text>
                        </View>
                        <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                            <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.textMuted}]}>{session.skippedCount}</Text>
                            <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Skipped</Text>
                        </View>
                        <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                            <Text
                                style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.syncDownload}]}>{session.downloadedCount}</Text>
                            <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Downloaded</Text>
                        </View>
                        <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                            <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.error}]}>{session.removedCount}</Text>
                            <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Removed</Text>
                        </View>
                        <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                            <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.error}]}>{session.errorCount}</Text>
                            <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Errors</Text>
                        </View>
                    </View>
                </Card>

                <View style={[styles.filterContainer, {flexDirection: 'row', paddingHorizontal: spacing.lg, gap: spacing.xs, marginBottom: spacing.md}]}>
                    {filters.map(f => (
                        <TouchableOpacity
                            key={f.key}
                            style={[
                                styles.filterButton,
                                {paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, borderRadius: borderRadius.full, backgroundColor: colors.surface},
                                filter === f.key && {backgroundColor: colors.primary}
                            ]}
                            onPress={() => setFilter(f.key)}
                        >
                            <Text style={[
                                styles.filterText,
                                {fontSize: fontSize.sm, color: colors.textSecondary},
                                filter === f.key && {color: colors.onPrimary, fontWeight: fontWeight.medium}
                            ]}>
                                {f.label}
                            </Text>
                        </TouchableOpacity>
                    ))}
                </View>

                <View style={[styles.recordsContainer, {padding: spacing.lg, paddingTop: 0}]}>
                    {sortedGroupEntries.map(([action, actionRecords]) => (
                        <Card key={action} style={{marginHorizontal: spacing.md}}>
                            <View style={[styles.recordGroupHeader, {marginBottom: spacing.xs}]}>
                                <View style={[styles.actionBadge, {flexDirection: 'row', alignItems: 'center', gap: spacing.xs, paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, borderRadius: borderRadius.sm, alignSelf: 'flex-start', backgroundColor: withAlpha(getActionColorKey(action), 0.12)}]}>
                                    <Ionicons
                                        name={getActionIcon(action, actionRecords[0]?.source || '') as any}
                                        size={14}
                                        color={getActionColor(action)}
                                    />
                                    <Text style={[styles.actionText, {fontSize: fontSize.sm, fontWeight: fontWeight.medium, color: getActionColor(action)}]}>
                                        {action} ({actionRecords.length})
                                    </Text>
                                </View>
                            </View>
                            {actionRecords.map((record, idx) => (
                                <View key={idx} style={[styles.recordItem, {paddingVertical: spacing.sm, borderBottomWidth: idx < actionRecords.length - 1 ? 1 : 0, borderBottomColor: colors.cardBorder}]}>
                                    <View style={[styles.recordPathRow, {flexDirection: 'row', alignItems: 'center', gap: spacing.xs}]}>
                                        <Ionicons
                                            name={record.source === 'Server' ? 'arrow-down' : 'arrow-up'}
                                            size={12}
                                            color={getActionColor(action)}
                                        />
                                        <Text style={[styles.recordPath, {flex: 1, fontSize: fontSize.sm, color: colors.cardText}]} numberOfLines={1}>
                                            {formatFilePath(record.filePath, session?.repositoryPath)}
                                        </Text>
                                    </View>
                                    {record.errorMessage && (
                                        <Text style={[styles.recordError, {fontSize: fontSize.sm, color: colors.error, marginTop: spacing.xs}]} numberOfLines={2}>
                                            {record.errorMessage}
                                        </Text>
                                    )}
                                    {record.reason && (
                                        <Text style={[styles.recordReason, {fontSize: fontSize.xs, color: colors.cardTextMuted, marginTop: spacing.xs, fontStyle: 'italic'}]} numberOfLines={2}>
                                            {record.reason}
                                        </Text>
                                    )}
                                </View>
                            ))}
                        </Card>
                    ))}

                    {filteredRecords.length === 0 && (
                        <View style={[styles.emptyRecords, {alignItems: 'center', padding: spacing.xl}]}>
                            <Text style={[styles.emptyText, {color: colors.textMuted}]}>No records match this filter</Text>
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
    },
    loadingContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
    errorContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
    errorDisplayContainer: {
        flex: 1,
    },
    errorText: {
        fontSize: 18,
    },
    header: {
        borderBottomWidth: 1,
    },
    headerRow: {
        flexDirection: 'row',
    },
    statusBadge: {
        paddingHorizontal: 8,
        paddingVertical: 4,
    },
    statusText: {
        fontWeight: '500',
        fontSize: 12,
    },
    dryRunBadge: {
        paddingHorizontal: 8,
        paddingVertical: 4,
    },
    dryRunText: {
        fontWeight: '500',
        fontSize: 12,
    },
    dateText: {
        fontSize: 12,
    },
    summaryTitle: {
        fontSize: 14,
        fontWeight: '600',
    },
    summaryGrid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
    },
    summaryItem: {
        width: '33%',
        alignItems: 'center',
    },
    summaryValue: {
        fontSize: 18,
        fontWeight: '700',
    },
    summaryLabel: {
        fontSize: 10,
    },
    filterContainer: {
        flexDirection: 'row',
    },
    filterButton: {
        paddingHorizontal: 8,
        paddingVertical: 4,
        borderRadius: 999,
    },
    filterText: {
        fontSize: 12,
    },
    recordsContainer: {
        padding: 16,
    },
    recordGroupHeader: {
        marginBottom: 4,
    },
    actionBadge: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    actionText: {
        fontSize: 12,
        fontWeight: '500',
    },
    recordItem: {
        marginBottom: 4,
    },
    recordPathRow: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    recordPath: {
        flex: 1,
        fontSize: 12,
    },
    recordError: {
        fontSize: 12,
    },
    recordReason: {
        fontSize: 10,
        fontStyle: 'italic',
    },
    emptyRecords: {
        alignItems: 'center',
    },
    emptyText: {
        fontSize: 12,
    },
});
