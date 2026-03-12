import {Dropzone} from "@mantine/dropzone";
import {Group, Portal, Stack, Text, ThemeIcon} from "@mantine/core";
import {IconMusic} from "@tabler/icons-react";

const ACCEPTED_AUDIO_TYPES = ['audio/mpeg', 'audio/mp4', 'audio/x-m4a', 'audio/m4a'];
const ACCEPTED_EXTENSIONS = ['.mp3', '.m4a'];

interface SongImportDropzoneProps {
    onFilesDropped: (files: File[]) => void;
    children: React.ReactNode;
}

function isAudioFile(file: File): boolean {
    if (ACCEPTED_AUDIO_TYPES.includes(file.type)) {
        return true;
    }
    const lowerName = file.name.toLowerCase();
    return ACCEPTED_EXTENSIONS.some(ext => lowerName.endsWith(ext));
}

export default function SongImportDropzone({onFilesDropped, children}: SongImportDropzoneProps) {
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
                    active={true}
                    onDrop={handleDrop}
                    activateOnDrag
                    accept={{
                        'audio/mpeg': ['.mp3'],
                        'audio/mp4': ['.m4a'],
                        'audio/x-m4a': ['.m4a'],
                    }}
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
                                Drop audio files here
                            </Text>
                            <Text size="sm" c="dimmed">
                                Supports MP3 and M4A files
                            </Text>
                        </Stack>
                    </Group>
                </Dropzone.FullScreen>
            {/* </Portal> */}
        </>
    );
}
