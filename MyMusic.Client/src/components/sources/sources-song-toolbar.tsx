import {Box, Center, SegmentedControl, TextInput} from "@mantine/core";
import {useUncontrolled} from "@mantine/hooks";
import {IconSearch} from '@tabler/icons-react'
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
    const [search, setSearch] = useUncontrolled({
        value: props.search,
        defaultValue: '',
        onChange: props.setSearch,
    });
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
                {/*<IconBrandYoutubeFilled size={16}/>*/}
                <span>{source.name}</span>
            </Center>
        )
    })), [sources]);

    // Auto-select first source
    useEffect(() => {
        if (source == null && sources.length > 0) {
            setSource(sources[0]);
        }
    }, [source, sources]);

    // className={styles.toolbar}
    return <CollectionToolbar {...props}
                              renderLeftSection={() => <Box>
                                  <SegmentedControl
                                      value={source?.id?.toString()}
                                      onChange={source => setSource(sources.find(s => s.id.toString() == source))}
                                      data={sourcesOptions}
                                  />
                              </Box>}
                              renderMiddleSection={() => <TextInput placeholder="Search..."
                                                                    leftSection={<IconSearch/>}
                                                                    value={search}
                                                                    onChange={e => setSearch(e.currentTarget.value)}/>}
    />;
    // return <Group justify="space-between" grow={true}>
    //    
    //    
    //     <Box></Box>
    // </Group>;
}