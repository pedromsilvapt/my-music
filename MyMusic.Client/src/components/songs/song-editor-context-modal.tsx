import {Loader} from "@mantine/core";
import type {ContextModalProps} from "@mantine/modals";
import {useEffect, useState} from "react";
import {useQueryClient} from "@tanstack/react-query";
import {getSong} from "../../client/songs";
import type {GetSongResponseSong} from "../../model";
import SongEditorModal from "./song-editor-modal";

interface SongEditorContextModalInnerProps {
    songIds: number[];
    onSuccess?: () => void;
}

export default function SongEditorContextModal({
    context,
    id,
    innerProps,
}: ContextModalProps<SongEditorContextModalInnerProps>) {
    const [songs, setSongs] = useState<GetSongResponseSong[]>([]);
    const [loading, setLoading] = useState(true);
    const queryClient = useQueryClient();

    const handleClose = () => {
        setSongs([]);
        setLoading(true);
        context.closeModal(id);
    };

    const handleSuccess = async () => {
        await queryClient.invalidateQueries({queryKey: ["api", "songs"]});
        innerProps?.onSuccess?.();
    };

    useEffect(() => {
        const fetchSongs = async () => {
            setLoading(true);
            const fetched: GetSongResponseSong[] = [];
            for (const songId of innerProps.songIds) {
                const response = await getSong(songId);
                if (response.data.song) {
                    fetched.push(response.data.song);
                }
            }
            setSongs(fetched);
            setLoading(false);
        };
        fetchSongs();
    }, [innerProps.songIds]);

    if (loading) {
        return <Loader size="lg" m="auto" my="xl" />;
    }

    return (
        <SongEditorModal
            opened={true}
            onClose={handleClose}
            songs={songs}
            onSuccess={handleSuccess}
        />
    );
}
