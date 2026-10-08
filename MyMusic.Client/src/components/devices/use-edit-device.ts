import {modals} from "@mantine/modals";
import {useCallback} from "react";
import {useTranslation} from "react-i18next";
import type {EditableDevice} from "./device-editor-modal.tsx";

/**
 * Returns a function that opens the editor for a device.
 */
export function useEditDevice() {
    const {t} = useTranslation(["devices"]);

    return useCallback((device: EditableDevice, onSuccess?: (deviceId: number) => void) => {
        modals.openContextModal({
            modal: 'device-editor',
            title: t("devices:editModal.titleEdit"),
            centered: true,
            size: 'xl',
            innerProps: {
                device: {
                    id: device.id,
                    name: device.name,
                    icon: device.icon,
                    color: device.color,
                    namingTemplate: device.namingTemplate,
                    importOnPurchase: device.importOnPurchase,
                },
                onSuccess,
            },
        });
    }, [t]);
}
