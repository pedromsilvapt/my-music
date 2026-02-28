import {useListPurchases} from "../../client/purchases.ts";
import {PURCHASE_REFETCH_INTERVAL_MS} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListPurchasesItem} from "../../model";

export default function usePurchasedSongsQuery() {
    const purchasesQuery = useListPurchases({
        query: {
            refetchInterval: (query) =>
                arePurchasesActive(query.state.data?.data?.purchases ?? []) ? PURCHASE_REFETCH_INTERVAL_MS : false
        }
    });

    const purchasesResponse = useQueryData(
        purchasesQuery,
        "Failed to fetch purchases"
    );

    if (!purchasesResponse) {
        return {
            ...purchasesQuery,
            data: {data: {purchases: [] as ListPurchasesItem[], total: 0}},
        };
    }

    return {
        ...purchasesQuery,
        data: purchasesResponse,
    };
}

function arePurchasesActive(purchases: ListPurchasesItem[] | null | undefined) {
    if (!purchases || purchases.length === 0) {
        return false;
    }

    return purchases.some(purchase => purchase.status === 'Queued' || purchase.status === 'Acquiring');
}