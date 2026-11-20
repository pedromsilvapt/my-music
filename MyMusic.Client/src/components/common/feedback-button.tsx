import {ActionIcon, Tooltip} from "@mantine/core";
import {IconMessageReport} from "@tabler/icons-react";
import {useTranslation} from "react-i18next";

import {useFeedbox} from "../../hooks/use-feedbox";

export default function FeedbackButton() {
    const {t} = useTranslation("common");
    const {isAvailable, open} = useFeedbox();

    if (!isAvailable) {
        return null;
    }

    return (
        <Tooltip label={t("feedback.open")}>
            <ActionIcon
                variant="default"
                size="lg"
                aria-label={t("feedback.open")}
                onClick={open}
                data-testid="topbar-feedback"
            >
                <IconMessageReport size={18} stroke={1.5}/>
            </ActionIcon>
        </Tooltip>
    );
}
