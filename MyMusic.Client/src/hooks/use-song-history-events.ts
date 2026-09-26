import {useQueryClient} from "@tanstack/react-query";
import {useEffect, useState} from "react";
import {getGetSongHistoryQueryKey, getStreamSongHistoryEventsUrl} from "../client/song-history.ts";
import {getGetLocalSongQueryKey, type getLocalSongResponse} from "../client/songs.ts";

/**
 * Subscribes to the song's history Server-Sent Events while it has pending history, refetching
 * the song history on every `processed` event. The server sends `complete` and closes the stream
 * once nothing is pending; the song is then refetched so `hasPendingHistory` reflects it, and the
 * stream is reopened if new history was queued in the meantime.
 *
 * @returns `isPending` — true while the stream is open, i.e. history updates are still expected.
 */
export function useSongHistoryEvents(songId: number, hasPendingHistory: boolean) {
    const queryClient = useQueryClient();
    const [isPending, setIsPending] = useState(false);

    useEffect(() => {
        if (!hasPendingHistory) {
            setIsPending(false);
            return;
        }

        let eventSource: EventSource | null = null;
        let disposed = false;

        const invalidateHistory = () =>
            queryClient.invalidateQueries({queryKey: getGetSongHistoryQueryKey(songId)});

        const connect = () => {
            eventSource = new EventSource(getStreamSongHistoryEventsUrl(songId));
            setIsPending(true);

            eventSource.addEventListener("processed", () => {
                void invalidateHistory();
            });

            eventSource.addEventListener("complete", async () => {
                // Close explicitly, otherwise EventSource auto-reconnects when the server ends the stream
                eventSource?.close();
                eventSource = null;
                setIsPending(false);

                await Promise.all([
                    invalidateHistory(),
                    queryClient.invalidateQueries({queryKey: getGetLocalSongQueryKey(songId)}),
                ]);

                // New history may have been queued between the server's last check and the refetch
                const song = queryClient.getQueryData<getLocalSongResponse>(getGetLocalSongQueryKey(songId));
                if (!disposed && song?.data.song.hasPendingHistory) {
                    connect();
                }
            });
        };

        connect();

        return () => {
            disposed = true;
            eventSource?.close();
        };
    }, [songId, hasPendingHistory, queryClient]);

    return {isPending};
}
