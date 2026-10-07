import {useRouter} from 'expo-router';
import React, {useState} from 'react';
import {ScrollView, StyleSheet, Switch, Text, View} from 'react-native';
import {Button, Card, Input} from '../../src/components/ui';
import {useTheme} from '../../src/hooks/useTheme';
import {
    ChunkSizeRangeForm,
    ChunkTuningFormErrors,
    parseChunkTuningForm,
    toChunkTuningForm,
} from '../../src/services/chunkTuningForm';
import {getChunkTuning, setChunkTuning} from '../../src/services/configService';
import {DEFAULT_CHUNK_TUNING} from '../../src/services/sync/adaptive-chunk-size';

/** The sizes of the chunked requests of a sync (see "Request Chunks" in docs/development/sync.md). */
export default function SyncPerformanceScreen() {
    const router = useRouter();
    const {colors, fontSize, fontWeight, spacing} = useTheme();
    const [form, setForm] = useState(() => toChunkTuningForm(getChunkTuning()));
    const [errors, setErrors] = useState<ChunkTuningFormErrors>({});

    const handleSave = async () => {
        const result = parseChunkTuningForm(form, getChunkTuning());
        if (result.errors) {
            setErrors(result.errors);
            return;
        }

        await setChunkTuning(result.tuning);
        router.back();
    };

    const handleRestoreDefaults = () => {
        setForm(toChunkTuningForm(DEFAULT_CHUNK_TUNING));
        setErrors({});
    };

    const renderRange = (
        key: 'check' | 'resolve',
        title: string,
        hint: string
    ) => {
        const range = form[key];
        const setRange = (values: Partial<ChunkSizeRangeForm>) => setForm({...form, [key]: {...range, ...values}});

        return (
            <Card>
                <Text style={[styles.sectionTitle, {fontSize: fontSize.lg, fontWeight: fontWeight.semibold, color: colors.cardText}]}>{title}</Text>
                <Text style={{fontSize: fontSize.sm, color: colors.cardTextMuted, marginTop: spacing.xs, marginBottom: spacing.md}}>{hint}</Text>
                <Input
                    label={form.adaptive ? 'Starting Size (files per request)' : 'Size (files per request)'}
                    value={range.size}
                    onChangeText={(size) => setRange({size})}
                    error={errors[`${key}Size`]}
                    keyboardType="number-pad"
                    variant="card"
                />
                {form.adaptive && (
                    <View style={[styles.rangeRow, {gap: spacing.md}]}>
                        <Input
                            label="Minimum"
                            value={range.min}
                            onChangeText={(min) => setRange({min})}
                            error={errors[`${key}Min`]}
                            keyboardType="number-pad"
                            variant="card"
                            containerStyle={styles.rangeInput}
                        />
                        <Input
                            label="Maximum"
                            value={range.max}
                            onChangeText={(max) => setRange({max})}
                            error={errors[`${key}Max`]}
                            keyboardType="number-pad"
                            variant="card"
                            containerStyle={styles.rangeInput}
                        />
                    </View>
                )}
            </Card>
        );
    };

    return (
        <ScrollView style={[styles.container, {backgroundColor: colors.backgroundSecondary}]} contentContainerStyle={{padding: spacing.md, paddingBottom: spacing.xxl}}>
            <Card>
                <View style={styles.toggleRow}>
                    <View style={[styles.toggleLabelContainer, {marginRight: spacing.md}]}>
                        <Text style={{fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText}}>Adjust Request Sizes Automatically</Text>
                        <Text style={{fontSize: fontSize.sm, color: colors.cardTextMuted, marginTop: spacing.xs}}>
                            Requests carry more files while the server answers quickly, and fewer when it slows down
                        </Text>
                    </View>
                    <Switch
                        value={form.adaptive}
                        onValueChange={(adaptive) => setForm({...form, adaptive})}
                        trackColor={{false: colors.cardBorder, true: colors.primary}}
                        thumbColor={colors.cardText}
                    />
                </View>
                {form.adaptive && (
                    <Input
                        label="Target Request Time (seconds)"
                        value={form.targetRequestSeconds}
                        onChangeText={(targetRequestSeconds) => setForm({...form, targetRequestSeconds})}
                        error={errors.targetRequestSeconds}
                        keyboardType="decimal-pad"
                        variant="card"
                        containerStyle={{marginTop: spacing.md, marginBottom: 0}}
                    />
                )}
            </Card>

            {renderRange('check', 'Check Requests', 'How many files the server compares with its library at a time')}
            {renderRange('resolve', 'Conflict Resolution Requests', 'How many changed files have their checksums compared at a time')}

            <Button title="Save" onPress={handleSave}/>
            <Button title="Restore Defaults" onPress={handleRestoreDefaults} variant="outline" style={{marginTop: spacing.sm}}/>
        </ScrollView>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
    },
    sectionTitle: {
        fontSize: 16,
    },
    toggleRow: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    toggleLabelContainer: {
        flex: 1,
    },
    rangeRow: {
        flexDirection: 'row',
    },
    rangeInput: {
        flex: 1,
    },
});
