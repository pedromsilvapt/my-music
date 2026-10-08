import {Dropzone} from "@mantine/dropzone";
import {Group, Stack, Text, ThemeIcon} from "@mantine/core";
import {IconMusic} from "@tabler/icons-react";
import {useTranslation} from "react-i18next";
import {useImportDropzoneStore} from "../../stores/import-dropzone-store.ts";
import {ACCEPTED_AUDIO_FILES, isAudioFile} from "../../utils/audio-files.ts";

interface SongImportDropzoneProps {
    onFilesDropped: (files: File[]) => void;
    children: React.ReactNode;
}

export default function SongImportDropzone({onFilesDropped, children}: SongImportDropzoneProps) {
    const {t} = useTranslation(["songs", "common"]);
    // Switched off while a dialog takes the dropped files itself
    const suspended = useImportDropzoneStore(state => state.suspensions > 0);
    const handleDrop = (files: File[]) => {
        const audioFiles = files.filter(isAudioFile);
        if (audioFiles.length > 0) {
            onFilesDropped(audioFiles);
        }
    };

    return (
        <>
            {children}
            {/* <Portal> */}
                <Dropzone.FullScreen
                    active={!suspended}
                    onDrop={handleDrop}
                    activateOnDrag
                    accept={ACCEPTED_AUDIO_FILES}
                    // style={{
                    //     position: 'fixed',
                    //     inset: 0,
                    //     top: 0,
                    //     bottom: 0,
                    // }}
                    styles={{
                        root: {
                            backgroundColor: 'rgba(30, 30, 30, 0.95)',
                            height: '100vh',  // Add this
                            width: '100vw',   // Add this
                            position: 'fixed', // Ensure fixed positioning
                            inset: 0,
                            top: 0,
                            left: 0,
                            zIndex: 1000,
                            display: 'flex',
                            flexDirection: 'column',
                            justifyContent: 'center'
                        },
                    }}
                >
                    <Group justify="center" gap="xl" mih={220} style={{ pointerEvents: 'none' }}>
                        <ThemeIcon variant="outline" size={80} radius="xl" color="gray">
                            <IconMusic size={40}/>
                        </ThemeIcon>
                        <Stack gap="xs" align="center">
                            <Text size="xl" fw={700} c="white">
                                {t("songs:import.dropTitle")}
                            </Text>
                            <Text size="sm" c="dimmed">
                                {t("songs:import.dropHelp")}
                            </Text>
                        </Stack>
                    </Group>
                </Dropzone.FullScreen>
            {/* </Portal> */}
        </>
    );
}
