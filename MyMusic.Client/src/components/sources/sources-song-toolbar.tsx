import {Box, Center, SegmentedControl} from "@mantine/core";
import {useUncontrolled} from "@mantine/hooks";
import {useEffect, useMemo} from "react";
import {useListSources} from "../../client/sources.ts";
import type {ListSourcesItem, SourceSong} from "../../model";
import CollectionToolbar, {type CollectionToolbarProps} from "../common/collection/collection-toolbar.tsx";
import TablerIcon from "../common/tabler-icon.tsx";

export interface SourcesSearchToolbarProps extends CollectionToolbarProps<SourceSong> {
    source?: ListSourcesItem | null | undefined,
    setSource?: (source: ListSourcesItem | null | undefined) => void,
}

export default function SourcesSearchToolbar(props: SourcesSearchToolbarProps) {
    const [source, setSource] = useUncontrolled<ListSourcesItem | null | undefined>({
        value: props.source,
        onChange: props.setSource,
    });

    const {data: sourcesData} = useListSources();

    const sources = sourcesData?.data.sources ?? [];

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
            filterMode="server"
            searchPlaceholder="Search songs..."
            renderLeftSection={() => (
                <Box>
                    <SegmentedControl
                        value={source?.id?.toString()}
                        onChange={sourceId => setSource(sources.find(s => s.id.toString() === sourceId))}
                        data={sourcesOptions}
                    />
                </Box>
            )}
        />
    );
}