import React from 'react';
import {ScrollView, StyleSheet, Text, View} from 'react-native';
import {borderRadius, colors, fontFamily, fontSize, spacing} from '../../constants/theme';
import {Button} from './Button';

export interface ErrorDetails {
    title?: string;
    status?: number;
    message: string;
    url?: string;
    responseBody?: string;
    stack?: string;
    cause?: string;
}

interface ErrorDisplayProps {
    error: ErrorDetails;
    onRetry?: () => void;
    onDismiss?: () => void;
}

export function ErrorDisplay({error, onRetry, onDismiss}: ErrorDisplayProps) {
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
        <View style={styles.container}>
            <ScrollView style={styles.scrollView} showsVerticalScrollIndicator={true}>
                <View style={styles.header}>
                    <Text style={styles.headerText}>ERROR</Text>
                </View>

                <View style={styles.section}>
                    <Text style={styles.label}>Status</Text>
                    <Text style={[styles.value, isSuccessStatus ? styles.successValue : styles.errorValue]}>
                        {formatValue(error.status?.toString())}
                    </Text>
                </View>

                {error.title && (
                    <View style={styles.section}>
                        <Text style={styles.label}>Title</Text>
                        <Text style={styles.value}>{error.title}</Text>
                    </View>
                )}

                <View style={styles.section}>
                    <Text style={styles.label}>Message</Text>
                    <Text style={styles.value}>{error.message}</Text>
                </View>

                {error.url && (
                    <View style={styles.section}>
                        <Text style={styles.label}>URL</Text>
                        <Text style={styles.value}>{error.url}</Text>
                    </View>
                )}

                {error.responseBody && (
                    <View style={styles.section}>
                        <Text style={styles.label}>Response Body</Text>
                        <Text style={styles.codeBlock}>{formatJson(error.responseBody)}</Text>
                    </View>
                )}

                {error.stack && (
                    <View style={styles.section}>
                        <Text style={styles.label}>Stack Trace</Text>
                        <Text style={styles.codeBlock}>{error.stack}</Text>
                    </View>
                )}

                {error.cause && (
                    <View style={styles.section}>
                        <Text style={styles.label}>Caused By</Text>
                        <Text style={styles.value}>{error.cause}</Text>
                    </View>
                )}
            </ScrollView>

            <View style={styles.actions}>
                {onRetry && (
                    <Button title="Retry" onPress={onRetry} variant="outline" size="small" />
                )}
                {onDismiss && (
                    <Button title="OK" onPress={onDismiss} size="small" />
                )}
            </View>
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        backgroundColor: colors.surfaceDark,
        borderRadius: borderRadius.md,
        borderWidth: 0,
        overflow: 'hidden',
        marginHorizontal: spacing.md,
    },
    scrollView: {
        padding: spacing.md,
    },
    header: {
        borderBottomWidth: 1,
        borderBottomColor: colors.borderDark,
        paddingBottom: spacing.sm,
        marginBottom: spacing.md,
    },
    headerText: {
        fontFamily: fontFamily.monospace,
        fontSize: fontSize.lg,
        fontWeight: '700' as const,
        color: colors.error,
    },
    section: {
        marginBottom: spacing.md,
    },
    label: {
        fontFamily: fontFamily.monospace,
        fontSize: fontSize.xs,
        color: colors.textSecondary,
        textTransform: 'uppercase',
        marginBottom: spacing.xs,
    },
    value: {
        fontFamily: fontFamily.monospace,
        fontSize: fontSize.sm,
        color: colors.textSecondary,
    },
    codeBlock: {
        fontFamily: fontFamily.monospace,
        fontSize: fontSize.xs,
        color: colors.textSecondary,
        backgroundColor: colors.backgroundDark,
        padding: spacing.sm,
        borderRadius: borderRadius.sm,
    },
    errorValue: {
        color: colors.error,
    },
    successValue: {
        color: colors.success,
    },
    actions: {
        flexDirection: 'row',
        justifyContent: 'flex-end',
        gap: spacing.sm,
        padding: spacing.md,
        borderTopWidth: 1,
        borderTopColor: colors.borderDark,
    },
});
