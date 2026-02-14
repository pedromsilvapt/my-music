import {ActionIcon, Center, Drawer, type MantineColor, RingProgress} from "@mantine/core";
import {useDisclosure} from "@mantine/hooks";
import {IconAlertTriangleFilled, IconCheck, IconLoader, IconPlayerPauseFilled,} from "@tabler/icons-react";
import {useMemo} from "react";
import PurchasesQueue from "./purchases-queue.tsx";
import usePurchasedSongsQuery from "./usePurchasedSongsQuery.tsx";

export interface PurchasesQueueIndicatorProps {

}

export default function PurchasesQueueIndicator(_props: PurchasesQueueIndicatorProps) {
    const [opened, {open, close}] = useDisclosure(false);

    const {data: data} = usePurchasedSongsQuery();

    const purchases = data?.data.purchases;

    const counts = useMemo(() => {
        return {
            completed: count(purchases, p => p.status === 'Completed'),
            failed: count(purchases, p => p.status === 'Failed'),
            acquiring: count(purchases, p => p.status === 'Acquiring'),
            queued: count(purchases, p => p.status === 'Queued')
        } as PurchasesQueueCounts;
    }, [purchases]);

    const total = counts.completed + counts.failed + counts.acquiring + counts.queued;

    const label = usePurchasesQueueIndicatorIcon(counts, open, 13);
    const sections = usePurchasesQueueIndicatorSections(counts, total);

    return <>
        <Drawer opened={opened} onClose={close} size="xl" title="Purchases Queue">
            <PurchasesQueue/>
        </Drawer>

        <RingProgress
            size={45}
            thickness={5}
            sections={sections}
            transitionDuration={250}
            label={<Center>{label}</Center>}
        />
    </>;
}


interface RingProgressSection {
    value: number;
    color: MantineColor;
}

export function usePurchasesQueueIndicatorIcon(counts: PurchasesQueueCounts, onClick: () => void, iconSize: number): React.ReactNode {
    let icon: React.ReactNode;
    let color: MantineColor;

    if (counts.acquiring > 0) {
        icon = <IconLoader size={iconSize}/>;
        color = 'yellow';
    } else if (counts.queued > 0) {
        icon = <IconPlayerPauseFilled size={iconSize}/>;
        color = 'gray'
    } else if (counts.failed > 0) {
        icon = <IconAlertTriangleFilled size={iconSize}/>
        color = 'red';
    } else if (counts.completed > 0) {
        icon = <IconCheck size={iconSize}/>;
        color = 'teal';
    } else {
        icon = <IconCheck size={iconSize}/>;
        color = 'gray';
    }

    return <ActionIcon color={color} variant="subtle" radius="xxs" size="xxs" onClick={onClick}>
        {icon}
    </ActionIcon>;
}

export function usePurchasesQueueIndicatorSections(counts: PurchasesQueueCounts, total: number) {
    const sections: RingProgressSection[] = [];

    if (counts.completed > 0) {
        sections.push({value: counts.completed * 100 / total, color: 'teal'});
    }

    if (counts.failed > 0) {
        sections.push({value: counts.failed * 100 / total, color: 'red'});
    }

    if (counts.acquiring > 0) {
        sections.push({value: counts.acquiring * 100 / total, color: 'yellow'});
    }

    return sections;
}

interface PurchasesQueueCounts {
    completed: number;
    failed: number;
    acquiring: number;
    queued: number;
}

function count<T>(arr: T[] | undefined | null, predicate: (elem: T) => boolean) {
    if (!arr) {
        return 0;
    }

    let counter = 0;

    for (const elem of arr) {
        if (predicate(elem)) {
            counter++;
        }
    }

    return counter;
}