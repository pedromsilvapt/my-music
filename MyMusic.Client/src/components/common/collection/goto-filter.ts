import type {CollectionSchema} from "./collection-schema.tsx";

/**
 * Precomputes the lowercased search vector of every item, so filtering on each keystroke is just a substring check.
 */
export function buildGoToSearchVectors<T>(items: T[], schema: Pick<CollectionSchema<T>, 'searchVector'>): string[] {
    return items.map(item => schema.searchVector(item).toLowerCase());
}

/**
 * Filters items for the GoTo modal using the same matching rule as the collection's client search
 * (case-insensitive substring match on `schema.searchVector`). `searchVectors` must come from
 * {@link buildGoToSearchVectors} for the same `items`.
 */
export function filterGoToItems<T>(items: T[], searchVectors: string[], search: string): T[] {
    if (search.trim() === '') {
        return items;
    }

    const searchLowerCase = search.toLowerCase();
    return items.filter((_, index) => searchVectors[index].includes(searchLowerCase));
}
