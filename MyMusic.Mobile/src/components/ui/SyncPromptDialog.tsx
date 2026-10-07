import {Ionicons} from '@expo/vector-icons';
import React, {useEffect, useState} from 'react';
import {Modal, Pressable, StyleSheet, Text, View} from 'react-native';
import {useTheme} from '../../hooks/useTheme';
import type {ConflictResolution} from '../../services/sync/types';
import {useSyncStore} from '../../stores/syncStore';
import {Button} from './Button';

const CONFLICT_LABELS: Record<ConflictResolution, string> = {
    upload: 'Upload',
    download: 'Download',
    skip: 'Skip',
};

/**
 * The question the running sync waits on: whether to delete a file, or what to do with a conflict. The answer
 * can be given for every remaining question of the same kind. It can only be closed by answering.
 */
export function SyncPromptDialog() {
    const {colors, fontSize, fontWeight, spacing, borderRadius} = useTheme();
    const prompt = useSyncStore((state) => state.pendingPrompt);
    const closePrompt = useSyncStore((state) => state.closePrompt);
    const [applyToAll, setApplyToAll] = useState(false);

    // Each question starts as an answer for that file only
    useEffect(() => {
        setApplyToAll(false);
    }, [prompt]);

    if (prompt === null) {
        return null;
    }

    const isDeletion = prompt.kind === 'deletion';

    const answerDeletion = (confirmed: boolean) => {
        if (prompt.kind === 'deletion') {
            prompt.answer({value: confirmed, applyToAll});
            closePrompt();
        }
    };

    const answerConflict = (resolution: ConflictResolution) => {
        if (prompt.kind === 'conflict') {
            prompt.answer({value: resolution, applyToAll});
            closePrompt();
        }
    };

    return (
        <Modal visible transparent animationType="fade" onRequestClose={() => {}}>
            <View style={[styles.overlay, {backgroundColor: 'rgba(0, 0, 0, 0.7)', padding: spacing.lg}]}>
                <View testID="sync-prompt-dialog" style={[styles.dialog, {backgroundColor: colors.card, borderRadius: borderRadius.lg, padding: spacing.lg, gap: spacing.md}]}>
                    <Text style={{fontSize: fontSize.lg, fontWeight: fontWeight.bold, color: colors.cardText}}>
                        {isDeletion ? 'Delete File?' : 'Conflict Detected'}
                    </Text>
                    <Text style={{fontSize: fontSize.md, color: colors.cardTextSecondary}}>
                        {isDeletion
                            ? `Do you want to delete "${prompt.filePath}"?`
                            : `The file "${prompt.filePath}" has been modified both locally and on the server. What would you like to do?`}
                    </Text>

                    <Pressable
                        style={[styles.applyToAll, {gap: spacing.sm}]}
                        onPress={() => setApplyToAll((value) => !value)}
                        accessibilityRole="checkbox"
                        accessibilityState={{checked: applyToAll}}
                    >
                        <Ionicons name={applyToAll ? 'checkbox' : 'square-outline'} size={24} color={applyToAll ? colors.primary : colors.cardTextSecondary}/>
                        <Text style={{fontSize: fontSize.md, color: colors.cardText}}>
                            {isDeletion ? 'Apply to all remaining files' : 'Apply to all remaining conflicts'}
                        </Text>
                    </Pressable>

                    <View style={[styles.buttons, {gap: spacing.sm}]}>
                        {prompt.kind === 'deletion' ? (
                            <>
                                <Button title="Skip" variant="outline" size="small" onPress={() => answerDeletion(false)}/>
                                <Button title="Delete" variant="danger" size="small" onPress={() => answerDeletion(true)}/>
                            </>
                        ) : (
                            prompt.choices.map((choice) => (
                                <Button
                                    key={choice}
                                    title={CONFLICT_LABELS[choice]}
                                    variant={choice === 'skip' ? 'outline' : 'primary'}
                                    size="small"
                                    onPress={() => answerConflict(choice)}
                                />
                            ))
                        )}
                    </View>
                </View>
            </View>
        </Modal>
    );
}

const styles = StyleSheet.create({
    overlay: {
        flex: 1,
        justifyContent: 'center',
    },
    dialog: {
        width: '100%',
    },
    applyToAll: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    buttons: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        justifyContent: 'flex-end',
    },
});
