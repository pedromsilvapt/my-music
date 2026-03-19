import React, {useEffect, useRef} from 'react';
import {Animated, StyleSheet, View, ViewStyle} from 'react-native';
import {useTheme} from '../../hooks/useTheme';

interface ProgressBarProps {
    progress: number; // 0 to 1
    height?: number;
    color?: string;
    backgroundColor?: string;
    style?: ViewStyle;
    animated?: boolean;
}

export function ProgressBar({
    progress,
    height = 8,
    color,
    backgroundColor,
    style,
    animated = true,
}: ProgressBarProps) {
    const {colors, borderRadius} = useTheme();
    
    const fillColor = color ?? colors.primary;
    const bgColor = backgroundColor ?? colors.border;

    const clampedProgress = Math.min(Math.max(progress, 0), 1);
    
    // Animated value for smooth progress transitions
    const animatedProgress = useRef(new Animated.Value(clampedProgress)).current;
    
    useEffect(() => {
        if (animated) {
            Animated.timing(animatedProgress, {
                toValue: clampedProgress,
                duration: 300,
                useNativeDriver: false,
            }).start();
        } else {
            animatedProgress.setValue(clampedProgress);
        }
    }, [clampedProgress, animated, animatedProgress]);

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
            <Animated.View
                style={[
                    {
                        height: '100%',
                        backgroundColor: fillColor,
                        borderRadius: borderRadius.full,
                    },
                    {
                        width: animatedProgress.interpolate({
                            inputRange: [0, 1],
                            outputRange: ['0%', '100%'],
                        }),
                    },
                ]}
            />
        </View>
    );
}
