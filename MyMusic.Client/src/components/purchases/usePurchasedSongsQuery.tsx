import {useListPurchases} from "../../client/purchases.ts";
import type {ListPurchasesItem} from "../../model";

export default function usePurchasedSongsQuery() {
    return useListPurchases({
        query: {
            refetchInterval: (query) =>
                arePurchasesActive(query.state.data?.data?.purchases ?? []) ? 1000 : false
        }
    });
}

function arePurchasesActive(purchases: ListPurchasesItem[] | null | undefined) {
    if (!purchases || purchases.length === 0) {
        return false;
    }

    return purchases.some(purchase => purchase.status === 'Queued' || purchase.status === 'Acquiring');
}