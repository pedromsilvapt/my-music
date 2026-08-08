/* eslint-disable react-refresh/only-export-components */
import {createContext, useCallback, useContext, useState} from "react";
import ManageSharingDialog from "../components/sharing/manage-sharing-dialog.tsx";

interface ManageSharingContextValue {
    open: (songIds: number[]) => void;
    registerRefetch: (key: string, fn: () => void) => void;
    unregisterRefetch: (key: string) => void;
}

export const ManageSharingContext = createContext<ManageSharingContextValue>(null!);

export function useManageSharingContext() {
    const context = useContext(ManageSharingContext);
    if (!context) {
        throw new Error('Missing ManageSharingProvider');
    }
    return context;
}

interface ManageSharingProviderProps {
    children: React.ReactNode;
}

export default function ManageSharingProvider({children}: ManageSharingProviderProps) {
    const [opened, setOpened] = useState(false);
    const [songIds, setSongIds] = useState<number[]>([]);
    const [refetchFns, setRefetchFns] = useState<Map<string, () => void>>(new Map());

    const open = useCallback((newSongIds: number[]) => {
        setSongIds(newSongIds);
        setOpened(true);
    }, []);

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
        <ManageSharingContext.Provider value={{open, registerRefetch, unregisterRefetch}}>
            {children}
            <ManageSharingDialog
                opened={opened}
                onClose={handleClose}
                songIds={songIds}
                onSuccess={handleSuccess}
            />
        </ManageSharingContext.Provider>
    );
}