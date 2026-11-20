import {useCallback, useEffect, useState} from "react";
import {useGetClientConfig} from "../client/config";
import {useUserPreferences} from "./use-user-preferences";

const SCRIPT_ID = "feedbox-widget";

/**
 * Loads the Feedbox feedback widget when the server configures it, and exposes a way to open its form.
 * The widget script is injected once per page and only when the server returns a feedback configuration.
 */
export function useFeedbox() {
    const {data} = useGetClientConfig({query: {staleTime: Infinity}});
    const feedback = data?.status === 200 ? data.data.feedback : null;
    const {user} = useUserPreferences();
    const [isLoaded, setIsLoaded] = useState(() => typeof window !== "undefined" && !!window.Feedbox);

    useEffect(() => {
        if (!feedback || window.Feedbox) {
            setIsLoaded(!!window.Feedbox);
            return;
        }

        let script = document.getElementById(SCRIPT_ID) as HTMLScriptElement | null;
        if (!script) {
            script = document.createElement("script");
            script.id = SCRIPT_ID;
            script.src = `${feedback.url}/widget/feedbox.js`;
            script.defer = true;
            script.dataset.project = feedback.project;
            script.dataset.trigger = "none";
            document.head.appendChild(script);
        }

        const onLoad = () => setIsLoaded(!!window.Feedbox);
        script.addEventListener("load", onLoad);
        return () => script.removeEventListener("load", onLoad);
    }, [feedback]);

    const open = useCallback(() => {
        window.Feedbox?.open({meta: {userId: String(user.id), userName: user.username}});
    }, [user.id, user.username]);

    return {isAvailable: !!feedback && isLoaded, open};
}
