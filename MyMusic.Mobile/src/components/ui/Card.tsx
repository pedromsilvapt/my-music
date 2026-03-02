import React from 'react';
import {StyleSheet, View, ViewStyle} from 'react-native';
import {borderRadius, colors, spacing} from '../../constants/theme';

interface CardProps {
    children: React.ReactNode;
    style?: ViewStyle;
}

export function Card({children, style}: CardProps) {
    return <View style={[styles.card, style]}>{children}</View>;
}

const styles = StyleSheet.create({
    card: {
        backgroundColor: colors.surface,
        borderRadius: borderRadius.lg,
        padding: spacing.md,
        marginBottom: spacing.md,
    },
});