import {Badge} from "@mantine/core";
import {useTranslation} from "react-i18next";

export interface PriceProps {
    value: number | null | undefined;
}

export default function Price(props: PriceProps) {
    const {t} = useTranslation(["common"]);
    const value = props.value ?? 0;

    if (props.value === 0) {
        return <Badge>{t("common:common.free")}</Badge>;
    } else {
        return <Badge>{value} €</Badge>;
    }
}