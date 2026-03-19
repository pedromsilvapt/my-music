import {Box, Flex, Tooltip} from "@mantine/core";
import TablerIcon from "../common/tabler-icon.tsx";
import type {ListSongItem, ListSongsDevice} from "../../model";
import {TEXT_COLOR, DIMMED_COLOR} from "../../utils/colors.ts";

interface SongDevicesCellProps {
    song: ListSongItem;
    allDevices: ListSongsDevice[];
}

export function SongDevicesCell({song, allDevices}: SongDevicesCellProps) {
    // Get the set of device IDs this song is on
    const songDeviceIds = new Set(song.devices.map(d => d.id));
    
    if (allDevices.length === 0) {
        return <Box miw={20} />;
    }
    
    return (
        <Flex gap={6} align="center">
            {allDevices.map(device => {
                const isOnDevice = songDeviceIds.has(device.id);
                // Use main text color when song is on device, dimmed color when not
                const iconColor = isOnDevice ? TEXT_COLOR : DIMMED_COLOR;
                
                return (
                    <Tooltip key={device.id} label={device.name}>
                        <Box>
                            <TablerIcon 
                                icon={device.icon} 
                                color={iconColor}
                                size={16}
                                defaultIcon="IconDeviceMobile"
                            />
                        </Box>
                    </Tooltip>
                );
            })}
        </Flex>
    );
}
