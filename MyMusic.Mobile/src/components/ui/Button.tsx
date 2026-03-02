import React from 'react';
import {ActivityIndicator, StyleSheet, Text, TouchableOpacity, ViewStyle} from 'react-native';
import {borderRadius, colors, fontSize, fontWeight, spacing} from '../../constants/theme';

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
    const buttonStyles = [
        styles.base,
        styles[variant],
        styles[`size_${size}`],
        disabled && styles.disabled,
        style,
    ];

    const textStyles = [
        styles.text,
        styles[`text_${variant}`],
        styles[`text_${size}`],
        disabled && styles.textDisabled,
    ];

    return (
        <TouchableOpacity
            style={buttonStyles}
            onPress={onPress}
            disabled={disabled || loading}
            activeOpacity={0.7}
        >
            {loading ? (
                <ActivityIndicator color={variant === 'outline' ? colors.primary : '#fff'}/>
            ) : (
                <Text style={textStyles}>{title}</Text>
            )}
        </TouchableOpacity>
    );
}

const styles = StyleSheet.create({
    base: {
        alignItems: 'center',
        justifyContent: 'center',
        borderRadius: borderRadius.md,
    },
    primary: {
        backgroundColor: colors.primary,
    },
    secondary: {
        backgroundColor: colors.surface,
    },
    outline: {
        backgroundColor: 'transparent',
        borderWidth: 1,
        borderColor: colors.primary,
    },
    danger: {
        backgroundColor: colors.error,
    },
    size_small: {
        paddingVertical: spacing.xs,
        paddingHorizontal: spacing.sm,
    },
    size_medium: {
        paddingVertical: spacing.sm,
        paddingHorizontal: spacing.md,
    },
    size_large: {
        paddingVertical: spacing.md,
        paddingHorizontal: spacing.lg,
    },
    disabled: {
        opacity: 0.5,
    },
    text: {
        fontWeight: fontWeight.semibold,
    },
    text_primary: {
        color: '#fff',
    },
    text_secondary: {
        color: colors.text,
    },
    text_outline: {
        color: colors.primary,
    },
    text_danger: {
        color: '#fff',
    },
    text_small: {
        fontSize: fontSize.sm,
    },
    text_medium: {
        fontSize: fontSize.md,
    },
    text_large: {
        fontSize: fontSize.lg,
    },
    textDisabled: {
        opacity: 0.7,
    },
});