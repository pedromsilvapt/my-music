import {useListPurchases} from "../../client/purchases.ts";
import {useTranslation} from "react-i18next";
import {PURCHASE_REFETCH_INTERVAL_MS} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListPurchaseItem} from "../../model";

export default function usePurchasedSongsQuery() {
    const {t} = useTranslation(["purchases", "common"]);
    const purchasesQuery = useListPurchases({
        query: {
            refetchInterval: (query) =>
                arePurchasesActive(query.state.data?.data?.purchases ?? []) ? PURCHASE_REFETCH_INTERVAL_MS : false
        }
    });

    const purchasesResponse = useQueryData(
        purchasesQuery,
        t("purchases:page.fetchFailed")
    );

    if (!purchasesResponse) {
        return {
            ...purchasesQuery,
            data: {data: {purchases: [] as ListPurchaseItem[], total: 0}},
        };
    }

    return {
        ...purchasesQuery,
        data: purchasesResponse,
    };
}

function arePurchasesActive(purchases: ListPurchaseItem[] | null | undefined) {
    if (!purchases || purchases.length === 0) {
        return false;
    }

    return purchases.some(purchase => purchase.status === 'Queued' || purchase.status === 'Acquiring');
}