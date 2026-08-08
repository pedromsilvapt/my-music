import {TextInput} from "@mantine/core";
import {useDebouncedValue} from "@mantine/hooks";
import {IconSearch} from "@tabler/icons-react";
import {useEffect, useState} from "react";
import {useTranslation} from "react-i18next";
import {FILTER_DEBOUNCE_MS} from "../../consts.ts";

interface UnifiedSearchProps {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
}

export function UnifiedSearch({value, onChange, placeholder}: UnifiedSearchProps) {
    const {t} = useTranslation(["filters", "common"]);
    const [localValue, setLocalValue] = useState(value);
    const [debouncedValue] = useDebouncedValue(localValue, FILTER_DEBOUNCE_MS);

    useEffect(() => {
        if (debouncedValue !== value) {
            onChange(debouncedValue);
        }
    }, [debouncedValue, onChange, value]);

    return (
        <TextInput
            placeholder={placeholder ?? t("common:songs.searchPlaceholder")}
            value={localValue}
            onChange={(e) => setLocalValue(e.target.value)}
            leftSection={<IconSearch size={16}/>}
            styles={{
                input: {
                    backgroundColor: "var(--mantine-color-gray-0)",
                },
            }}
            w="100%"
            maw={400}
        />
    );
}
