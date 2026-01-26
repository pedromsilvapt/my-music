import {Badge, Flex} from "@mantine/core";

export interface ExplicitLabelProps {
    visible: boolean;
    children: React.ReactNode;
}

export default function ExplicitLabel(props: ExplicitLabelProps) {
    return <Flex align="center" component="span">
        {props.children}
        {props.visible && <Badge component="span" radius="sm" size="xs" color="gray" ml={6}>E</Badge>}
    </Flex>;
}