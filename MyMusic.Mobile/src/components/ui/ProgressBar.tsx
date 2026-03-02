import React from 'react';
import {StyleSheet, View, ViewStyle} from 'react-native';
import {borderRadius, colors} from '../../constants/theme';

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
                                color = colors.primary,
                                backgroundColor = colors.border,
                                style,
                            }: ProgressBarProps) {
    const clampedProgress = Math.min(Math.max(progress, 0), 1);

    return (
        <View style={[styles.container, {height, backgroundColor}, style]}>
            <View
                style={[
                    styles.fill,
                    {
                        width: `${clampedProgress * 100}%`,
                        backgroundColor: color,
                    },
                ]}
            />
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        borderRadius: borderRadius.full,
        overflow: 'hidden',
    },
    fill: {
        height: '100%',
        borderRadius: borderRadius.full,
    },
});