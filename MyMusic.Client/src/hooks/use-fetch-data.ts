import {notifications} from "@mantine/notifications";

type ErrorMessageFactory<T extends unknown[]> = (...args: T) => string;

export function useFetchData<TArgs extends unknown[], TResult>(
    fetchFn: (...args: TArgs) => Promise<TResult>,
    getErrorMessage: ErrorMessageFactory<TArgs>
): (...args: TArgs) => Promise<TResult> {
    return async (...args: TArgs) => {
        try {
            return await fetchFn(...args);
        } catch (error) {
            notifications.show({
                title: "Error",
                message: getErrorMessage(...args),
                color: "red",
            });
            throw error;
        }
    };
}
