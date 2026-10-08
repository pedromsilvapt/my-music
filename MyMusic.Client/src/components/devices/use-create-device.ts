import {modals} from "@mantine/modals";
import {useCallback} from "react";
import {useTranslation} from "react-i18next";

/**
 * Returns a function that opens the device editor to create a device.
 */
export function useCreateDevice() {
    const {t} = useTranslation(["devices"]);

    return useCallback((onSuccess?: (deviceId: number) => void) => {
        modals.openContextModal({
            modal: 'device-editor',
            title: t("devices:editModal.titleCreate"),
            centered: true,
            size: 'xl',
            innerProps: {onSuccess},
        });
    }, [t]);
}
