import {Alert, Box, Group, Input, Stack, Switch, Text} from "@mantine/core";
import {useVirtualizer} from "@tanstack/react-virtual";
import {useEffect, useMemo, useRef, useState} from "react";
import {useTranslation} from "react-i18next";
import {useNamingTemplatePreview} from "../../hooks/use-naming-template-preview.ts";
import type {PreviewDeviceNamingTemplateSong} from "../../model";
import ScribanTemplateEditor from "../common/scriban-template-editor.tsx";
import styles from "./naming-template-field.module.css";

// The variables TemplateNamingStrategy gives to a template, with the translation key of their description
const VARIABLES = [
    {name: "title", key: "title"},
    {name: "album.name", key: "albumName"},
    {name: "album.artist.name", key: "albumArtistName"},
    {name: "artists[0].name", key: "firstArtistName"},
    {name: "artists_label", key: "artistsLabel"},
    {name: "simple_label", key: "simpleLabel"},
    {name: "full_label", key: "fullLabel"},
    {name: "genres", key: "genres"},
    {name: "track", key: "track"},
    {name: "year", key: "year"},
    {name: "duration", key: "duration"},
    {name: "explicit", key: "explicit"},
    {name: "extension", key: "extension"},
    {name: "original_folder", key: "originalFolder"},
    {name: "original_name", key: "originalName"},
    {name: "id", key: "id"},
] as const;

const PREVIEW_ROW_HEIGHT = 44;

export interface NamingTemplateFieldProps {
    /** The device whose songs are previewed. Without it (a device being created) the template is only validated. */
    deviceId?: number;
    /** The template; empty uses the server's default one. */
    value: string;
    onChange: (value: string) => void;
    /** Told whether the template can be saved: it has no errors, and that was checked for what is typed now. */
    onValidityChange?: (valid: boolean) => void;
    disabled?: boolean;
}

/**
 * Edits the naming template of a device, showing its errors and the file names it would give to the songs
 * of the device as it is typed.
 */
export default function NamingTemplateField({deviceId, value, onChange, onValidityChange, disabled}: NamingTemplateFieldProps) {
    const {t} = useTranslation(["devices"]);
    const [changedOnly, setChangedOnly] = useState(false);
    const {preview, isOutdated} = useNamingTemplatePreview(deviceId, value);

    const errors = preview?.errors;
    const valid = !isOutdated && (errors?.length ?? 0) === 0;

    useEffect(() => {
        onValidityChange?.(valid);
    }, [valid, onValidityChange]);

    const variables = useMemo(() => VARIABLES.map(variable => ({
        name: variable.name,
        description: t(`devices:namingTemplate.variables.${variable.key}`),
    })), [t]);

    const songs = useMemo(() => {
        const songs = preview?.songs ?? [];
        return changedOnly ? songs.filter(song => song.changed) : songs;
    }, [preview?.songs, changedOnly]);

    return (
        <Input.Wrapper
            label={t("devices:namingTemplate.label")}
            description={t("devices:namingTemplate.description")}
        >
            <Stack gap="xs" mt={4} data-testid="naming-template-field" data-loading={isOutdated ? "true" : "false"}>
                <Box style={{border: "1px solid var(--mantine-color-default-border)", borderRadius: "var(--mantine-radius-sm)", overflow: "hidden"}}>
                    <ScribanTemplateEditor
                        value={value}
                        onChange={onChange}
                        placeholder={preview?.defaultNamingTemplate}
                        errors={errors}
                        variables={variables}
                        readOnly={disabled}
                        testId="naming-template-editor"
                    />
                </Box>

                {errors && errors.length > 0 && (
                    <Alert color="red" p="xs" data-testid="naming-template-errors">
                        {errors.map((error, index) => (
                            <Text key={index} size="sm" data-testid="naming-template-error">
                                {t("devices:namingTemplate.error", {
                                    line: error.line,
                                    column: error.column,
                                    message: error.message,
                                })}
                            </Text>
                        ))}
                    </Alert>
                )}

                {deviceId !== undefined && preview && preview.errors.length === 0 && (
                    <>
                        <Group justify="space-between">
                            <Text
                                size="sm"
                                data-testid="naming-preview-summary"
                                data-renamed={preview.renamed}
                                data-total={preview.total}
                            >
                                {t("devices:namingTemplate.summary", {renamed: preview.renamed, count: preview.total})}
                            </Text>
                            <Switch
                                size="xs"
                                label={t("devices:namingTemplate.changedOnly")}
                                checked={changedOnly}
                                onChange={event => setChangedOnly(event.currentTarget.checked)}
                                data-testid="naming-preview-changed-only"
                            />
                        </Group>
                        <NamingPreviewList songs={songs}/>
                    </>
                )}
            </Stack>
        </Input.Wrapper>
    );
}

/** The previewed files, virtualized: a device can hold every song of the library. */
function NamingPreviewList({songs}: { songs: PreviewDeviceNamingTemplateSong[] }) {
    const {t} = useTranslation(["devices"]);
    const scrollRef = useRef<HTMLDivElement>(null);

    const virtualizer = useVirtualizer({
        count: songs.length,
        getScrollElement: () => scrollRef.current,
        estimateSize: () => PREVIEW_ROW_HEIGHT,
        overscan: 10,
    });

    if (songs.length === 0) {
        return <Text size="sm" c="dimmed" data-testid="naming-preview-empty">{t("devices:namingTemplate.empty")}</Text>;
    }

    return (
        <div ref={scrollRef} className={styles.list} data-testid="naming-preview-list" data-count={songs.length}>
            <div style={{height: virtualizer.getTotalSize(), position: "relative"}}>
                {virtualizer.getVirtualItems().map(item => {
                    const song = songs[item.index]!;

                    return (
                        <div
                            key={song.songDeviceId}
                            className={styles.row}
                            style={{top: item.start, height: item.size}}
                            data-testid="naming-preview-row"
                            data-changed={song.changed ? "true" : "false"}
                            title={song.title}
                        >
                            <span
                                className={`${styles.path} ${styles.currentPath}`}
                                data-changed={song.changed ? "true" : "false"}
                                data-testid="naming-preview-current"
                            >
                                {song.currentPath}
                            </span>
                            {song.changed && (
                                <span className={styles.path} data-testid="naming-preview-new">
                                    {song.newPath}
                                </span>
                            )}
                            {song.error && (
                                <Text span size="xs" c="red" className={styles.path} data-testid="naming-preview-error">
                                    {song.error}
                                </Text>
                            )}
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
