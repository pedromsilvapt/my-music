import {Ionicons} from '@expo/vector-icons';
import {useRouter} from 'expo-router';
import React from 'react';
import {ScrollView, StyleSheet, Switch, Text, View} from 'react-native';
import {Button} from '../../src/components/ui';
import {useTheme} from '../../src/hooks/useTheme';
import {useSyncStore} from '../../src/stores/syncStore';

export default function SyncOptionsScreen() {
    const router = useRouter();
    const {colors, fontSize, fontWeight, spacing, borderRadius, withAlpha} = useTheme();
    const {options, setOptions} = useSyncStore();

    const handleStartSync = () => {
        router.push('/sync/progress');
    };

    const handleCancel = () => {
        router.back();
    };

    return (
        <View style={[styles.container, {backgroundColor: colors.backgroundSecondary}]}>
            <ScrollView style={styles.scrollView} contentContainerStyle={[styles.content, {padding: spacing.md, paddingBottom: spacing.xl}]}>
                <View style={[styles.header, {marginBottom: spacing.lg}]}>
                    <Text style={[styles.title, {fontSize: fontSize.xl, fontWeight: fontWeight.bold, color: colors.text}]}>Sync Options</Text>
                    <Text style={[styles.subtitle, {fontSize: fontSize.md, color: colors.textSecondary, marginTop: spacing.xs}]}>Configure how you want to sync your music</Text>
                </View>

                <View style={[styles.optionCard, {backgroundColor: colors.card, borderRadius: borderRadius.lg, padding: spacing.md, marginBottom: spacing.md}]}>
                    <View style={styles.optionRow}>
                        <View style={[styles.optionInfo, {marginRight: spacing.md}]}>
                            <Text style={[styles.optionTitle, {fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText}]}>Force Upload</Text>
                            <Text style={[styles.optionDescription, {fontSize: fontSize.sm, color: colors.cardTextSecondary, marginTop: spacing.xs}]}>
                                Upload files even if the server already has them
                            </Text>
                        </View>
                        <Switch
                            value={options.force}
                            onValueChange={(value) => setOptions({force: value})}
                            trackColor={{false: colors.cardBorder, true: colors.primary}}
                            thumbColor={colors.cardText}
                        />
                    </View>
                </View>

                <View style={[styles.optionCard, {backgroundColor: colors.card, borderRadius: borderRadius.lg, padding: spacing.md, marginBottom: spacing.md}]}>
                    <View style={styles.optionRow}>
                        <View style={[styles.optionInfo, {marginRight: spacing.md}]}>
                            <Text style={[styles.optionTitle, {fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText}]}>Dry Run</Text>
                            <Text style={[styles.optionDescription, {fontSize: fontSize.sm, color: colors.cardTextSecondary, marginTop: spacing.xs}]}>
                                Simulate sync without making any actual changes
                            </Text>
                        </View>
                        <Switch
                            value={options.dryRun}
                            onValueChange={(value) => setOptions({dryRun: value})}
                            trackColor={{false: colors.cardBorder, true: colors.primary}}
                            thumbColor={colors.cardText}
                        />
                    </View>
                    {options.dryRun && (
                        <View style={[styles.warningBadge, {gap: spacing.xs, marginTop: spacing.md, padding: spacing.sm, backgroundColor: colors.cardSecondary, borderRadius: borderRadius.md}]}>
                            <Ionicons name="information-circle" size={16} color={colors.warning}/>
                            <Text style={[styles.warningText, {fontSize: fontSize.sm, color: colors.warning}]}>Changes will be simulated only</Text>
                        </View>
                    )}
                </View>

                <View style={[styles.optionCard, {backgroundColor: colors.card, borderRadius: borderRadius.lg, padding: spacing.md, marginBottom: spacing.md}]}>
                    <View style={styles.optionRow}>
                        <View style={[styles.optionInfo, {marginRight: spacing.md}]}>
                            <Text style={[styles.optionTitle, {fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText}]}>Auto Confirm Deletions</Text>
                            <Text style={[styles.optionDescription, {fontSize: fontSize.sm, color: colors.cardTextSecondary, marginTop: spacing.xs}]}>
                                Delete files without asking for confirmation
                            </Text>
                        </View>
                        <Switch
                            value={options.autoConfirm}
                            onValueChange={(value) => setOptions({autoConfirm: value})}
                            trackColor={{false: colors.cardBorder, true: colors.primary}}
                            thumbColor={colors.cardText}
                        />
                    </View>
                    {!options.autoConfirm && (
                        <View style={[styles.warningBadge, {gap: spacing.xs, marginTop: spacing.md, padding: spacing.sm, backgroundColor: colors.cardSecondary, borderRadius: borderRadius.md}]}>
                            <Ionicons name="warning" size={16} color={colors.warning}/>
                            <Text style={[styles.warningText, {fontSize: fontSize.sm, color: colors.warning}]}>You will be asked to confirm each deletion</Text>
                        </View>
                    )}
                </View>

                <View style={[styles.optionCard, {backgroundColor: colors.card, borderRadius: borderRadius.lg, padding: spacing.md, marginBottom: spacing.md}]}>
                    <View style={styles.optionRow}>
                        <View style={[styles.optionInfo, {marginRight: spacing.md}]}>
                            <Text style={[styles.optionTitle, {fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText}]}>Treat Conflicts as Errors</Text>
                            <Text style={[styles.optionDescription, {fontSize: fontSize.sm, color: colors.cardTextSecondary, marginTop: spacing.xs}]}>
                                Skip conflicting files instead of asking what to do
                            </Text>
                        </View>
                        <Switch
                            value={options.treatConflictsAsErrors}
                            onValueChange={(value) => setOptions({treatConflictsAsErrors: value})}
                            trackColor={{false: colors.cardBorder, true: colors.primary}}
                            thumbColor={colors.cardText}
                        />
                    </View>
                    {!options.treatConflictsAsErrors && (
                        <View style={[styles.warningBadge, {gap: spacing.xs, marginTop: spacing.md, padding: spacing.sm, backgroundColor: colors.cardSecondary, borderRadius: borderRadius.md}]}>
                            <Ionicons name="information-circle" size={16} color={colors.info}/>
                            <Text style={[styles.warningText, {fontSize: fontSize.sm, color: colors.cardText}]}>You will be asked to choose upload, download, or skip for each conflict</Text>
                        </View>
                    )}
                </View>

                {options.dryRun && (
                    <View style={[styles.dryRunNotice, {gap: spacing.sm, padding: spacing.md, backgroundColor: withAlpha('info', 0.12), borderRadius: borderRadius.lg, borderWidth: 1, borderColor: colors.info}]}>
                        <Ionicons name="information-circle-outline" size={20} color={colors.info}/>
                        <Text style={[styles.dryRunText, {flex: 1, fontSize: fontSize.sm, color: colors.text, lineHeight: 20}]}>
                            In dry run mode, no files will be uploaded, downloaded, or deleted. All operations will be
                            recorded in the sync session as if they were performed.
                        </Text>
                    </View>
                )}
            </ScrollView>

            <View style={[styles.buttonContainer, {padding: spacing.md, gap: spacing.md, borderTopWidth: 1, borderTopColor: colors.border}]}>
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
    },
    scrollView: {
        flex: 1,
    },
    content: {
        padding: 16,
        paddingBottom: 32,
    },
    header: {
        marginBottom: 24,
    },
    title: {
        fontSize: 18,
    },
    subtitle: {
        fontSize: 14,
    },
    optionCard: {
        marginBottom: 16,
    },
    optionRow: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    optionInfo: {
        flex: 1,
    },
    optionTitle: {
        fontSize: 14,
    },
    optionDescription: {
        fontSize: 12,
    },
    warningBadge: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    warningText: {
        fontSize: 12,
    },
    dryRunNotice: {
        flexDirection: 'row',
        alignItems: 'flex-start',
    },
    dryRunText: {
        flex: 1,
        fontSize: 12,
        lineHeight: 20,
    },
    buttonContainer: {
        flexDirection: 'row',
        borderTopWidth: 1,
    },
    cancelButton: {
        flex: 1,
    },
    startButton: {
        flex: 2,
    },
});
