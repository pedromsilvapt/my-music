import {createContext, useContext, useState} from "react";
import ManageDevicesDialog from "../components/devices/manage-devices-dialog.tsx";

interface ManageDevicesContextValue {
    open: (songIds: number[]) => void;
}

export const ManageDevicesContext = createContext<ManageDevicesContextValue>(null!);

export function useManageDevicesContext() {
    const context = useContext(ManageDevicesContext);
    if (!context) {
        throw new Error('Missing ManageDevicesProvider');
    }
    return context;
}

interface ManageDevicesProviderProps {
    children: React.ReactNode;
}

export default function ManageDevicesProvider({children}: ManageDevicesProviderProps) {
    const [opened, setOpened] = useState(false);
    const [songIds, setSongIds] = useState<number[]>([]);

    const open = (newSongIds: number[]) => {
        setSongIds(newSongIds);
        setOpened(true);
    };

    const handleClose = () => {
        setOpened(false);
        setSongIds([]);
    };

    return (
        <ManageDevicesContext.Provider value={{open}}>
            {children}
            <ManageDevicesDialog
                opened={opened}
                onClose={handleClose}
                songIds={songIds}
            />
        </ManageDevicesContext.Provider>
    );
}
