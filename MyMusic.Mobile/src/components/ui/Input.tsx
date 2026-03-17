import React from 'react';
import {StyleSheet, Text, TextInput, TextInputProps, View, ViewStyle} from 'react-native';
import {useTheme} from '../../hooks/useTheme';

interface InputProps extends TextInputProps {
    label?: string;
    error?: string;
    containerStyle?: ViewStyle;
    variant?: 'default' | 'card';
}

export function Input({label, error, containerStyle, style, variant = 'default', ...props}: InputProps) {
    const {colors, borderRadius, fontSize, fontWeight, spacing} = useTheme();

    const isCard = variant === 'card';
    const labelColor = isCard ? colors.cardTextSecondary : colors.textSecondary;
    const inputColor = isCard ? colors.cardText : colors.text;
    const inputBgColor = isCard ? colors.cardSecondary : colors.surface;
    const borderColor = isCard ? colors.cardBorder : colors.border;
    const placeholderColor = isCard ? colors.cardTextMuted : colors.textMuted;

    return (
        <View style={[{marginBottom: spacing.md}, containerStyle]}>
            {label && (
                <Text
                    style={{
                        fontSize: fontSize.sm,
                        fontWeight: fontWeight.medium,
                        color: labelColor,
                        marginBottom: spacing.xs,
                    }}
                >
                    {label}
                </Text>
            )}
            <TextInput
                style={[
                    {
                        backgroundColor: inputBgColor,
                        borderRadius: borderRadius.md,
                        paddingVertical: spacing.sm,
                        paddingHorizontal: spacing.md,
                        fontSize: fontSize.md,
                        color: inputColor,
                        borderWidth: 1,
                        borderColor: error ? colors.error : borderColor,
                    },
                    style,
                ]}
                placeholderTextColor={placeholderColor}
                {...props}
            />
            {error && (
                <Text
                    style={{
                        fontSize: fontSize.sm,
                        color: colors.error,
                        marginTop: spacing.xs,
                    }}
                >
                    {error}
                </Text>
            )}
        </View>
    );
}
