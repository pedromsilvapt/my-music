import React from 'react';
import {StyleSheet, View, ViewStyle} from 'react-native';
import {useTheme} from '../../hooks/useTheme';

interface ProgressBarProps {
    progress: number; // 0 to 1
    height?: number;
    color?: string;
    backgroundColor?: string;
    style?: ViewStyle;
}

export function ProgressBar({
    progress,
    height = 8,
    color,
    backgroundColor,
    style,
}: ProgressBarProps) {
    const {colors, borderRadius} = useTheme();
    
    const fillColor = color ?? colors.primary;
    const bgColor = backgroundColor ?? colors.border;

    const clampedProgress = Math.min(Math.max(progress, 0), 1);

    return (
        <View
            style={[
                {
                    height,
                    backgroundColor: bgColor,
                    borderRadius: borderRadius.full,
                    overflow: 'hidden',
                },
                style,
            ]}
        >
            <View
                style={[
                    {
                        height: '100%',
                        backgroundColor: fillColor,
                        borderRadius: borderRadius.full,
                        width: `${clampedProgress * 100}%`,
                    },
                ]}
            />
        </View>
    );
}
