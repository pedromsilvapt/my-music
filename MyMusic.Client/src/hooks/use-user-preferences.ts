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
    }, [user.colorScheme, isLoading, setColorScheme]);

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

    return {
        user,
        isLoading,
        colorScheme: user.colorScheme as "light" | "dark" | "auto",
        volume: user.volume,
        isMuted: user.isMuted,
        updateColorScheme,
        updateVolume,
        updateIsMuted,
        isUpdating: updateMutation.isPending,
    };
}