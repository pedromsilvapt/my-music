import {Button} from "@mantine/core";
import {IconRefresh} from "@tabler/icons-react";
import {useTranslation} from "react-i18next";
import {useBatchMetadataFetch} from "../../hooks/useBatchMetadataFetch";

interface AutoFetchButtonProps {
    onSuccess?: () => void;
}

export function AutoFetchButton({onSuccess}: AutoFetchButtonProps) {
    const {t} = useTranslation(["audits", "common"]);
    const batchFetch = useBatchMetadataFetch();

    const handleClick = () => {
        batchFetch.mutate(undefined, {
            onSuccess: () => {
                onSuccess?.();
            }
        });
    };

    return (
        <Button
            leftSection={<IconRefresh size={18}/>}
            onClick={handleClick}
            loading={batchFetch.isPending}
            variant="light"
        >
            {t("audits:autoFetch.label")}
        </Button>
    );
}
