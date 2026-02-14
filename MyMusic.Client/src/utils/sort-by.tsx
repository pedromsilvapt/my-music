import type {CollectionSort} from "../components/common/collection/collection-schema.tsx";

export function sortBy<T>(fields: CollectionSort<T>): (a: T, b: T) => number {
    return (a, b) => {
        for (const sortField of fields) {
            const aValue = sortField.getValue ? sortField.getValue(a) : a[sortField.field];
            const bValue = sortField.getValue ? sortField.getValue(b) : b[sortField.field];

            if (aValue == null && bValue == null) continue;
            if (aValue == null) return sortField.direction === 'asc' ? -1 : 1;
            if (bValue == null) return sortField.direction === 'asc' ? 1 : -1;

            let comparison = 0;
            if (typeof aValue === 'string' && typeof bValue === 'string') {
                comparison = aValue.localeCompare(bValue);
            } else if (typeof aValue === 'number' && typeof bValue === 'number') {
                comparison = aValue - bValue;
            } else {
                comparison = String(aValue).localeCompare(String(bValue));
            }

            if (comparison !== 0) {
                return sortField.direction === 'asc' ? comparison : -comparison;
            }
        }

        return 0;
    };
}