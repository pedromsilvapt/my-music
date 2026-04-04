import { useListWishlist, useCreateWishlist, useUpdateWishlist, useDeleteWishlist } from "../client/wishlist";
import type { ListWishlistParams } from "../model";

export function useWishlist(sourceId?: number) {
    const params: ListWishlistParams | undefined = sourceId ? { sourceId } : undefined;
    return useListWishlist(params);
}

export function useCreateWishlistMutation() {
    return useCreateWishlist();
}

export function useUpdateWishlistMutation() {
    return useUpdateWishlist();
}

export function useRemoveWishlistMutation() {
    return useDeleteWishlist();
}