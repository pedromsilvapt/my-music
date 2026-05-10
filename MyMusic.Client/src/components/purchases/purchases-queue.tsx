import {saveAs} from 'file-saver';
import {Switch} from '@mantine/core';
import {useDeleteManyPurchases, useDeletePurchase, useRequeuePurchase} from "../../client/purchases.ts";
import {getDownloadSongUrl} from "../../client/songs.ts";
import {type ListPurchaseItem} from "../../model";
import PurchasesQueueList from "./purchases-queue-list.tsx";
import {useUserPreferences} from "../../hooks/use-user-preferences.ts";

export type PurchasesQueueProps = object;

export default function PurchasesQueue() {
    const requeuePurchase = useRequeuePurchase();
    const deletePurchase = useDeletePurchase();
    const deleteManyPurchases = useDeleteManyPurchases();
    const {autoDownloadOnPurchase, updateAutoDownloadOnPurchase, isUpdating} = useUserPreferences();

    const handleRequeue = (purchases: ListPurchaseItem[]) => {
        for (const purchase of purchases) {
            requeuePurchase.mutate({id: purchase.id})
        }
    };
    const handleDownload = (purchases: ListPurchaseItem[]) => {
        for (const file of purchases) {
            if (file.songId != null) {
                saveAs(getDownloadSongUrl(file.songId));
            }
        }
    };
    const handleClear = (purchases: ListPurchaseItem[]) => {
        for (const purchase of purchases) {
            deletePurchase.mutate({id: purchase.id})
        }
    };
    const handleClearCompleted = () => {
        deleteManyPurchases.mutate({
            params: {
                onlyFinished: true,
            }
        })
    };
    const handleClearAll = () => {
        deleteManyPurchases.mutate({
            params: {
                onlyFinished: false,
            }
        })
    };

    return <>
        <div style={{height: '100%'}}>
            <Switch
                label="Auto-download purchased songs"
                checked={autoDownloadOnPurchase}
                onChange={(e) => updateAutoDownloadOnPurchase(e.currentTarget.checked)}
                disabled={isUpdating}
                mb="sm"
            />

            <PurchasesQueueList
                onRequeue={handleRequeue}
                onDownload={handleDownload}
                onClear={handleClear}
                onClearCompleted={handleClearCompleted}
                onClearAll={handleClearAll}
                refreshInterval={5000} // Refresh every 5 seconds
            />
        </div>
    </>;
};
