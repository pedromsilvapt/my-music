import {useQueryClient} from '@tanstack/react-query';
import {useEffect, useRef} from 'react';
import {useListPurchases} from '../../client/purchases.ts';
import {getGetSongHistoryQueryKey} from '../../client/song-history.ts';
import {getGetLocalSongQueryKey, getListSongsQueryKey} from '../../client/songs.ts';

/**
 * Refetches the songs whose audio a purchase has just replaced: the purchase runs in the queue, so nothing else
 * tells the pages showing the song that its file, and its history, have changed.
 */
export function useReplacedSongsRefresh() {
    const queryClient = useQueryClient();
    const {data: purchasesData} = useListPurchases();
    const unfinishedRef = useRef(new Set<number>());

    const purchases = purchasesData?.data?.purchases;

    useEffect(() => {
        if (!purchases) return;

        let anyReplaced = false;

        for (const purchase of purchases) {
            if (!purchase.replacesSongFile || purchase.songId == null) continue;

            if (purchase.status !== 'Completed') {
                unfinishedRef.current.add(purchase.id);
            } else if (unfinishedRef.current.delete(purchase.id)) {
                // Seen unfinished before, so it was completed while the page was open
                anyReplaced = true;
                void queryClient.invalidateQueries({queryKey: getGetLocalSongQueryKey(purchase.songId)});
                void queryClient.invalidateQueries({queryKey: getGetSongHistoryQueryKey(purchase.songId)});
            }
        }

        if (anyReplaced) {
            void queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
        }
    }, [purchases, queryClient]);
}
