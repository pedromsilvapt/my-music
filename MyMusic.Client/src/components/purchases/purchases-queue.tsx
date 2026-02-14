import {saveAs} from 'file-saver';
import {useDeleteManyPurchases, useDeletePurchase, useRequeuePurchase} from "../../client/purchases.ts";
import {getDownloadSongUrl} from "../../client/songs.ts";
import {type ListPurchasesItem} from "../../model";
import PurchasesQueueList from "./purchases-queue-list.tsx";

export interface PurchasesQueueProps {

}

export default function PurchasesQueue({}: PurchasesQueueProps) {
    const requeuePurchase = useRequeuePurchase();
    const deletePurchase = useDeletePurchase();
    const deleteManyPurchases = useDeleteManyPurchases();

    const handleRequeue = (purchases: ListPurchasesItem[]) => {
        for (const purchase of purchases) {
            requeuePurchase.mutate({id: purchase.id})
        }
    };
    const handleDownload = (purchases: ListPurchasesItem[]) => {
        for (const file of purchases) {
            if (file.songId != null) {
                saveAs(getDownloadSongUrl(file.songId));
            }
        }
    };
    const handleClear = (purchases: ListPurchasesItem[]) => {
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
        <div style={{height: 'var(--parent-height)'}}>
            {/*<Collection*/}
            {/*    key="artists"*/}
            {/*    items={elements}*/}
            {/*    schema={albumsSchema}>*/}
            {/*</Collection>*/}

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