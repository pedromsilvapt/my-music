import {useQuery} from "@tanstack/react-query";
import {useState} from "react";
import Collection from "../common/collection/collection.tsx";
import {useDevicesSchema} from "./useDevicesSchema.tsx";

export default function DevicesPage() {
    const [appliedSearch, setAppliedSearch] = useState("");
    const [appliedFilter, setAppliedFilter] = useState("");

    const {data} = useQuery({
        queryKey: ["devices", appliedSearch, appliedFilter],
        queryFn: async () => {
            const params = new URLSearchParams();
            if (appliedSearch) params.set("search", appliedSearch);
            if (appliedFilter) params.set("filter", appliedFilter);

            const url = `/api/api/devices${params.toString() ? `?${params.toString()}` : ""}`;
            const response = await fetch(url);

            if (!response.ok) {
                throw new Error("Failed to fetch devices");
            }

            return response.json();
        },
    });

    const devicesSchema = useDevicesSchema();

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setAppliedSearch(newSearch);
        setAppliedFilter(newFilter);
    };

    const elements = data?.devices ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="devices"
                items={elements}
                schema={devicesSchema}
                filterMode="server"
                serverSearch={appliedSearch}
                serverFilter={appliedFilter}
                onServerFilterChange={handleFilterChange}
                searchPlaceholder="Search devices..."
            />
        </div>
    );
}
