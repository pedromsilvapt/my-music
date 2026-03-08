import {
    ActionIcon,
    Alert,
    Button,
    Center,
    Group,
    Modal,
    ScrollArea,
    Stack,
    Switch,
    Text,
    TextInput,
    UnstyledButton,
    useComputedColorScheme
} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {
    useCreateSource,
    useDeleteSource,
    getListSourcesQueryKey,
    useUpdateSource
} from "../../client/sources.ts";
import {ZINDEX_MODAL} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {CreateSourceData, ListSourcesItem} from "../../model";
import TablerIcon from "../common/tabler-icon.tsx";
import {IconAlertTriangle, IconPlus, IconTrash} from "@tabler/icons-react";
import {useEffect, useState} from "react";
import {useListSources} from "../../client/sources.ts";

interface ManageSourcesDialogProps {
    opened: boolean;
    onClose: () => void;
}

interface SourceFormData {
    name: string;
    icon: string;
    address: string;
    isPaid: boolean;
}

function createEmptyFormData(): SourceFormData {
    return {
        name: '',
        icon: '',
        address: '',
        isPaid: false
    };
}

function sourceToFormData(source: ListSourcesItem): SourceFormData {
    return {
        name: source.name,
        icon: source.icon,
        address: source.address,
        isPaid: source.isPaid
    };
}

function hasChanges(edited: SourceFormData | null, original: SourceFormData | null): boolean {
    if (!edited || !original) return false;
    return JSON.stringify(edited) !== JSON.stringify(original);
}

export default function ManageSourcesDialog({opened, onClose}: ManageSourcesDialogProps) {
    const colorScheme = useComputedColorScheme('light');
    const sourcesQuery = useListSources({query: {enabled: opened}});
    const sourcesResponse = useQueryData(sourcesQuery, "Failed to fetch sources") ?? {data: {sources: []}};
    const sources = sourcesResponse?.data?.sources ?? [];

    const queryClient = useQueryClient();

    const [selectedId, setSelectedId] = useState<number | 'new' | null>(null);
    const [editedData, setEditedData] = useState<SourceFormData | null>(null);
    const [originalData, setOriginalData] = useState<SourceFormData | null>(null);
    const [pendingSelection, setPendingSelection] = useState<number | 'new' | null>(null);
    const [showConfirmDiscard, setShowConfirmDiscard] = useState(false);

    useEffect(() => {
        if (opened && sources.length > 0 && selectedId === null) {
            const source = sources[0]!;
            setSelectedId(source.id);
            const data = sourceToFormData(source);
            setEditedData(data);
            setOriginalData(data);
        }
    }, [opened, sources, selectedId]);

    const createSource = useCreateSource({
        mutation: {
            onSuccess: (response) => {
                queryClient.invalidateQueries({queryKey: getListSourcesQueryKey()});
                const newSource = response.data.source;
                if (newSource) {
                    setSelectedId(newSource.id);
                    setEditedData(sourceToFormData(newSource));
                    setOriginalData(sourceToFormData(newSource));
                }
                notifications.show({
                    title: 'Success',
                    message: 'Source created successfully',
                    color: 'green'
                });
            },
            onError: (error) => {
                notifications.show({
                    title: 'Error',
                    message: 'Failed to create source',
                    color: 'red'
                });
                console.error('Failed to create source:', error);
            }
        }
    });

    const updateSource = useUpdateSource({
        mutation: {
            onSuccess: () => {
                queryClient.invalidateQueries({queryKey: getListSourcesQueryKey()});
                if (editedData) {
                    setOriginalData({...editedData});
                }
                notifications.show({
                    title: 'Success',
                    message: 'Source updated successfully',
                    color: 'green'
                });
            },
            onError: (error) => {
                notifications.show({
                    title: 'Error',
                    message: 'Failed to update source',
                    color: 'red'
                });
                console.error('Failed to update source:', error);
            }
        }
    });

    const deleteSource = useDeleteSource({
        mutation: {
            onSuccess: () => {
                queryClient.invalidateQueries({queryKey: getListSourcesQueryKey()});
                notifications.show({
                    title: 'Success',
                    message: 'Source deleted successfully',
                    color: 'green'
                });
                setSelectedId(null);
                setEditedData(null);
                setOriginalData(null);
            },
            onError: (error) => {
                notifications.show({
                    title: 'Error',
                    message: 'Failed to delete source',
                    color: 'red'
                });
                console.error('Failed to delete source:', error);
            }
        }
    });

    const selectSource = (id: number | 'new') => {
        setSelectedId(id);
        if (id === 'new') {
            const empty = createEmptyFormData();
            setEditedData(empty);
            setOriginalData(empty);
        } else {
            const source = sources.find(s => s.id === id);
            if (source) {
                const data = sourceToFormData(source);
                setEditedData(data);
                setOriginalData(data);
            }
        }
    };

    const handleSourceClick = (id: number | 'new') => {
        if (hasChanges(editedData, originalData)) {
            setPendingSelection(id);
            setShowConfirmDiscard(true);
        } else {
            selectSource(id);
        }
    };

    const handleConfirmDiscard = () => {
        setShowConfirmDiscard(false);
        if (pendingSelection !== null) {
            selectSource(pendingSelection);
            setPendingSelection(null);
        }
    };

    const handleCancelDiscard = () => {
        setShowConfirmDiscard(false);
        setPendingSelection(null);
    };

    const handleFieldChange = <K extends keyof SourceFormData>(field: K, value: SourceFormData[K]) => {
        if (editedData) {
            setEditedData({...editedData, [field]: value});
        }
    };

    const handleSave = () => {
        if (!editedData) return;

        const sourceData: CreateSourceData = {
            name: editedData.name,
            icon: editedData.icon,
            address: editedData.address,
            isPaid: editedData.isPaid
        };

        if (selectedId === 'new') {
            createSource.mutate({data: {source: sourceData}});
        } else if (typeof selectedId === 'number') {
            updateSource.mutate({id: selectedId, data: {source: sourceData}});
        }
    };

    const handleDelete = () => {
        if (typeof selectedId === 'number') {
            deleteSource.mutate({id: selectedId});
        }
    };

    const handleClose = () => {
        if (hasChanges(editedData, originalData)) {
            setPendingSelection(null);
            setShowConfirmDiscard(true);
        } else {
            setSelectedId(null);
            setEditedData(null);
            setOriginalData(null);
            onClose();
        }
    };

    const isSaving = createSource.isPending || updateSource.isPending;
    const isDeleting = deleteSource.isPending;
    const hasUnsavedChanges = hasChanges(editedData, originalData);
    const isValid = editedData?.name.trim() && editedData.icon.trim() && editedData.address.trim();

    const sourceHasChanges = (id: number): boolean => {
        if (selectedId !== id) return false;
        return hasChanges(editedData, originalData);
    };

    return (
        <Modal
            opened={opened}
            onClose={handleClose}
            size="xl"
            title="Manage Sources"
            centered
            zIndex={ZINDEX_MODAL}
        >
            {showConfirmDiscard && (
                <Alert
                    icon={<IconAlertTriangle/>}
                    title="Unsaved Changes"
                    color="yellow"
                    mb="md"
                    style={{position: 'absolute', top: 60, left: 20, right: 20, zIndex: ZINDEX_MODAL + 1}}
                >
                    <Text size="sm" mb="sm">You have unsaved changes. Discard them?</Text>
                    <Group gap="xs">
                        <Button size="xs" variant="default" onClick={handleCancelDiscard}>Cancel</Button>
                        <Button size="xs" color="yellow" onClick={handleConfirmDiscard}>Discard Changes</Button>
                    </Group>
                </Alert>
            )}

            <Group align="stretch" gap="md" style={{minHeight: 400}}>
                <Stack gap="sm" style={{width: 200, flexShrink: 0}}>
                    <Button
                        variant="light"
                        leftSection={<IconPlus size={16}/>}
                        onClick={() => handleSourceClick('new')}
                        disabled={selectedId === 'new'}
                        fullWidth
                    >
                        Create New
                    </Button>
                    <ScrollArea flex={1}>
                        <Stack gap="xs">
                            {sources.map(source => {
                                const isSelected = selectedId === source.id;
                                const selectedBg = colorScheme === 'dark' 
                                    ? 'var(--mantine-color-blue-8)' 
                                    : 'var(--mantine-color-blue-1)';
                                const selectedBorder = colorScheme === 'dark'
                                    ? 'var(--mantine-color-blue-5)'
                                    : 'var(--mantine-color-blue-4)';
                                const unselectedBg = colorScheme === 'dark'
                                    ? 'var(--mantine-color-dark-6)'
                                    : 'transparent';
                                
                                return (
                                    <UnstyledButton
                                        key={source.id}
                                        onClick={() => handleSourceClick(source.id)}
                                        style={{
                                            display: 'block',
                                            width: '100%',
                                            padding: '8px 12px',
                                            borderRadius: 4,
                                            backgroundColor: isSelected ? selectedBg : unselectedBg,
                                            border: isSelected 
                                                ? `1px solid ${selectedBorder}` 
                                                : '1px solid transparent'
                                        }}
                                    >
                                        <Group gap="xs" wrap="nowrap">
                                            <TablerIcon icon={source.icon} size={18}/>
                                            <Text size="sm" fw={isSelected ? 600 : 400} truncate>
                                                {source.name}
                                                {sourceHasChanges(source.id) && ' *'}
                                            </Text>
                                        </Group>
                                    </UnstyledButton>
                                );
                            })}
                        </Stack>
                    </ScrollArea>
                </Stack>

                <Stack gap="md" flex={1}>
                    {editedData ? (
                        <>
                            <Stack gap="sm">
                                <TextInput
                                    label="Name"
                                    placeholder="Source name"
                                    value={editedData.name}
                                    onChange={(e) => handleFieldChange('name', e.target.value)}
                                    required
                                />
                                <TextInput
                                    label="Icon"
                                    placeholder="Icon name (e.g., IconMusic)"
                                    value={editedData.icon}
                                    onChange={(e) => handleFieldChange('icon', e.target.value)}
                                    required
                                />
                                <TextInput
                                    label="Address"
                                    placeholder="Source address / URL"
                                    value={editedData.address}
                                    onChange={(e) => handleFieldChange('address', e.target.value)}
                                    required
                                />
                                <Switch
                                    label="Is Paid"
                                    checked={editedData.isPaid}
                                    onChange={(e) => handleFieldChange('isPaid', e.currentTarget.checked)}
                                />
                            </Stack>

                            <Group justify="space-between" mt="auto">
                                {selectedId !== 'new' ? (
                                    <ActionIcon
                                        variant="subtle"
                                        color="red"
                                        size="lg"
                                        onClick={handleDelete}
                                        loading={isDeleting}
                                        title="Delete source"
                                    >
                                        <IconTrash size={18}/>
                                    </ActionIcon>
                                ) : (
                                    <div/>
                                )}
                                <Group gap="xs">
                                    <Button variant="default" onClick={handleClose}>
                                        Cancel
                                    </Button>
                                    <Button
                                        onClick={handleSave}
                                        loading={isSaving}
                                        disabled={!hasUnsavedChanges || !isValid}
                                    >
                                        Save
                                    </Button>
                                </Group>
                            </Group>
                        </>
                    ) : (
                        <Center flex={1}>
                            <Text c="dimmed">Select a source to edit or create a new one</Text>
                        </Center>
                    )}
                </Stack>
            </Group>
        </Modal>
    );
}