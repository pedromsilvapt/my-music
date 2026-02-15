import {Button, Group, Modal, Stack, TextInput} from "@mantine/core";
import {useState} from "react";
import {useCreatePlaylist} from "../../client/playlists.ts";

interface CreatePlaylistModalProps {
    opened: boolean;
    onClose: () => void;
    onSuccess?: () => void;
}

export default function CreatePlaylistModal({opened, onClose, onSuccess}: CreatePlaylistModalProps) {
    const [name, setName] = useState("");

    const createPlaylist = useCreatePlaylist({
        mutation: {
            onSuccess: () => {
                setName("");
                onClose();
                onSuccess?.();
            }
        }
    });

    const handleCreate = () => {
        if (name.trim()) {
            createPlaylist.mutate({data: {name: name.trim()}});
        }
    };

    return (
        <Modal opened={opened} onClose={onClose} title="Create Playlist" centered>
            <Stack>
                <TextInput
                    label="Playlist Name"
                    placeholder="My Playlist"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                            handleCreate();
                        }
                    }}
                    autoFocus
                />
                <Group justify="flex-end">
                    <Button variant="subtle" onClick={onClose}>
                        Cancel
                    </Button>
                    <Button onClick={handleCreate} loading={createPlaylist.isPending}>
                        Create
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
