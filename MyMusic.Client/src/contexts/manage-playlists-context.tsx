import {createContext, useCallback, useContext, useState} from "react";
import ManagePlaylistsDialog from "../components/playlists/manage-playlists-dialog.tsx";

interface ManagePlaylistsContextValue {
    open: (songIds: number[]) => void;
    registerRefetch: (key: string, fn: () => void) => void;
    unregisterRefetch: (key: string) => void;
}

export const ManagePlaylistsContext = createContext<ManagePlaylistsContextValue>(null!);

export function useManagePlaylistsContext() {
    const context = useContext(ManagePlaylistsContext);
    if (!context) {
        throw new Error('Missing ManagePlaylistsProvider');
    }
    return context;
}

interface ManagePlaylistsProviderProps {
    children: React.ReactNode;
}

export default function ManagePlaylistsProvider({children}: ManagePlaylistsProviderProps) {
    const [opened, setOpened] = useState(false);
    const [songIds, setSongIds] = useState<number[]>([]);
    const [refetchFns, setRefetchFns] = useState<Map<string, () => void>>(new Map());

    const open = (newSongIds: number[]) => {
        setSongIds(newSongIds);
        setOpened(true);
    };

    const registerRefetch = useCallback((key: string, fn: () => void) => {
        setRefetchFns(prev => {
            const newMap = new Map(prev);
            newMap.set(key, fn);
            return newMap;
        });
    }, []);

    const unregisterRefetch = useCallback((key: string) => {
        setRefetchFns(prev => {
            const newMap = new Map(prev);
            newMap.delete(key);
            return newMap;
        });
    }, []);

    const handleClose = () => {
        setOpened(false);
        setSongIds([]);
    };

    const handleSuccess = () => {
        refetchFns.forEach(fn => fn());
        handleClose();
    };

    return (
        <ManagePlaylistsContext.Provider value={{open, registerRefetch, unregisterRefetch}}>
            {children}
            <ManagePlaylistsDialog
                opened={opened}
                onClose={handleClose}
                songIds={songIds}
                onSuccess={handleSuccess}
            />
        </ManagePlaylistsContext.Provider>
    );
}
