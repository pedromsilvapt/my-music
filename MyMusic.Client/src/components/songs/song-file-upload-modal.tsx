import {Alert, Button, Group, Modal, Stack, Text, ThemeIcon} from "@mantine/core";
import {Dropzone} from "@mantine/dropzone";
import {IconAlertTriangle, IconMusic} from "@tabler/icons-react";
import {useEffect, useState} from "react";
import {useTranslation} from "react-i18next";
import {ZINDEX_DRAWER} from "../../consts.ts";
import {useReplaceSongFileWithNotifications} from "../../hooks/use-replace-song-file.ts";
import {useImportDropzoneStore} from "../../stores/import-dropzone-store.ts";
import {ACCEPTED_AUDIO_FILES, isAudioFile} from "../../utils/audio-files.ts";

interface SongFileUploadModalProps {
    opened: boolean;
    onClose: () => void;
    songId: number;
}

/**
 * Lets the user replace the audio of a song with a file of their own, dropped or picked. Nothing is uploaded until
 * the replacement is confirmed.
 */
export default function SongFileUploadModal({opened, onClose, songId}: SongFileUploadModalProps) {
    const {t} = useTranslation(["songs", "common"]);
    const {replace, isPending} = useReplaceSongFileWithNotifications();
    const [file, setFile] = useState<File | null>(null);
    const suspendImportDropzone = useImportDropzoneStore(state => state.suspend);
    const resumeImportDropzone = useImportDropzoneStore(state => state.resume);

    // A file dropped here replaces this song's audio: it must not be imported as a new song as well
    useEffect(() => {
        if (!opened) return;

        suspendImportDropzone();

        return resumeImportDropzone;
    }, [opened, suspendImportDropzone, resumeImportDropzone]);

    const handleClose = () => {
        setFile(null);
        onClose();
    };

    const handleDrop = (files: File[]) => {
        const audioFile = files.find(isAudioFile);
        if (audioFile) {
            setFile(audioFile);
        }
    };

    const handleReplace = () => {
        if (file == null) return;

        replace(songId, file, handleClose);
    };

    return (
        <Modal
            opened={opened}
            onClose={handleClose}
            title={t("songs:tools.uploadSong.title")}
            centered
            zIndex={ZINDEX_DRAWER}
        >
            <Stack gap="md" data-testid="song-file-upload-modal">
                <Dropzone
                    onDrop={handleDrop}
                    accept={ACCEPTED_AUDIO_FILES}
                    multiple={false}
                    disabled={isPending}
                    inputProps={{"data-testid": "song-file-upload-input"} as React.ComponentProps<"input">}
                    styles={{
                        root: {
                            border: "1px dashed var(--mantine-color-default-border)",
                            borderRadius: "var(--mantine-radius-md)",
                            padding: "var(--mantine-spacing-lg)",
                            cursor: isPending ? "default" : "pointer",
                        },
                    }}
                >
                    <Group justify="center" gap="md" wrap="nowrap" style={{pointerEvents: "none"}}>
                        <ThemeIcon variant="light" size={48} radius="xl" color="gray">
                            <IconMusic size={24}/>
                        </ThemeIcon>
                        <Stack gap={2} style={{minWidth: 0}}>
                            <Text fw={600} truncate data-testid="song-file-upload-selected">
                                {file ? file.name : t("songs:tools.uploadSong.dropTitle")}
                            </Text>
                            <Text size="sm" c="dimmed">
                                {file ? t("songs:tools.uploadSong.dropAnother") : t("songs:import.dropHelp")}
                            </Text>
                        </Stack>
                    </Group>
                </Dropzone>
                <Alert color="yellow" icon={<IconAlertTriangle size={18}/>}>
                    {t("songs:tools.uploadSong.warning")}
                </Alert>
                <Group justify="flex-end" gap="xs">
                    <Button variant="subtle" onClick={handleClose} disabled={isPending}>
                        {t("common:actions.cancel")}
                    </Button>
                    <Button onClick={handleReplace} disabled={file == null} loading={isPending}>
                        {t("songs:tools.uploadSong.replace")}
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
