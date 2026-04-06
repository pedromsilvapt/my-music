import {ActionIcon, Box, Checkbox, Group, Stack, Text, TextInput} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {IconClipboard, IconDownload, IconMusic, IconUpload, IconWorld, IconX} from "@tabler/icons-react";
import {useCallback, useEffect, useRef, useState} from "react";
import {useArtworkLightbox} from "../../contexts/artwork-lightbox-context.tsx";
import {fetchArtworkAsDataUrl} from "../../utils/artwork.ts";
import {autocompleteSongs} from "../../client/songs.ts";
import AutocompleteField, {type AutocompleteItem} from "./autocomplete-field.tsx";
import type {ArtworkRef} from "../../model/artworkRef";

interface CoverDimensions {
    width: number;
    height: number;
}

interface CoverUploadFieldProps {
    label?: string;
    value: ArtworkRef | null;
    onChange: (value: ArtworkRef | null, dimensions: CoverDimensions | null) => void;
    currentDimensions?: CoverDimensions | null;
    disabled?: boolean;
    diffMode?: boolean;
    isChecked?: boolean;
    onCheckChange?: (checked: boolean) => void;
    oldCoverUrl?: string;
}

export default function CoverUploadField({
                                             label = "Cover Artwork",
                                             value,
                                             onChange,
                                             currentDimensions,
                                             disabled,
                                             diffMode,
                                             isChecked = true,
                                             onCheckChange,
                                             oldCoverUrl,
                                           }: CoverUploadFieldProps) {
    const [url, setUrl] = useState("");
    const [loading, setLoading] = useState(false);
    const [previewDimensions, setPreviewDimensions] = useState<CoverDimensions | null>(currentDimensions ?? null);
    const [oldCoverDimensions, setOldCoverDimensions] = useState<CoverDimensions | null>(null);
    const [newCoverDimensions, setNewCoverDimensions] = useState<CoverDimensions | null>(null);
    const [coverSourceMode, setCoverSourceMode] = useState<'url' | 'song'>('url');
    const [selectedSong, setSelectedSong] = useState<AutocompleteItem | null>(null);
    const {openLightbox} = useArtworkLightbox();
    const fileInputRef = useRef<HTMLInputElement>(null);

    const hasChanged = diffMode && value !== null && value?.base64 !== undefined;

    const loadImageDimensions = useCallback((src: string): Promise<CoverDimensions | null> => {
        return new Promise((resolve) => {
            const img = new Image();
            img.onload = () => resolve({width: img.width, height: img.height});
            img.onerror = () => resolve(null);
            img.src = src;
        });
    }, []);

    useEffect(() => {
        if (diffMode && oldCoverUrl) {
            loadImageDimensions(oldCoverUrl).then(setOldCoverDimensions);
        } else {
            setOldCoverDimensions(null);
        }
    }, [diffMode, oldCoverUrl, loadImageDimensions]);

    useEffect(() => {
        if (diffMode && value?.base64) {
            loadImageDimensions(value.base64).then(setNewCoverDimensions);
        } else if (diffMode && value?.id) {
            loadImageDimensions(`/api/artwork/${value.id}`).then(setNewCoverDimensions);
        } else {
            setNewCoverDimensions(null);
        }
    }, [diffMode, value, loadImageDimensions]);

    useEffect(() => {
        if (!diffMode && value?.id && !previewDimensions) {
            loadImageDimensions(`/api/artwork/${value.id}`).then(setPreviewDimensions);
        }
    }, [diffMode, value?.id, previewDimensions, loadImageDimensions]);

    const loadImageFromBase64 = useCallback((base64: string): Promise<CoverDimensions> => {
        return new Promise((resolve, reject) => {
            const img = new Image();
            img.onload = () => {
                resolve({width: img.width, height: img.height});
            };
            img.onerror = () => reject(new Error("Failed to load image"));
            img.src = base64;
        });
    }, []);

    const handleFileUpload = useCallback(async (file: File) => {
        if (!file.type.startsWith("image/")) {
            notifications.show({title: "Error", message: "Please select an image file", color: "red"});
            return;
        }

        setLoading(true);
        try {
            const reader = new FileReader();
            reader.onload = async (e) => {
                const base64 = e.target?.result as string;
                const dimensions = await loadImageFromBase64(base64);
                setPreviewDimensions(dimensions);
                onChange({ base64 }, dimensions);
            };
            reader.readAsDataURL(file);
        } catch {
            notifications.show({title: "Error", message: "Failed to load image", color: "red"});
        } finally {
            setLoading(false);
        }
    }, [loadImageFromBase64, onChange]);

    const handlePaste = useCallback(async () => {
        try {
            const clipboardItems = await navigator.clipboard.read();
            for (const item of clipboardItems) {
                const imageType = item.types.find(type => type.startsWith("image/"));
                if (imageType) {
                    const blob = await item.getType(imageType);
                    const file = new File([blob], "pasted-image.png", {type: imageType});
                    await handleFileUpload(file);
                    return;
                }
            }
            notifications.show({title: "Error", message: "No image found in clipboard", color: "red"});
        } catch {
            notifications.show({title: "Error", message: "Failed to paste from clipboard", color: "red"});
        }
    }, [handleFileUpload]);

    const handleUrlDownload = useCallback(async () => {
        if (!url) return;

        setLoading(true);
        try {
            const response = await fetch(url);
            if (!response.ok) throw new Error("Failed to download image");

            const blob = await response.blob();
            const file = new File([blob], "downloaded-image.png", {type: blob.type});
            await handleFileUpload(file);
            setUrl("");
        } catch {
            notifications.show({title: "Error", message: "Failed to download image from URL", color: "red"});
        } finally {
            setLoading(false);
        }
    }, [url, handleFileUpload]);

    const handleClear = useCallback(() => {
        onChange(null, null);
        setPreviewDimensions(null);
        setSelectedSong(null);
        setUrl("");
    }, [onChange]);

    const searchSongs = useCallback(async (query: string): Promise<AutocompleteItem[]> => {
        if (query.length < 1) return [];
        const response = await autocompleteSongs({ search: query, limit: 15 });
        return response.data.songs.map(song => ({
            id: song.id,
            name: song.title,
            subtitle: song.albumName ?? undefined,
            coverId: song.coverId,
            artistName: song.artistName ?? undefined,
        }));
    }, []);

    const handleSongSelect = useCallback(async (song: AutocompleteItem | string | null) => {
        if (song === null || typeof song === "string" || (typeof song === "object" && song.id < 0)) {
            setSelectedSong(null);
            return;
        }

        setSelectedSong(song);

        if (!song.coverId) {
            notifications.show({ title: "No Cover", message: "Selected song has no cover artwork", color: "yellow" });
            return;
        }

        setLoading(true);
        try {
            const { dimensions } = await fetchArtworkAsDataUrl(song.coverId);
            setPreviewDimensions(dimensions);
            onChange({ id: song.coverId }, dimensions);
        } catch {
            notifications.show({ title: "Error", message: "Failed to load cover from selected song", color: "red" });
        } finally {
            setLoading(false);
        }
    }, [onChange]);

    const handleOpenLightbox = useCallback((src: string) => {
        openLightbox(src);
    }, [openLightbox]);

    const previewSrc = value?.base64 || (value?.id ? `/api/artwork/${value.id}` : null);
    const showSideBySide = diffMode && oldCoverUrl;

    const oldBorderColor = isChecked ? 'var(--mantine-color-red-6)' : 'var(--mantine-color-gray-5)';
    const oldBgColor = isChecked ? 'var(--mantine-color-red-0)' : 'var(--mantine-color-gray-1)';
    const newBorderColor = isChecked ? 'var(--mantine-color-green-6)' : 'var(--mantine-color-gray-5)';
    const newBgColor = isChecked ? 'var(--mantine-color-green-0)' : 'var(--mantine-color-gray-1)';

    return (
        <Stack gap="xs">
            <Group gap="xs" align="center">
                {diffMode && onCheckChange && (
                    <Checkbox
                        checked={isChecked}
                        onChange={(e) => onCheckChange(e.currentTarget.checked)}
                    />
                )}
                <Text size="sm" fw={500}>{label}</Text>
            </Group>

            {showSideBySide ? (
                <Group align="flex-start" gap="md">
                    <Group gap="md">
                        <Stack gap="xs" align="center">
                            <Text size="xs" c={isChecked ? "red" : "dimmed"}>Old</Text>
                            <Box
                                style={{
                                    width: 120,
                                    height: 120,
                                    border: `1px solid ${oldBorderColor}`,
                                    borderRadius: "var(--mantine-radius-sm)",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    overflow: "hidden",
                                    backgroundColor: oldBgColor,
                                    cursor: "pointer",
                                }}
                                onClick={() => oldCoverUrl && handleOpenLightbox(oldCoverUrl)}
                            >
                                {oldCoverUrl ? (
                                    <img
                                        src={oldCoverUrl.startsWith("data:") ? oldCoverUrl : oldCoverUrl}
                                        alt="Old Cover"
                                        style={{maxWidth: "100%", maxHeight: "100%", objectFit: "contain"}}
                                    />
                                ) : (
                                    <IconMusic size={40} color="var(--mantine-color-gray-4)"/>
                                )}
                            </Box>
                            {oldCoverDimensions ? (
                                <Text size="xs" c="dimmed">
                                    {oldCoverDimensions.width} x {oldCoverDimensions.height}
                                </Text>
                            ) : (
                                <Text size="xs" c="dimmed">no size</Text>
                            )}
                        </Stack>

                        <Stack gap="xs" align="center" style={{justifyContent: "center"}}>
                            <Text size="xs" c="dimmed">→</Text>
                        </Stack>

                        <Stack gap="xs" align="center">
                            <Text size="xs" c={isChecked ? "green" : "dimmed"}>New</Text>
                            <Box
                                style={{
                                    width: 120,
                                    height: 120,
                                    border: `1px solid ${newBorderColor}`,
                                    borderRadius: "var(--mantine-radius-sm)",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    overflow: "hidden",
                                    backgroundColor: newBgColor,
                                    cursor: "pointer",
                                }}
                                onClick={() => previewSrc && handleOpenLightbox(previewSrc)}
                            >
                                <img
                                    src={previewSrc ?? ""}
                                    alt="New Cover"
                                    style={{maxWidth: "100%", maxHeight: "100%", objectFit: "contain"}}
                                />
                            </Box>
                            {newCoverDimensions ? (
                                <Text size="xs" c="dimmed">
                                    {newCoverDimensions.width} x {newCoverDimensions.height}
                                </Text>
                            ) : (
                                <Text size="xs" c="dimmed">no size</Text>
                            )}
                        </Stack>
                    </Group>

                    <Stack gap="xs" style={{minWidth: 200}}>
                        <Group gap="xs">
                            <input
                                ref={fileInputRef}
                                type="file"
                                accept="image/*"
                                style={{display: "none"}}
                                onChange={(e) => {
                                    const file = e.target.files?.[0];
                                    if (file) handleFileUpload(file);
                                }}
                                disabled={disabled || !isChecked}
                            />
                            <ActionIcon
                                variant="light"
                                size="lg"
                                onClick={() => fileInputRef.current?.click()}
                                disabled={disabled || !isChecked}
                                loading={loading}
                                title="Upload file"
                            >
                                <IconUpload/>
                            </ActionIcon>
                            <ActionIcon
                                variant="light"
                                size="lg"
                                onClick={handlePaste}
                                disabled={disabled || !isChecked}
                                title="Paste from clipboard"
                            >
                                <IconClipboard/>
                            </ActionIcon>
                            <ActionIcon
                                variant={coverSourceMode === 'url' ? 'filled' : 'light'}
                                size="lg"
                                onClick={() => setCoverSourceMode('url')}
                                disabled={disabled || !isChecked}
                                title="From URL"
                            >
                                <IconWorld/>
                            </ActionIcon>
                            <ActionIcon
                                variant={coverSourceMode === 'song' ? 'filled' : 'light'}
                                size="lg"
                                onClick={() => setCoverSourceMode('song')}
                                disabled={disabled || !isChecked}
                                title="From existing song"
                            >
                                <IconMusic/>
                            </ActionIcon>
                            {value && (
                                <ActionIcon
                                    variant="light"
                                    size="lg"
                                    color="red"
                                    onClick={handleClear}
                                    disabled={disabled || !isChecked}
                                    title="Remove cover"
                                >
                                    <IconX/>
                                </ActionIcon>
                            )}
                        </Group>

                        {coverSourceMode === 'url' ? (
                            <Group gap="xs" align="flex-end">
                                <TextInput
                                    placeholder="Paste image URL..."
                                    value={url}
                                    onChange={(e) => setUrl(e.target.value)}
                                    style={{flex: 1}}
                                    disabled={disabled || !isChecked}
                                />
                                <ActionIcon
                                    variant="light"
                                    size="lg"
                                    onClick={handleUrlDownload}
                                    disabled={!url || disabled || !isChecked}
                                    loading={loading}
                                    title="Download from URL"
                                >
                                    <IconDownload/>
                                </ActionIcon>
                            </Group>
                        ) : (
                            <AutocompleteField
                                placeholder="Search for a song to use its cover..."
                                value={selectedSong}
                                onChange={handleSongSelect}
                                onSearch={searchSongs}
                                disabled={disabled || !isChecked}
                                showArtwork={true}
                            />
                        )}
                    </Stack>
                </Group>
            ) : (
                <Group align="flex-start" gap="md">
                    <Stack gap="xs" align="center">
                        <Box
                            style={{
                                width: 150,
                                height: 150,
                                border: "1px solid var(--mantine-color-gray-3)",
                                borderRadius: "var(--mantine-radius-sm)",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                overflow: "hidden",
                                backgroundColor: hasChanged ? "var(--mantine-color-green-0)" : undefined,
                                borderColor: hasChanged ? "var(--mantine-color-green-6)" : undefined,
                                cursor: previewSrc ? "pointer" : undefined,
                            }}
                            onClick={() => previewSrc && handleOpenLightbox(previewSrc)}
                        >
                            {previewSrc ? (
                                <img
                                    src={previewSrc}
                                    alt="Cover"
                                    style={{maxWidth: "100%", maxHeight: "100%", objectFit: "contain"}}
                                />
                            ) : (
                                <IconMusic size={50} color="var(--mantine-color-gray-4)"/>
                            )}
                        </Box>
                        {previewDimensions ? (
                            <Text size="xs" c="dimmed">
                                {previewDimensions.width} x {previewDimensions.height}
                            </Text>
                        ) : (
                            <Text size="xs" c="dimmed">no size</Text>
                        )}
                    </Stack>

                    <Stack gap="xs" style={{flex: 1}}>
                        <Group gap="xs" style={{flex: 1}}>
                            <input
                                ref={fileInputRef}
                                type="file"
                                accept="image/*"
                                style={{display: "none"}}
                                onChange={(e) => {
                                    const file = e.target.files?.[0];
                                    if (file) handleFileUpload(file);
                                }}
                                disabled={disabled || (diffMode && !isChecked)}
                            />
                            <ActionIcon
                                variant="light"
                                size="lg"
                                onClick={() => fileInputRef.current?.click()}
                                disabled={disabled || (diffMode && !isChecked)}
                                loading={loading}
                                title="Upload file"
                            >
                                <IconUpload/>
                            </ActionIcon>
                            <ActionIcon
                                variant="light"
                                size="lg"
                                onClick={handlePaste}
                                disabled={disabled || (diffMode && !isChecked)}
                                title="Paste from clipboard"
                            >
                                <IconClipboard/>
                            </ActionIcon>
                            <ActionIcon
                                variant={coverSourceMode === 'url' ? 'filled' : 'light'}
                                size="lg"
                                onClick={() => setCoverSourceMode('url')}
                                disabled={disabled || (diffMode && !isChecked)}
                                title="From URL"
                            >
                                <IconWorld/>
                            </ActionIcon>
                            <ActionIcon
                                variant={coverSourceMode === 'song' ? 'filled' : 'light'}
                                size="lg"
                                onClick={() => setCoverSourceMode('song')}
                                disabled={disabled || (diffMode && !isChecked)}
                                title="From existing song"
                            >
                                <IconMusic/>
                            </ActionIcon>
                            {value && (
                                <ActionIcon
                                    variant="light"
                                    size="lg"
                                    color="red"
                                    onClick={handleClear}
                                    disabled={disabled || (diffMode && !isChecked)}
                                    title="Remove cover"
                                >
                                    <IconX/>
                                </ActionIcon>
                            )}
                        </Group>

                        {coverSourceMode === 'url' ? (
                            <Group gap="xs" align="flex-end">
                                <TextInput
                                    placeholder="Paste image URL..."
                                    value={url}
                                    onChange={(e) => setUrl(e.target.value)}
                                    style={{flex: 1}}
                                    disabled={disabled || (diffMode && !isChecked)}
                                />
                                <ActionIcon
                                    variant="light"
                                    size="lg"
                                    onClick={handleUrlDownload}
                                    disabled={!url || disabled || (diffMode && !isChecked)}
                                    loading={loading}
                                    title="Download from URL"
                                >
                                    <IconDownload/>
                                </ActionIcon>
                            </Group>
                        ) : (
                            <AutocompleteField
                                placeholder="Search for a song to use its cover..."
                                value={selectedSong}
                                onChange={handleSongSelect}
                                onSearch={searchSongs}
                                disabled={disabled || (diffMode && !isChecked)}
                                showArtwork={true}
                            />
                        )}
                    </Stack>
                </Group>
            )}
        </Stack>
    );
}
