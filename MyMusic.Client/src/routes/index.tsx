import {createFileRoute} from '@tanstack/react-router'
import {Center, Text} from "@mantine/core";

export const Route = createFileRoute('/')({
    component: Index,
})

function Index() {
    return <>
        <Center>
            <Text>TBD</Text>
        </Center>
    </>;
}