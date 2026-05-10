import {saveAs} from 'file-saver';
import {useEffect, useRef} from 'react';
import {getDownloadSongUrl} from '../../client/songs.ts';
import {useGetCurrentUser} from '../../client/users.ts';
import {useListPurchases} from '../../client/purchases.ts';
import {usePurchasesStore} from '../../stores/purchases-store.ts';

const AUTO_DOWNLOAD_STAGGER_MS = 1000;

export function useAutoDownload() {
    const {pendingAutoDownloads, removePendingAutoDownload} = usePurchasesStore();
    const {data: userData} = useGetCurrentUser();
    const {data: purchasesData} = useListPurchases();
    const downloadingRef = useRef(new Set<number>());

    const autoDownloadOnPurchase = userData?.data?.user?.autoDownloadOnPurchase ?? false;
    const purchases = purchasesData?.data?.purchases;

    useEffect(() => {
        if (!autoDownloadOnPurchase || !purchases) return;

        const completedIds: number[] = [];

        for (const purchase of purchases) {
            if (
                purchase.status === 'Completed' &&
                purchase.songId != null &&
                pendingAutoDownloads.has(purchase.id) &&
                !downloadingRef.current.has(purchase.id)
            ) {
                completedIds.push(purchase.id);
            }
        }

        if (completedIds.length === 0) return;

        void (async () => {
            for (let i = 0; i < completedIds.length; i++) {
                const purchaseId = completedIds[i];
                downloadingRef.current.add(purchaseId);

                if (i > 0) {
                    await new Promise((resolve) => setTimeout(resolve, AUTO_DOWNLOAD_STAGGER_MS));
                }

                const purchase = purchases.find((p) => p.id === purchaseId);
                if (purchase?.songId != null) {
                    saveAs(getDownloadSongUrl(purchase.songId));
                }

                removePendingAutoDownload(purchaseId);
            }
        })();
    }, [autoDownloadOnPurchase, purchases, pendingAutoDownloads, removePendingAutoDownload]);
}