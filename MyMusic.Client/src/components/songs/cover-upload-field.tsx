import {ActionIcon, Box, Checkbox, Group, Stack, Text, TextInput} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {IconClipboard, IconDownload, IconMusic, IconUpload, IconX} from "@tabler/icons-react";
import {useCallback, useRef, useState} from "react";

interface CoverDimensions {
    width: number;
    height: number;
}

interface CoverUploadFieldProps {
    label?: string;
    value: string | null;
    onChange: (value: string | null, dimensions: CoverDimensions | null) => void;
    currentCoverId?: number | null;
    currentDimensions?: CoverDimensions | null;
    disabled?: boolean;
    diffMode?: boolean;
    isChecked?: boolean;
    onCheckChange?: (checked: boolean) => void;
}

export default function CoverUploadField({
                                             label = "Cover Artwork",
                                             value,
                                             onChange,
                                             currentCoverId,
                                             currentDimensions,
                                             disabled,
                                             diffMode,
                                             isChecked = true,
                                             onCheckChange,
                                         }: CoverUploadFieldProps) {
    const [url, setUrl] = useState("");
    const [loading, setLoading] = useState(false);
    const [previewDimensions, setPreviewDimensions] = useState<CoverDimensions | null>(currentDimensions ?? null);
    const fileInputRef = useRef<HTMLInputElement>(null);

    const hasChanged = diffMode && value !== null;

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
                onChange(base64, dimensions);
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
    }, [onChange]);

    const previewSrc = value || (currentCoverId ? `/api/artwork/${currentCoverId}` : null);

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

            <Group align="flex-start" gap="md">
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
                    }}
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

                <Stack gap="xs" style={{flex: 1}}>
                    {previewDimensions && (
                        <Text size="sm" c="dimmed">
                            {previewDimensions.width} x {previewDimensions.height} px
                        </Text>
                    )}

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
                        {(value || currentCoverId) && (
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
                </Stack>
            </Group>
        </Stack>
    );
}
