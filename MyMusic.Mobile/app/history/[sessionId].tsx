import {Ionicons} from '@expo/vector-icons';
import {useLocalSearchParams, useNavigation, useRouter} from 'expo-router';
import React, {useCallback, useEffect, useLayoutEffect, useMemo, useState} from 'react';
import {ActivityIndicator, Alert, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import {FlashList} from '@shopify/flash-list';
import type {SyncRecordResponseItem, SyncSessionItem} from '../../src/api/types';
import {deleteSession} from '../../src/api/sync';
import {Card, ErrorDisplay} from '../../src/components/ui';
import type {ErrorDetails} from '../../src/components/ui/ErrorDisplay';
import {useTheme} from '../../src/hooks/useTheme';
import {fetchSessionDetails, fetchSyncHistory} from '../../src/services/syncService';
import {useConfigStore} from '../../src/stores/configStore';

type FilterType = 'all' | 'created' | 'updated' | 'skipped' | 'downloaded' | 'removed' | 'error';

const PAGE_SIZE = 50;

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

type FlatListItem =
    | { type: 'header'; session: SyncSessionItem }
    | { type: 'summary'; session: SyncSessionItem }
    | { type: 'filters'; activeFilter: FilterType; isLoading?: boolean }
    | { type: 'group_header'; action: string; count: number; firstRecord: SyncRecordResponseItem }
    | { type: 'record'; record: SyncRecordResponseItem; repositoryPath: string | null | undefined }
    | { type: 'skeleton' }
    | { type: 'loading' }
    | { type: 'empty' };

export default function SessionDetailScreen() {
    const {sessionId} = useLocalSearchParams<{ sessionId: string }>();
    const navigation = useNavigation();
    const {colors, fontSize, fontWeight, spacing, borderRadius, withAlpha} = useTheme();
    const {deviceId} = useConfigStore();

    const [session, setSession] = useState<SyncSessionItem | null>(null);
    const [records, setRecords] = useState<SyncRecordResponseItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadingMore, setLoadingMore] = useState(false);
    const [isLoadingFilter, setIsLoadingFilter] = useState(false);
    const [filter, setFilter] = useState<FilterType>('all');
    const [error, setError] = useState<ErrorDetails | null>(null);
    const [hasMore, setHasMore] = useState(false);

    const loadRecords = useCallback(async (isInitial: boolean = false, isFilterChange: boolean = false) => {
        if (!deviceId || !sessionId) return;

        try {
            if (isInitial) {
                if (isFilterChange) {
                    setIsLoadingFilter(true);
                } else {
                    setLoading(true);
                }
                // Load session info and first page of records
                const [sessionsResponse, recordsResponse] = await Promise.all([
                    fetchSyncHistory(deviceId, 100),
                    fetchSessionDetails(
                        deviceId,
                        parseInt(sessionId),
                        filter === 'all' ? undefined : filter,
                        undefined,
                        PAGE_SIZE,
                        0,
                        'action_date'
                    )
                ]);

                const foundSession = sessionsResponse.sessions.find(s => s.id === parseInt(sessionId));
                setSession(foundSession || null);
                setRecords(recordsResponse.records);
                setHasMore(recordsResponse.hasMore);
            } else {
                // Load next page
                if (!hasMore || loadingMore) return;

                setLoadingMore(true);
                const currentOffset = records.length;
                const recordsResponse = await fetchSessionDetails(
                    deviceId,
                    parseInt(sessionId),
                    filter === 'all' ? undefined : filter,
                    undefined,
                    PAGE_SIZE,
                    currentOffset,
                    'action_date'
                );

                setRecords(prev => [...prev, ...recordsResponse.records]);
                setHasMore(recordsResponse.hasMore);
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
            setLoadingMore(false);
            setIsLoadingFilter(false);
        }
    }, [deviceId, sessionId, filter, hasMore, loadingMore]);

    // Initial load
    useEffect(() => {
        loadRecords(true);
    }, [deviceId, sessionId]);

    // Reload when filter changes
    useEffect(() => {
        if (!loading && session) {
            // Reset pagination and reload with new filter
            // Don't clear records yet - skeletons will overlay them
            setHasMore(false);
            loadRecords(true, true);
        }
    }, [filter]);

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

    // Server returns records sorted by action then date, so they arrive pre-grouped.
    // Build group headers by detecting action boundaries in the ordered records.
    const groupEntries = useMemo((): Array<{ action: string; records: SyncRecordResponseItem[] }> => {
        const groups: Array<{ action: string; records: SyncRecordResponseItem[] }> = [];
        let currentAction: string | null = null;
        let currentGroup: SyncRecordResponseItem[] = [];

        for (const record of records) {
            if (record.action !== currentAction) {
                if (currentAction !== null && currentGroup.length > 0) {
                    groups.push({action: currentAction, records: currentGroup});
                }
                currentAction = record.action;
                currentGroup = [record];
            } else {
                currentGroup.push(record);
            }
        }

        if (currentAction !== null && currentGroup.length > 0) {
            groups.push({action: currentAction, records: currentGroup});
        }

        return groups;
    }, [records]);

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
            default:
                return 'textSecondary';
        }
    };

    const getActionIcon = (action: string, source: string) => {
        if (action.toLowerCase() === 'error') return 'alert-circle';
        if (action.toLowerCase() === 'skipped') return 'skip-forward';
        if (source === 'Server') return 'cloud-download';
        return 'cloud-upload';
    };

    const getSessionActionCount = useCallback((action: string): number => {
        if (!session) return 0;
        switch (action.toLowerCase()) {
            case 'created': return session.createdCount;
            case 'updated': return session.updatedCount;
            case 'skipped': return session.skippedCount;
            case 'downloaded': return session.downloadedCount;
            case 'removed': return session.removedCount;
            case 'error': return session.errorCount;
            default: return 0;
        }
    }, [session]);

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
    ];

    // Build flat list data for FlashList
    const flatListData = useMemo((): FlatListItem[] => {
        if (!session) return [];

        const data: FlatListItem[] = [
            {type: 'header', session},
            {type: 'summary', session},
            {type: 'filters', activeFilter: filter, isLoading: isLoadingFilter},
        ];

        if (isLoadingFilter) {
            // Show 5 skeleton records while loading new filter
            for (let i = 0; i < 5; i++) {
                data.push({type: 'skeleton'});
            }
        } else if (groupEntries.length === 0) {
            data.push({type: 'empty'});
        } else {
            for (const group of groupEntries) {
                data.push({
                    type: 'group_header',
                    action: group.action,
                    count: getSessionActionCount(group.action),
                    firstRecord: group.records[0],
                });

                group.records.forEach((record) => {
                    data.push({
                        type: 'record',
                        record,
                        repositoryPath: session?.repositoryPath,
                    });
                });
            }
        }

        if (loadingMore) {
            data.push({type: 'loading'});
        }

        return data;
    }, [session, groupEntries, filter, loadingMore, isLoadingFilter, getSessionActionCount]);

    const handleLoadMore = useCallback(() => {
        if (hasMore && !loadingMore && !loading) {
            loadRecords(false);
        }
    }, [hasMore, loadingMore, loading, loadRecords]);

    const renderItem = useCallback(({item}: {item: FlatListItem}) => {
        switch (item.type) {
            case 'header':
                return (
                    <View style={[styles.header, {padding: spacing.md, borderBottomWidth: 1, borderBottomColor: colors.border}]}>
                        <View style={[styles.headerRow, {flexDirection: 'row', gap: spacing.sm}]}>
                            <View style={[styles.statusBadge, {backgroundColor: withAlpha('success', 0.12), paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, borderRadius: borderRadius.sm}]}>
                                <Text style={[styles.statusText, {color: colors.success, fontWeight: fontWeight.medium, fontSize: fontSize.sm}]}>{item.session.status}</Text>
                            </View>
                            {item.session.isDryRun && (
                                <View style={[styles.dryRunBadge, {backgroundColor: withAlpha('warning', 0.12), paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, borderRadius: borderRadius.sm}]}>
                                    <Text style={[styles.dryRunText, {color: colors.warning, fontWeight: fontWeight.medium, fontSize: fontSize.sm}]}>Dry Run</Text>
                                </View>
                            )}
                        </View>
                        <Text style={[styles.dateText, {color: colors.textSecondary, fontSize: fontSize.sm, marginTop: spacing.xs}]}>Started: {formatDate(item.session.startedAt)}</Text>
                        {item.session.completedAt && (
                            <Text style={[styles.dateText, {color: colors.textSecondary, fontSize: fontSize.sm, marginTop: spacing.xs}]}>Completed: {formatDate(item.session.completedAt)}</Text>
                        )}
                    </View>
                );

            case 'summary':
                return (
                    <Card style={{marginHorizontal: spacing.md}}>
                        <Text style={[styles.summaryTitle, {fontSize: fontSize.md, fontWeight: fontWeight.semibold, color: colors.cardText, marginBottom: spacing.md}]}>Summary</Text>
                        <View style={[styles.summaryGrid, {flexDirection: 'row', flexWrap: 'wrap'}]}>
                            <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                                <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.success}]}>{item.session.createdCount}</Text>
                                <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Created</Text>
                            </View>
                            <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                                <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.info}]}>{item.session.updatedCount}</Text>
                                <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Updated</Text>
                            </View>
                            <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                                <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.textMuted}]}>{item.session.skippedCount}</Text>
                                <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Skipped</Text>
                            </View>
                            <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                                <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.syncDownload}]}>{item.session.downloadedCount}</Text>
                                <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Downloaded</Text>
                            </View>
                            <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                                <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.error}]}>{item.session.removedCount}</Text>
                                <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Removed</Text>
                            </View>
                            <View style={[styles.summaryItem, {width: '33%', alignItems: 'center', paddingVertical: spacing.sm}]}>
                                <Text style={[styles.summaryValue, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.error}]}>{item.session.errorCount}</Text>
                                <Text style={[styles.summaryLabel, {fontSize: fontSize.xs, color: colors.cardTextMuted}]}>Errors</Text>
                            </View>
                        </View>
                    </Card>
                );

            case 'filters':
                return (
                    <View style={[styles.filterContainer, {flexDirection: 'row', paddingHorizontal: spacing.lg, gap: spacing.xs, marginBottom: spacing.md, marginTop: spacing.md}]}>
                        {filters.map(f => (
                            <TouchableOpacity
                                key={f.key}
                                style={[
                                    styles.filterButton,
                                    {paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, borderRadius: borderRadius.full, backgroundColor: colors.surface},
                                    item.activeFilter === f.key && {backgroundColor: colors.primary}
                                ]}
                                onPress={() => !item.isLoading && setFilter(f.key)}
                                disabled={item.isLoading}
                            >
                                <Text style={[
                                    styles.filterText,
                                    {fontSize: fontSize.sm, color: colors.textSecondary},
                                    item.activeFilter === f.key && {color: colors.onPrimary, fontWeight: fontWeight.medium},
                                    item.isLoading && {opacity: 0.5}
                                ]}>
                                    {f.label}
                                </Text>
                            </TouchableOpacity>
                        ))}
                        {item.isLoading && (
                            <ActivityIndicator size="small" color={colors.primary} style={{marginLeft: spacing.sm}} />
                        )}
                    </View>
                );

            case 'group_header':
                return (
                    <View style={{marginHorizontal: spacing.md, marginTop: spacing.md, marginBottom: spacing.sm}}>
                        <View style={[styles.actionBadge, {flexDirection: 'row', alignItems: 'center', gap: spacing.xs, paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, borderRadius: borderRadius.sm, alignSelf: 'flex-start', backgroundColor: withAlpha(getActionColorKey(item.action), 0.12)}]}>
                            <Ionicons
                                name={getActionIcon(item.action, item.firstRecord.source) as any}
                                size={14}
                                color={getActionColor(item.action)}
                            />
                            <Text style={[styles.actionText, {fontSize: fontSize.sm, fontWeight: fontWeight.medium, color: getActionColor(item.action)}]}>
                                {item.action} ({item.count})
                            </Text>
                        </View>
                    </View>
                );

            case 'skeleton':
                return (
                    <Card style={{marginHorizontal: spacing.md, paddingVertical: spacing.sm, marginTop: 0}}>
                        <View style={[styles.recordItem]}>
                            <View style={[styles.recordPathRow, {flexDirection: 'row', alignItems: 'center', gap: spacing.xs}]}>
                                <View style={{width: 12, height: 12, backgroundColor: colors.textMuted, borderRadius: 6, opacity: 0.3}} />
                                <View style={{flex: 1, height: 14, backgroundColor: colors.textMuted, borderRadius: 4, opacity: 0.3}} />
                            </View>
                        </View>
                    </Card>
                );

            case 'record':
                return (
                    <Card style={{marginHorizontal: spacing.md, paddingVertical: spacing.sm, marginTop: 0}}>
                        <View style={[styles.recordItem]}>
                            <View style={[styles.recordPathRow, {flexDirection: 'row', alignItems: 'center', gap: spacing.xs}]}>
                                <Ionicons
                                    name={item.record.source === 'Server' ? 'arrow-down' : 'arrow-up'}
                                    size={12}
                                    color={getActionColor(item.record.action)}
                                />
                                <Text style={[styles.recordPath, {flex: 1, fontSize: fontSize.sm, color: colors.cardText}]} numberOfLines={1}>
                                    {formatFilePath(item.record.filePath, item.repositoryPath)}
                                </Text>
                            </View>
                            {item.record.errorMessage && (
                                <Text style={[styles.recordError, {fontSize: fontSize.sm, color: colors.error, marginTop: spacing.xs}]} numberOfLines={2}>
                                    {item.record.errorMessage}
                                </Text>
                            )}
                            {item.record.reason && (
                                <Text style={[styles.recordReason, {fontSize: fontSize.xs, color: colors.cardTextMuted, marginTop: spacing.xs, fontStyle: 'italic'}]} numberOfLines={2}>
                                    {item.record.reason}
                                </Text>
                            )}
                        </View>
                    </Card>
                );

            case 'loading':
                return (
                    <View style={{padding: spacing.lg, alignItems: 'center'}}>
                        <ActivityIndicator size="small" color={colors.primary}/>
                        <Text style={{marginTop: spacing.xs, color: colors.textSecondary, fontSize: fontSize.sm}}>
                            Loading more records...
                        </Text>
                    </View>
                );

            case 'empty':
                return (
                    <View style={[styles.emptyRecords, {alignItems: 'center', padding: spacing.xl, marginHorizontal: spacing.md}]}>
                        <Text style={[styles.emptyText, {color: colors.textMuted}]}>No records match this filter</Text>
                    </View>
                );

            default:
                return null;
        }
    }, [colors, spacing, fontSize, fontWeight, borderRadius, withAlpha]);

    const getItemType = (item: FlatListItem) => item.type;

    if (loading && records.length === 0) {
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
            <FlashList
                data={flatListData}
                renderItem={renderItem}
                keyExtractor={(item, index) => {
                    if (item.type === 'header') return 'header';
                    if (item.type === 'summary') return 'summary';
                    if (item.type === 'filters') return 'filters';
                    if (item.type === 'group_header') return `group-${item.action}`;
                    if (item.type === 'record') return `record-${item.record.filePath}-${index}`;
                    if (item.type === 'loading') return 'loading';
                    if (item.type === 'empty') return 'empty';
                    return `item-${index}`;
                }}
                // @ts-expect-error FlashList types don't include estimatedItemSize but it's required
                estimatedItemSize={100}
                onEndReached={handleLoadMore}
                onEndReachedThreshold={0.5}
                contentContainerStyle={{paddingBottom: spacing.lg}}
            />
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
