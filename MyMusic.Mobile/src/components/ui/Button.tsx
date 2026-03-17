import React from 'react';
import {ActivityIndicator, StyleSheet, Text, TouchableOpacity, ViewStyle} from 'react-native';
import {useTheme} from '../../hooks/useTheme';

interface ButtonProps {
    title: string;
    onPress: () => void;
    variant?: 'primary' | 'secondary' | 'outline' | 'danger';
    size?: 'small' | 'medium' | 'large';
    disabled?: boolean;
    loading?: boolean;
    style?: ViewStyle;
}

export function Button({
    title,
    onPress,
    variant = 'primary',
    size = 'medium',
    disabled = false,
    loading = false,
    style,
}: ButtonProps) {
    const {colors, borderRadius, fontSize, fontWeight, spacing} = useTheme();

    const getButtonStyles = (): ViewStyle => {
        const base: ViewStyle = {
            alignItems: 'center',
            justifyContent: 'center',
            borderRadius: borderRadius.md,
        };

        const variants: Record<string, ViewStyle> = {
            primary: {backgroundColor: colors.primary},
            secondary: {backgroundColor: '#ffffff', borderWidth: 1, borderColor: colors.border},
            outline: {backgroundColor: 'transparent', borderWidth: 1, borderColor: colors.primary},
            danger: {backgroundColor: colors.error},
        };

        const sizes: Record<string, ViewStyle> = {
            small: {paddingVertical: spacing.xs, paddingHorizontal: spacing.sm},
            medium: {paddingVertical: spacing.sm, paddingHorizontal: spacing.md},
            large: {paddingVertical: spacing.md, paddingHorizontal: spacing.lg},
        };

        return {
            ...base,
            ...variants[variant],
            ...sizes[size],
            ...(disabled ? {opacity: 0.5} : {}),
        };
    };

    const getTextStyles = () => {
        const base = {
            fontWeight: fontWeight.semibold,
        };

        const variants: Record<string, {color: string}> = {
            primary: {color: colors.onPrimary},
            secondary: {color: '#1a1a2e'},
            outline: {color: colors.primary},
            danger: {color: '#ffffff'},
        };

        const sizes: Record<string, {fontSize: number}> = {
            small: {fontSize: fontSize.sm},
            medium: {fontSize: fontSize.md},
            large: {fontSize: fontSize.lg},
        };

        return {
            ...base,
            ...variants[variant],
            ...sizes[size],
            ...(disabled ? {opacity: 0.7} : {}),
        };
    };

    const indicatorColor = variant === 'outline' ? colors.primary : colors.onPrimary;

    return (
        <TouchableOpacity
            style={[getButtonStyles(), style]}
            onPress={onPress}
            disabled={disabled || loading}
            activeOpacity={0.7}
        >
            {loading ? (
                <ActivityIndicator color={indicatorColor}/>
            ) : (
                <Text style={getTextStyles()}>{title}</Text>
            )}
        </TouchableOpacity>
    );
}
