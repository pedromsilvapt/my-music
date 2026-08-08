import {createFileRoute} from '@tanstack/react-router'
import {Center, Text} from "@mantine/core";
import {useTranslation} from "react-i18next";

export const Route = createFileRoute('/')({
    component: Index,
})

function Index() {
    const {t} = useTranslation(["common"]);
    return <>
        <Center data-testid="home">
            <Text>{t("common:common.underConstruction")}</Text>
        </Center>
    </>;
}