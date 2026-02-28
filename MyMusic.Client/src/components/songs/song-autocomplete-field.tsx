import {useCallback} from "react";
import {autocompleteSongs} from "../../client/songs.ts";
import AutocompleteField, {type AutocompleteItem} from "./autocomplete-field.tsx";

interface SongAutocompleteFieldProps {
    label?: string;
    placeholder?: string;
    value: AutocompleteItem | null;
    onChange: (value: AutocompleteItem | null) => void;
    disabled?: boolean;
    error?: string;
}

export default function SongAutocompleteField({
                                                  label = "Song",
                                                  placeholder = "Search for a song...",
                                                  value,
                                                  onChange,
                                                  disabled,
                                                  error,
                                              }: SongAutocompleteFieldProps) {
    const handleSearch = useCallback(async (query: string) => {
        if (query.length < 1) return [];
        const response = await autocompleteSongs({search: query, limit: 15});
        return response.data.songs.map(song => ({
            id: song.id,
            name: song.title,
            subtitle: song.albumName ?? undefined,
            coverId: song.coverId,
            artistName: song.artistName ?? undefined,
        }));
    }, []);

    const handleChange = (newValue: AutocompleteItem | string | null) => {
        if (newValue === null || typeof newValue === "string") {
            onChange(null);
        } else {
            onChange(newValue);
        }
    };

    return (
        <AutocompleteField
            label={label}
            placeholder={placeholder}
            value={value}
            onChange={handleChange}
            onSearch={handleSearch}
            disabled={disabled}
            error={error}
            showArtwork={true}
        />
    );
}
