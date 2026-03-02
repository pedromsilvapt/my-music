import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React from 'react';
import {ScrollView, StyleSheet, Switch, Text, View} from 'react-native';
import {Button} from '../../src/components/ui';
import {borderRadius, colors, fontSize, fontWeight, spacing} from '../../src/constants/theme';
import {useSyncStore} from '../../src/stores/syncStore';

export default function SyncOptionsScreen() {
    const router = useRouter();
    const {options, setOptions} = useSyncStore();

    const handleStartSync = () => {
        router.push('/sync/progress');
    };

    const handleCancel = () => {
        router.back();
    };

    return (
        <View style={styles.container}>
            <ScrollView style={styles.scrollView} contentContainerStyle={styles.content}>
                <View style={styles.header}>
                    <Text style={styles.title}>Sync Options</Text>
                    <Text style={styles.subtitle}>Configure how you want to sync your music</Text>
                </View>

                <View style={styles.optionCard}>
                    <View style={styles.optionRow}>
                        <View style={styles.optionInfo}>
                            <Text style={styles.optionTitle}>Force Upload</Text>
                            <Text style={styles.optionDescription}>
                                Upload files even if the server already has them
                            </Text>
                        </View>
                        <Switch
                            value={options.force}
                            onValueChange={(value) => setOptions({force: value})}
                            trackColor={{false: colors.border, true: colors.primary}}
                            thumbColor={colors.text}
                        />
                    </View>
                </View>

                <View style={styles.optionCard}>
                    <View style={styles.optionRow}>
                        <View style={styles.optionInfo}>
                            <Text style={styles.optionTitle}>Dry Run</Text>
                            <Text style={styles.optionDescription}>
                                Simulate sync without making any actual changes
                            </Text>
                        </View>
                        <Switch
                            value={options.dryRun}
                            onValueChange={(value) => setOptions({dryRun: value})}
                            trackColor={{false: colors.border, true: colors.primary}}
                            thumbColor={colors.text}
                        />
                    </View>
                    {options.dryRun && (
                        <View style={styles.warningBadge}>
                            <Ionicons name="information-circle" size={16} color={colors.warning}/>
                            <Text style={styles.warningText}>Changes will be simulated only</Text>
                        </View>
                    )}
                </View>

                <View style={styles.optionCard}>
                    <View style={styles.optionRow}>
                        <View style={styles.optionInfo}>
                            <Text style={styles.optionTitle}>Auto Confirm Deletions</Text>
                            <Text style={styles.optionDescription}>
                                Delete files without asking for confirmation
                            </Text>
                        </View>
                        <Switch
                            value={options.autoConfirm}
                            onValueChange={(value) => setOptions({autoConfirm: value})}
                            trackColor={{false: colors.border, true: colors.primary}}
                            thumbColor={colors.text}
                        />
                    </View>
                    {!options.autoConfirm && (
                        <View style={styles.warningBadge}>
                            <Ionicons name="warning" size={16} color={colors.warning}/>
                            <Text style={styles.warningText}>You will be asked to confirm each deletion</Text>
                        </View>
                    )}
                </View>

                {options.dryRun && (
                    <View style={styles.dryRunNotice}>
                        <Ionicons name="information-circle-outline" size={20} color={colors.info}/>
                        <Text style={styles.dryRunText}>
                            In dry run mode, no files will be uploaded, downloaded, or deleted. All operations will be
                            recorded in the sync session as if they were performed.
                        </Text>
                    </View>
                )}
            </ScrollView>

            <View style={styles.buttonContainer}>
                <Button
                    title="Cancel"
                    onPress={handleCancel}
                    variant="secondary"
                    style={styles.cancelButton}
                />
                <Button
                    title="Start Sync"
                    onPress={handleStartSync}
                    style={styles.startButton}
                />
            </View>
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.backgroundDark,
    },
    scrollView: {
        flex: 1,
    },
    content: {
        padding: spacing.md,
        paddingBottom: spacing.xl,
    },
    header: {
        marginBottom: spacing.lg,
    },
    title: {
        fontSize: fontSize.xl,
        fontWeight: fontWeight.bold,
        color: colors.text,
    },
    subtitle: {
        fontSize: fontSize.md,
        color: colors.textSecondary,
        marginTop: spacing.xs,
    },
    optionCard: {
        backgroundColor: colors.surface,
        borderRadius: borderRadius.lg,
        padding: spacing.md,
        marginBottom: spacing.md,
    },
    optionRow: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    optionInfo: {
        flex: 1,
        marginRight: spacing.md,
    },
    optionTitle: {
        fontSize: fontSize.md,
        fontWeight: fontWeight.medium,
        color: colors.text,
    },
    optionDescription: {
        fontSize: fontSize.sm,
        color: colors.textSecondary,
        marginTop: spacing.xs,
    },
    warningBadge: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.xs,
        marginTop: spacing.md,
        padding: spacing.sm,
        backgroundColor: colors.backgroundDark,
        borderRadius: borderRadius.md,
    },
    warningText: {
        fontSize: fontSize.sm,
        color: colors.warning,
    },
    dryRunNotice: {
        flexDirection: 'row',
        alignItems: 'flex-start',
        gap: spacing.sm,
        padding: spacing.md,
        backgroundColor: colors.info + '20',
        borderRadius: borderRadius.lg,
        borderWidth: 1,
        borderColor: colors.info,
    },
    dryRunText: {
        flex: 1,
        fontSize: fontSize.sm,
        color: colors.info,
        lineHeight: 20,
    },
    buttonContainer: {
        flexDirection: 'row',
        padding: spacing.md,
        gap: spacing.md,
        borderTopWidth: 1,
        borderTopColor: colors.border,
    },
    cancelButton: {
        flex: 1,
    },
    startButton: {
        flex: 2,
    },
});
