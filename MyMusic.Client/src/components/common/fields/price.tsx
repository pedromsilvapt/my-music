import {Badge} from "@mantine/core";

export interface PriceProps {
    value: number | null | undefined;
}

export default function Price(props: PriceProps) {
    const value = props.value ?? 0;

    if (props.value === 0) {
        return <Badge>Free</Badge>;
    } else {
        return <Badge>{value} €</Badge>;
    }
}