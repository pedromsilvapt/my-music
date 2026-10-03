import type {ReactNode} from 'react';
import {useCallback} from 'react';
import {useTranslation} from 'react-i18next';
import {modals} from '@mantine/modals';
import {notifications} from '@mantine/notifications';

export interface ConfirmDeleteOptions {
    title: ReactNode;
    children: ReactNode;
    /** Performs the deletion; the dialog stays open (with a loading button) until it settles. */
    onConfirm: () => Promise<unknown>;
    /** Runs after the dialog closes following a successful deletion. */
    onSuccess?: () => void;
    /** Notification message shown when the deletion fails. */
    errorMessage: string;
}

/**
 * Opens a destructive confirmation dialog that only closes once the deletion succeeds.
 * While the request is pending the dialog can't be dismissed; on failure it stays open
 * and an error notification is shown.
 */
export function useConfirmDelete() {
    const {t} = useTranslation(["common"]);

    return useCallback(({title, children, onConfirm, onSuccess, errorMessage}: ConfirmDeleteOptions) => {
        const setPending = (pending: boolean) => modals.updateModal({
            modalId,
            confirmProps: {color: 'red', loading: pending},
            cancelProps: {disabled: pending},
            closeOnEscape: !pending,
            closeOnClickOutside: !pending,
            withCloseButton: !pending,
        });

        const modalId = modals.openConfirmModal({
            title,
            children,
            labels: {confirm: t("common:actions.delete"), cancel: t("common:actions.cancel")},
            confirmProps: {color: 'red'},
            closeOnConfirm: false,
            onConfirm: async () => {
                setPending(true);
                try {
                    await onConfirm();
                } catch (error) {
                    setPending(false);
                    notifications.show({title: t("common:status.error"), message: errorMessage, color: 'red'});
                    console.error('Failed to delete:', error);
                    return;
                }
                modals.close(modalId);
                onSuccess?.();
            },
        });
    }, [t]);
}
