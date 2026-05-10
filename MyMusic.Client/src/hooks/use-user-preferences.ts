import {useMantineColorScheme} from "@mantine/core";
import {useEffect} from "react";
import {useGetCurrentUser, useUpdateCurrentUser} from "../client/users";
import type {GetUserItem} from "../model/getUserItem";

const DEFAULT_USER: GetUserItem = {
    id: 0,
    username: "",
    name: "",
    colorScheme: "auto",
    volume: 1.0,
    isMuted: false,
    autoDownloadOnPurchase: false,
};

export function useUserPreferences() {
    const {setColorScheme} = useMantineColorScheme();
    const {data, isLoading} = useGetCurrentUser({
        query: {
            initialData: {data: {user: DEFAULT_USER}, status: 200, headers: new Headers()},
        },
    });
    const updateMutation = useUpdateCurrentUser();

    const user = data?.data?.user ?? DEFAULT_USER;

    useEffect(() => {
        if (user.colorScheme && !isLoading) {
            const validSchemes = ["light", "dark", "auto"] as const;
            const scheme = validSchemes.includes(user.colorScheme as typeof validSchemes[number])
                ? user.colorScheme as "light" | "dark" | "auto"
                : "auto";
            setColorScheme(scheme);
        }
        // setColorScheme is excluded from deps as it causes infinite re-renders when color scheme is manually set
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [user.colorScheme, isLoading]);

    const updateColorScheme = async (colorScheme: "light" | "dark" | "auto") => {
        await updateMutation.mutateAsync({data: {colorScheme}});
        setColorScheme(colorScheme);
    };

    const updateVolume = async (volume: number) => {
        await updateMutation.mutateAsync({data: {volume}});
    };

    const updateIsMuted = async (isMuted: boolean) => {
        await updateMutation.mutateAsync({data: {isMuted}});
    };

    const updateAutoDownloadOnPurchase = async (autoDownloadOnPurchase: boolean) => {
        await updateMutation.mutateAsync({data: {autoDownloadOnPurchase}});
    };

    return {
        user,
        isLoading,
        colorScheme: user.colorScheme as "light" | "dark" | "auto",
        volume: user.volume,
        isMuted: user.isMuted,
        autoDownloadOnPurchase: user.autoDownloadOnPurchase,
        updateColorScheme,
        updateVolume,
        updateIsMuted,
        updateAutoDownloadOnPurchase,
        isUpdating: updateMutation.isPending,
    };
}