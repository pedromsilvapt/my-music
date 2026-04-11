import React from 'react';
import {ScrollView, StyleSheet, Text, View} from 'react-native';
import {useTheme} from '../../hooks/useTheme';
import {Button} from './Button';

export interface ErrorDetails {
    title?: string;
    status?: number;
    message: string;
    url?: string;
    responseBody?: string;
    stack?: string;
    cause?: string;
    validationErrors?: Record<string, string[]>;
}

interface ErrorDisplayProps {
    error: ErrorDetails;
    onRetry?: () => void;
    onDismiss?: () => void;
}

export function ErrorDisplay({error, onRetry, onDismiss}: ErrorDisplayProps) {
    const {colors, borderRadius, fontFamily, fontSize, spacing} = useTheme();

    const formatValue = (value: string | undefined): string => {
        if (!value) return 'N/A';
        return value;
    };

    const formatJson = (value: string | undefined): string => {
        if (!value) return 'N/A';
        try {
            const parsed = JSON.parse(value);
            return JSON.stringify(parsed, null, 2);
        } catch {
            return value;
        }
    };

    const isSuccessStatus = error.status !== undefined && error.status >= 200 && error.status < 300;

    return (
        <View
            style={[
                styles.container,
                {
                    backgroundColor: colors.surfaceSecondary,
                    borderRadius: borderRadius.md,
                    marginHorizontal: spacing.md,
                },
            ]}
        >
            <ScrollView style={{padding: spacing.md}} showsVerticalScrollIndicator={true}>
                <View
                    style={[
                        styles.header,
                        {
                            borderBottomWidth: 1,
                            borderBottomColor: colors.borderSecondary,
                            paddingBottom: spacing.sm,
                            marginBottom: spacing.md,
                        },
                    ]}
                >
                    <Text
                        style={{
                            fontFamily: fontFamily.monospace,
                            fontSize: fontSize.lg,
                            fontWeight: '700',
                            color: colors.error,
                        }}
                    >
                        ERROR
                    </Text>
                </View>

                <View style={{marginBottom: spacing.md}}>
                    <Text
                        style={{
                            fontFamily: fontFamily.monospace,
                            fontSize: fontSize.xs,
                            color: colors.textSecondary,
                            textTransform: 'uppercase',
                            marginBottom: spacing.xs,
                        }}
                    >
                        Status
                    </Text>
                    <Text
                        style={[
                            {
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.sm,
                                color: colors.textSecondary,
                            },
                            isSuccessStatus ? {color: colors.success} : {color: colors.error},
                        ]}
                    >
                        {formatValue(error.status?.toString())}
                    </Text>
                </View>

                {error.title && (
                    <View style={{marginBottom: spacing.md}}>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.xs,
                                color: colors.textSecondary,
                                textTransform: 'uppercase',
                                marginBottom: spacing.xs,
                            }}
                        >
                            Title
                        </Text>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.sm,
                                color: colors.textSecondary,
                            }}
                        >
                            {error.title}
                        </Text>
                    </View>
                )}

                <View style={{marginBottom: spacing.md}}>
                    <Text
                        style={{
                            fontFamily: fontFamily.monospace,
                            fontSize: fontSize.xs,
                            color: colors.textSecondary,
                            textTransform: 'uppercase',
                            marginBottom: spacing.xs,
                        }}
                    >
                        Message
                    </Text>
                    <Text
                        style={{
                            fontFamily: fontFamily.monospace,
                            fontSize: fontSize.sm,
                            color: colors.textSecondary,
                        }}
                    >
                        {error.message}
                    </Text>
                </View>

                {error.url && (
                    <View style={{marginBottom: spacing.md}}>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.xs,
                                color: colors.textSecondary,
                                textTransform: 'uppercase',
                                marginBottom: spacing.xs,
                            }}
                        >
                            URL
                        </Text>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.sm,
                                color: colors.textSecondary,
                            }}
                        >
                            {error.url}
                        </Text>
                    </View>
                )}

                {error.validationErrors && Object.keys(error.validationErrors).length > 0 && (
                    <View style={{marginBottom: spacing.md}}>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.xs,
                                color: colors.textSecondary,
                                textTransform: 'uppercase',
                                marginBottom: spacing.xs,
                            }}
                        >
                            Validation Errors
                        </Text>
                        <View style={{
                            backgroundColor: colors.backgroundSecondary,
                            padding: spacing.sm,
                            borderRadius: borderRadius.sm,
                        }}>
                            {Object.entries(error.validationErrors).map(([field, messages]) => (
                                <View key={field} style={{marginBottom: spacing.xs}}>
                                    <Text style={{
                                        fontFamily: fontFamily.monospace,
                                        fontSize: fontSize.xs,
                                        color: colors.error,
                                        fontWeight: '600',
                                    }}>
                                        {field}:
                                    </Text>
                                    {messages.map((msg, idx) => (
                                        <Text key={idx} style={{
                                            fontFamily: fontFamily.monospace,
                                            fontSize: fontSize.xs,
                                            color: colors.textSecondary,
                                            marginLeft: spacing.sm,
                                        }}>
                                            {msg}
                                        </Text>
                                    ))}
                                </View>
                            ))}
                        </View>
                    </View>
                )}

                {error.responseBody && (
                    <View style={{marginBottom: spacing.md}}>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.xs,
                                color: colors.textSecondary,
                                textTransform: 'uppercase',
                                marginBottom: spacing.xs,
                            }}
                        >
                            Response Body
                        </Text>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.xs,
                                color: colors.textSecondary,
                                backgroundColor: colors.backgroundSecondary,
                                padding: spacing.sm,
                                borderRadius: borderRadius.sm,
                            }}
                        >
                            {formatJson(error.responseBody)}
                        </Text>
                    </View>
                )}

                {error.stack && (
                    <View style={{marginBottom: spacing.md}}>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.xs,
                                color: colors.textSecondary,
                                textTransform: 'uppercase',
                                marginBottom: spacing.xs,
                            }}
                        >
                            Stack Trace
                        </Text>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.xs,
                                color: colors.textSecondary,
                                backgroundColor: colors.backgroundSecondary,
                                padding: spacing.sm,
                                borderRadius: borderRadius.sm,
                            }}
                        >
                            {error.stack}
                        </Text>
                    </View>
                )}

                {error.cause && (
                    <View style={{marginBottom: spacing.md}}>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.xs,
                                color: colors.textSecondary,
                                textTransform: 'uppercase',
                                marginBottom: spacing.xs,
                            }}
                        >
                            Caused By
                        </Text>
                        <Text
                            style={{
                                fontFamily: fontFamily.monospace,
                                fontSize: fontSize.sm,
                                color: colors.textSecondary,
                            }}
                        >
                            {error.cause}
                        </Text>
                    </View>
                )}
            </ScrollView>

            <View
                style={[
                    styles.actions,
                    {
                        borderTopWidth: 1,
                        borderTopColor: colors.borderSecondary,
                        padding: spacing.md,
                    },
                ]}
            >
                {onRetry && (
                    <Button title="Retry" onPress={onRetry} variant="outline" size="small"/>
                )}
                {onDismiss && (
                    <Button title="OK" onPress={onDismiss} size="small"/>
                )}
            </View>
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        borderWidth: 0,
        overflow: 'hidden',
    },
    header: {
        borderBottomWidth: 1,
    },
    actions: {
        flexDirection: 'row',
        justifyContent: 'flex-end',
        gap: 8,
    },
});
