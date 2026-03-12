import {useState, useEffect, useRef, useCallback} from "react";
import {Modal, Stack, Text, Progress, Group, ThemeIcon, Button, ScrollArea, Box} from "@mantine/core";
import {IconCheck, IconX, IconLoader, IconFileMusic} from "@tabler/icons-react";
import {useUploadSong} from "../../client/songs";
import {ZINDEX_MODAL} from "../../consts";

export interface SongImportFile {
    id: string;
    file: File;
    status: 'pending' | 'uploading' | 'success' | 'error';
    error?: string;
    progress: number;
}

interface SongImportProgressProps {
    opened: boolean;
    onClose: () => void;
    files: File[];
}

export default function SongImportProgress({opened, onClose, files}: SongImportProgressProps) {
    const [importFiles, setImportFiles] = useState<SongImportFile[]>([]);
    const [currentIndex, setCurrentIndex] = useState(0);
    const [isCanceled, setIsCanceled] = useState(false);
    const abortRef = useRef(false);
    const isUploadingRef = useRef(false);
    const filesRef = useRef(files);

    const uploadMutation = useUploadSong();

    useEffect(() => {
        filesRef.current = files;
    }, [files]);

    useEffect(() => {
        if (opened && files.length > 0) {
            setImportFiles(files.map((file, index) => ({
                id: `file-${index}-${Date.now()}`,
                file,
                status: 'pending',
                progress: 0,
            })));
            setCurrentIndex(0);
            setIsCanceled(false);
            abortRef.current = false;
        }
    }, [opened, files]);

    const uploadNextFile = useCallback(async (index: number) => {
        if (abortRef.current) {
            return;
        }

        const currentFiles = filesRef.current;
        if (index >= currentFiles.length) {
            return;
        }

        const fileData: SongImportFile = {
            id: `file-${index}`,
            file: currentFiles[index],
            status: 'pending',
            progress: 0,
        };
        
        setImportFiles(prev => prev.map((f, i) => 
            i === index ? {...f, status: 'uploading', progress: 50} : f
        ));

        try {
            const modifiedAt = new Date(fileData.file.lastModified).toISOString();
            const createdAt = new Date(fileData.file.lastModified).toISOString();

            const result = await uploadMutation.mutateAsync({
                data: {
                    file: fileData.file,
                    path: fileData.file.name,
                    modifiedAt,
                    createdAt,
                },
            });

            if (abortRef.current) {
                return;
            }

            const responseData = result.data as {success?: boolean; songId?: number; error?: string};
            const success = responseData?.success ?? false;
            
            setImportFiles(prev => prev.map((f, i) => 
                i === index ? {
                    ...f, 
                    status: success ? 'success' : 'error',
                    error: success ? undefined : responseData?.error,
                    progress: 100
                } : f
            ));
        } catch (error) {
            if (abortRef.current) {
                return;
            }

            setImportFiles(prev => prev.map((f, i) => 
                i === index ? {
                    ...f,
                    status: 'error',
                    error: error instanceof Error ? error.message : 'Upload failed',
                    progress: 0
                } : f
            ));
        }
    }, [uploadMutation]);

    useEffect(() => {
        if (!opened || isCanceled || isUploadingRef.current) {
            return;
        }

        if (currentIndex < importFiles.length) {
            isUploadingRef.current = true;
            uploadNextFile(currentIndex).finally(() => {
                isUploadingRef.current = false;
                if (!abortRef.current) {
                    setCurrentIndex(prev => prev + 1);
                }
            });
        }
    }, [currentIndex, opened, isCanceled, importFiles.length, uploadNextFile]);

    const handleCancel = () => {
        abortRef.current = true;
        setIsCanceled(true);
        onClose();
    };

    const completedCount = importFiles.filter(f => f.status === 'success').length;
    const errorCount = importFiles.filter(f => f.status === 'error').length;
    const inProgressCount = importFiles.filter(f => f.status === 'uploading').length;
    const isComplete = currentIndex >= importFiles.length && inProgressCount === 0;

    const overallProgress = importFiles.length > 0
        ? ((completedCount + errorCount) / importFiles.length) * 100
        : 0;

    return (
        <Modal
            opened={opened}
            onClose={isComplete ? onClose : handleCancel}
            title={isComplete ? "Import Complete" : "Importing Songs"}
            centered
            zIndex={ZINDEX_MODAL}
            size="lg"
            closeOnClickOutside={false}
            closeOnEscape={!isComplete}
        >
            <Stack gap="md">
                <Group justify="space-between">
                    <Text size="sm" c="dimmed">
                        {completedCount} of {importFiles.length} completed
                        {errorCount > 0 && ` (${errorCount} failed)`}
                    </Text>
                    <Text size="sm" fw={500}>
                        {Math.round(overallProgress)}%
                    </Text>
                </Group>

                <Progress value={overallProgress} size="sm" animated={inProgressCount > 0} />

                <ScrollArea h={300}>
                    <Stack gap="xs">
                        {importFiles.map((file) => (
                            <FileItem key={file.id} file={file} />
                        ))}
                    </Stack>
                </ScrollArea>

                <Group justify="flex-end" gap="sm">
                    {isComplete ? (
                        <Button onClick={onClose}>Done</Button>
                    ) : (
                        <Button variant="light" color="red" onClick={handleCancel}>
                            Cancel
                        </Button>
                    )}
                </Group>
            </Stack>
        </Modal>
    );
}

function FileItem({file}: {file: SongImportFile}) {
    const icon = {
        pending: <ThemeIcon variant="light" color="gray" size="sm"><IconFileMusic size={14}/></ThemeIcon>,
        uploading: <ThemeIcon variant="light" color="yellow" size="sm"><IconLoader size={14} className="spin"/></ThemeIcon>,
        success: <ThemeIcon variant="light" color="green" size="sm"><IconCheck size={14}/></ThemeIcon>,
        error: <ThemeIcon variant="light" color="red" size="sm"><IconX size={14}/></ThemeIcon>,
    }[file.status];

    const color = {
        pending: 'gray',
        uploading: 'yellow',
        success: 'green',
        error: 'red',
    }[file.status];

    return (
        <Box
            p="xs"
            style={{
                border: '1px solid var(--mantine-color-default-border)',
                borderRadius: 'var(--mantine-radius-sm)',
                backgroundColor: color === 'gray' 
                    ? 'var(--mantine-color-gray-light)' 
                    : `var(--mantine-color-${color}-light)`,
            }}
        >
            <Group justify="space-between" gap="sm">
                <Group gap="sm">
                    {icon}
                    <Box style={{flex: 1, minWidth: 0}}>
                        <Text size="sm" fw={500} truncate>
                            {file.file.name}
                        </Text>
                        {file.error && (
                            <Text size="xs" c="red" truncate>
                                {file.error}
                            </Text>
                        )}
                    </Box>
                </Group>
                {file.status === 'uploading' && (
                    <Progress size="xs" w={60} value={file.progress} animated />
                )}
            </Group>
        </Box>
    );
}