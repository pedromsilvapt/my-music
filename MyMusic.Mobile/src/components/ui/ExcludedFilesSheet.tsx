import {Ionicons} from '@expo/vector-icons';
import {FlashList} from '@shopify/flash-list';
import React, {useEffect, useMemo, useRef, useState} from 'react';
import {ActivityIndicator, KeyboardAvoidingView, Modal, Platform, Pressable, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import {useTheme} from '../../hooks/useTheme';
import {scanRepositoryPaths} from '../../services/excludedFilesService';
import {filterExcludedPaths, searchPaths} from '../../services/sync/exclusions';
import {useSyncStore} from '../../stores/syncStore';
import {Input} from './Input';

interface ExcludedFilesSheetProps {
    /** The exclusion rule to list the files of, or null to keep the sheet closed. */
    rule: string | null;
    repositoryPath: string;
    onClose: () => void;
}

interface RepositoryScan {
    key: string;
    paths: Promise<string[]>;
}

/** A drawer listing the music files of the repository that an exclusion rule matches. */
export function ExcludedFilesSheet({rule, repositoryPath, onClose}: ExcludedFilesSheetProps) {
    const {colors, fontSize, fontWeight, fontFamily, spacing, borderRadius} = useTheme();
    const scannerType = useSyncStore((state) => state.options.scannerType);
    const [paths, setPaths] = useState<string[] | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [search, setSearch] = useState('');
    const [refreshes, setRefreshes] = useState(0);

    // The repository is scanned once and every rule is tried against the same list. The scan is kept
    // from the moment it starts, so reopening the sheet while it runs waits for it instead of starting another
    const scan = useRef<RepositoryScan | null>(null);

    useEffect(() => {
        if (rule !== null) {
            setSearch('');
        }
    }, [rule]);

    useEffect(() => {
        if (rule === null) {
            return;
        }

        setError(null);

        const key = `${scannerType}|${repositoryPath}`;
        if (scan.current?.key !== key) {
            const started: RepositoryScan = {key, paths: scanRepositoryPaths(repositoryPath, scannerType)};
            scan.current = started;

            // A failed scan is not kept: opening the sheet again retries it
            started.paths.catch(() => {
                if (scan.current === started) {
                    scan.current = null;
                }
            });
        }

        let cancelled = false;
        setPaths(null);

        scan.current.paths
            .then((scanned) => {
                if (!cancelled) {
                    setPaths(scanned);
                }
            })
            .catch((scanError: any) => {
                if (!cancelled) {
                    setError(scanError?.message || 'Failed to list the files of the music folder');
                }
            });

        return () => {
            cancelled = true;
        };
    }, [rule, repositoryPath, scannerType, refreshes]);

    const excluded = useMemo(
        () => (rule !== null && paths ? filterExcludedPaths(paths, rule) : []),
        [paths, rule]
    );
    const visible = useMemo(() => searchPaths(excluded, search), [excluded, search]);

    const isSearching = search.trim() !== '';
    const isLoading = paths === null && error === null;

    // Drops the scan that was kept, so the files changed since are picked up. The search is left as it is
    const handleRefresh = () => {
        scan.current = null;
        setRefreshes(count => count + 1);
    };

    return (
        <Modal
            visible={rule !== null}
            transparent
            animationType="slide"
            onRequestClose={onClose}
        >
            {/* iOS only: on Android the window of the modal already makes room for the keyboard, and avoiding it
                a second time moves the sheet and leaves a gap under it once the keyboard closes */}
            <KeyboardAvoidingView style={styles.container} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
                <Pressable style={[styles.overlay, {backgroundColor: 'rgba(0, 0, 0, 0.7)'}]} onPress={onClose}>
                    <Pressable
                        style={[styles.sheet, {backgroundColor: colors.card, borderTopLeftRadius: borderRadius.lg, borderTopRightRadius: borderRadius.lg, padding: spacing.md}]}
                        onPress={() => {}}
                    >
                        <View style={[styles.header, {marginBottom: spacing.sm, gap: spacing.sm}]}>
                            <View style={styles.headerText}>
                                <Text style={{fontSize: fontSize.lg, fontWeight: fontWeight.semibold, color: colors.cardText}}>Excluded Files</Text>
                                <Text
                                    style={{fontSize: fontSize.sm, color: colors.cardTextSecondary, fontFamily: fontFamily.monospace, marginTop: spacing.xs}}
                                    numberOfLines={1}
                                >
                                    {rule}
                                </Text>
                            </View>
                            {!isLoading && error === null && (
                                <Text style={{fontSize: fontSize.sm, color: colors.cardTextMuted}}>
                                    {isSearching ? `${visible.length} / ${excluded.length}` : excluded.length}
                                </Text>
                            )}
                            <TouchableOpacity
                                onPress={handleRefresh}
                                disabled={isLoading}
                                style={{padding: spacing.xs}}
                                accessibilityLabel="Refresh"
                            >
                                <Ionicons name="refresh" size={22} color={isLoading ? colors.cardTextMuted : colors.cardTextSecondary}/>
                            </TouchableOpacity>
                            <TouchableOpacity onPress={onClose} style={{padding: spacing.xs}} accessibilityLabel="Close">
                                <Ionicons name="close" size={24} color={colors.cardTextSecondary}/>
                            </TouchableOpacity>
                        </View>

                        <View>
                            <Input
                                placeholder="Search files"
                                value={search}
                                onChangeText={setSearch}
                                autoCapitalize="none"
                                autoCorrect={false}
                                style={{paddingRight: spacing.xl + spacing.sm}}
                                containerStyle={{marginBottom: spacing.sm}}
                                variant="card"
                            />
                            {search !== '' && (
                                <TouchableOpacity
                                    onPress={() => setSearch('')}
                                    style={[styles.clearSearch, {right: spacing.sm, bottom: spacing.sm}]}
                                    accessibilityLabel="Clear search"
                                >
                                    <Ionicons name="close-circle" size={20} color={colors.cardTextMuted}/>
                                </TouchableOpacity>
                            )}
                        </View>

                        <View style={styles.body}>
                            {isLoading && (
                                <View style={[styles.message, {gap: spacing.sm}]}>
                                    <ActivityIndicator size="large" color={colors.primary}/>
                                    <Text style={{fontSize: fontSize.sm, color: colors.cardTextMuted}}>Listing the music folder...</Text>
                                </View>
                            )}
                            {error !== null && (
                                <View style={styles.message}>
                                    <Text style={{fontSize: fontSize.md, color: colors.error, textAlign: 'center'}}>{error}</Text>
                                </View>
                            )}
                            {!isLoading && error === null && visible.length === 0 && (
                                <View style={styles.message}>
                                    <Text style={{fontSize: fontSize.md, color: colors.cardTextMuted, textAlign: 'center'}}>
                                        {isSearching && excluded.length > 0
                                            ? 'No excluded file matches the search'
                                            : 'No music files match this rule'}
                                    </Text>
                                </View>
                            )}
                            {!isLoading && error === null && visible.length > 0 && (
                                <FlashList
                                    data={visible}
                                    keyExtractor={(path) => path}
                                    keyboardShouldPersistTaps="handled"
                                    renderItem={({item}) => (
                                        <Text
                                            style={{
                                                fontSize: fontSize.sm,
                                                color: colors.cardText,
                                                paddingVertical: spacing.sm,
                                                borderBottomWidth: StyleSheet.hairlineWidth,
                                                borderBottomColor: colors.cardBorder,
                                            }}
                                        >
                                            {item}
                                        </Text>
                                    )}
                                />
                            )}
                        </View>
                    </Pressable>
                </Pressable>
            </KeyboardAvoidingView>
        </Modal>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
    },
    overlay: {
        flex: 1,
        justifyContent: 'flex-end',
    },
    sheet: {
        // A fixed height, as the virtualized list needs a bounded parent
        height: '75%',
    },
    header: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    headerText: {
        flex: 1,
    },
    clearSearch: {
        position: 'absolute',
        padding: 4,
    },
    body: {
        flex: 1,
    },
    message: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
});
