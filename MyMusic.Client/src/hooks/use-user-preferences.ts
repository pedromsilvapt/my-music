import {useMantineColorScheme} from "@mantine/core";
import {useEffect} from "react";
import {useGetCurrentUser, useUpdateCurrentUser} from "../client/users";
import type {GetUserItem} from "../model/getUserItem";
import {DEFAULT_LANGUAGE, i18n, isSupportedLanguage} from "../locales";

const DEFAULT_USER: GetUserItem = {
    id: 0,
    username: "",
    name: "",
    colorScheme: "auto",
    language: DEFAULT_LANGUAGE,
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

    useEffect(() => {
        const language = isSupportedLanguage(user.language) ? user.language : DEFAULT_LANGUAGE;
        if (i18n.language !== language) {
            void i18n.changeLanguage(language);
        }
        document.documentElement.lang = language;
    }, [user.language]);

    const updateColorScheme = async (colorScheme: "light" | "dark" | "auto") => {
        await updateMutation.mutateAsync({data: {colorScheme}});
        setColorScheme(colorScheme);
    };

    const updateLanguage = async (language: string) => {
        await updateMutation.mutateAsync({data: {language}});
        if (isSupportedLanguage(language)) {
            await i18n.changeLanguage(language);
            document.documentElement.lang = language;
        }
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
        language: user.language,
        volume: user.volume,
        isMuted: user.isMuted,
        autoDownloadOnPurchase: user.autoDownloadOnPurchase,
        updateColorScheme,
        updateLanguage,
        updateVolume,
        updateIsMuted,
        updateAutoDownloadOnPurchase,
        isUpdating: updateMutation.isPending,
    };
}