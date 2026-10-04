import {Ionicons} from '@expo/vector-icons';
import React, {useMemo} from 'react';
import {StyleSheet, Text, View, ViewStyle} from 'react-native';
import type {SyncSessionItem} from '../../api/types';
import {useTheme} from '../../hooks/useTheme';
import {buildSessionCounterSlots} from '../../services/sync/sessionCounters';

interface SessionCountersProps {
    session: SyncSessionItem;
    size?: 'sm' | 'lg';
    style?: ViewStyle;
}

/**
 * The six counter slots of a sync session (Remote, Local, Links, Conflict, Error, Skipped).
 * A slot shows an icon and value for each of its actions that has records, or a single 0.
 */
export function SessionCounters({session, size = 'lg', style}: SessionCountersProps) {
    const {colors, fontSize, fontWeight, spacing} = useTheme();
    const slots = useMemo(() => buildSessionCounterSlots(session), [session]);

    const valueSize = size === 'lg' ? fontSize.xl : fontSize.md;
    const iconSize = size === 'lg' ? 16 : 13;

    return (
        <View style={[styles.grid, style]}>
            {slots.map((slot) => (
                <View key={slot.key} testID={`session-counter-${slot.key}`} style={[styles.slot, {paddingVertical: spacing.sm}]}>
                    <View style={[styles.parts, {columnGap: spacing.sm}]}>
                        {slot.parts.length === 0 ? (
                            <Text style={{fontSize: valueSize, fontWeight: fontWeight.bold, color: colors.cardTextMuted}}>0</Text>
                        ) : slot.parts.map((part) => (
                            <View key={part.action} testID={`session-counter-${part.action}`} style={[styles.part, {gap: 2}]}>
                                <Ionicons name={part.icon as keyof typeof Ionicons.glyphMap} size={iconSize} color={colors[part.colorKey]}/>
                                <Text style={{fontSize: valueSize, fontWeight: fontWeight.bold, color: colors[part.colorKey]}}>{part.value}</Text>
                            </View>
                        ))}
                    </View>
                    <Text style={{fontSize: fontSize.xs, color: colors.cardTextMuted}}>{slot.label}</Text>
                </View>
            ))}
        </View>
    );
}

const styles = StyleSheet.create({
    grid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
    },
    slot: {
        width: '33%',
        alignItems: 'center',
    },
    parts: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        justifyContent: 'center',
        alignItems: 'center',
    },
    part: {
        flexDirection: 'row',
        alignItems: 'center',
    },
});
