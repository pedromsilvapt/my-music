import {notifications} from "@mantine/notifications";
import {type UseQueryResult} from "@tanstack/react-query";

export function useQueryData<TData>(
    query: UseQueryResult<TData, unknown>,
    errorMessage: string
): TData | null {
    const hasApiError = query.data && typeof query.data === 'object' && 'status' in query.data && (query.data as {
        status: number
    }).status >= 400;

    if (query.isError || hasApiError) {
        console.error(errorMessage, query.error ?? query.data);
        notifications.show({
            title: "Error",
            message: errorMessage,
            color: "red",
        });
        return null;
    }

    return query.data ?? null;
}