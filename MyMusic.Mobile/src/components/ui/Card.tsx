import React from 'react';
import {StyleSheet, View, ViewStyle} from 'react-native';
import {useTheme} from '../../hooks/useTheme';

interface CardProps {
    children: React.ReactNode;
    style?: ViewStyle;
}

export function Card({children, style}: CardProps) {
    const {colors, borderRadius, spacing} = useTheme();

    return (
        <View
            style={[
                {
                    backgroundColor: colors.card,
                    borderRadius: borderRadius.lg,
                    padding: spacing.md,
                    marginBottom: spacing.md,
                },
                style,
            ]}
        >
            {children}
        </View>
    );
}
