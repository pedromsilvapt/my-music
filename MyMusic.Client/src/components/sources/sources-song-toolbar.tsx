import {ActionIcon, Center, Group, SegmentedControl} from "@mantine/core";
import {useUncontrolled} from "@mantine/hooks";
import {useEffect, useMemo} from "react";
import {useListSources} from "../../client/sources.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListSourcesItem, SourceSong} from "../../model";
import CollectionToolbar, {type CollectionToolbarProps} from "../common/collection/collection-toolbar.tsx";
import type {CollectionFilterBarRef} from "../common/collection/collection-filter-bar.tsx";
import TablerIcon from "../common/tabler-icon.tsx";
import {IconPencil} from "@tabler/icons-react";

export interface SourcesSearchToolbarProps extends CollectionToolbarProps<SourceSong> {
    source?: ListSourcesItem | null | undefined,
    setSource?: (source: ListSourcesItem | null | undefined) => void,
    searchInputRef?: React.RefObject<CollectionFilterBarRef | null>;
    onManageSources?: () => void;
}

export default function SourcesSearchToolbar(props: SourcesSearchToolbarProps) {
    const [source, setSource] = useUncontrolled<ListSourcesItem | null | undefined>({
        value: props.source,
        onChange: props.setSource,
    });

    const sourcesQuery = useListSources();

    const sourcesResponse = useQueryData(sourcesQuery, "Failed to fetch sources") ?? {data: {sources: []}};

    const sources = sourcesResponse?.data.sources ?? [];

    const sourcesOptions = useMemo(() => sources.map(source => ({
        value: '' + source.id,
        label: (
            <Center style={{gap: 10}}>
                <TablerIcon icon={source.icon} size={16}/>
                <span>{source.name}</span>
            </Center>
        )
    })), [sources]);

    useEffect(() => {
        if (source == null && sources.length > 0) {
            setSource(sources[0]);
        }
    }, [source, sources, setSource]);

    return (
        <CollectionToolbar
            {...props}
            searchInputRef={props.searchInputRef}
            filterMode="server"
            searchPlaceholder="Search songs..."
            renderLeftSection={() => (
                <Group gap="xs" wrap="nowrap">
                    <SegmentedControl
                        value={source?.id?.toString()}
                        onChange={sourceId => setSource(sources.find(s => s.id.toString() === sourceId))}
                        data={sourcesOptions}
                    />
                    <ActionIcon
                        variant="subtle"
                        size="lg"
                        onClick={props.onManageSources}
                        title="Manage sources"
                    >
                        <IconPencil size={18}/>
                    </ActionIcon>
                </Group>
            )}
        />
    );
}