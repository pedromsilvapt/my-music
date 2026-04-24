import type { CollectionSchema } from "./collection-schema";

export interface FilterToken {
    type: 'field' | 'operator' | 'value' | 'combinator';
    value: string;
}

export function tokenizeFilter(dsl: string): FilterToken[] {
    const tokens: FilterToken[] = [];
    const regex = /(\w+(?:\.\w+)*)|("[^"]*")|(\d+)|(and|or)|([=<>!~]+)/gi;
    let match: RegExpExecArray | null;

    match = regex.exec(dsl);
    while (match !== null) {
        if (match[1]) {
            if (['and', 'or'].includes(match[1].toLowerCase())) {
                tokens.push({type: 'combinator', value: match[1].toLowerCase()});
            } else {
                tokens.push({type: 'field', value: match[1]});
            }
        } else if (match[2]) {
            tokens.push({type: 'value', value: match[2].slice(1, -1)});
        } else if (match[3]) {
            tokens.push({type: 'value', value: match[3]});
        } else if (match[4]) {
            tokens.push({type: 'combinator', value: match[4].toLowerCase()});
        } else if (match[5]) {
            tokens.push({type: 'operator', value: match[5]});
        }
        match = regex.exec(dsl);
    }

    return tokens;
}

export function evaluateTokens<T>(item: T, tokens: FilterToken[], schema: CollectionSchema<T>): boolean {
    if (tokens.length === 0) return true;

    let result = true;
    let currentCombinator: 'and' | 'or' = 'and';

    for (let i = 0; i < tokens.length; i += 3) {
        const fieldToken = tokens[i];
        const operatorToken = tokens[i + 1];
        const valueToken = tokens[i + 2];

        if (!fieldToken || !operatorToken || !valueToken) continue;
        if (fieldToken.type === 'combinator') {
            currentCombinator = fieldToken.value as 'and' | 'or';
            i -= 2;
            continue;
        }

        const fieldValue = getFieldValue(item, fieldToken.value, schema);
        const conditionResult = evaluateCondition(fieldValue, operatorToken.value, valueToken.value);

        if (currentCombinator === 'and') {
            result = result && conditionResult;
        } else {
            result = result || conditionResult;
        }

        const nextToken = tokens[i + 3];
        if (nextToken?.type === 'combinator') {
            currentCombinator = nextToken.value as 'and' | 'or';
            i++;
        }
    }

    return result;
}

function getSingularToPluralMapping(field: string): string {
    const mapping: Record<string, string> = {
        'artist': 'artists',
        'genre': 'genres',
        'device': 'devices',
        'playlist': 'playlists',
    };
    return mapping[field] ?? field;
}

function getFieldValue<T>(item: T, fieldPath: string, schema: CollectionSchema<T>): unknown {
    const searchVector = schema.searchVector(item).toLowerCase();

    if (fieldPath.toLowerCase().includes('searchabletext') || fieldPath.toLowerCase() === 'search') {
        return searchVector;
    }

    const parts = fieldPath.split('.');
    
    const firstPart = parts[0];
    const fieldMetadata = schema.filterMetadata?.fields.find(f => f.name === fieldPath);
    
    if (fieldMetadata?.isCollection && parts.length > 1) {
        const pluralName = getSingularToPluralMapping(firstPart);
        const collection = (item as Record<string, unknown>)[pluralName];
        
        if (!Array.isArray(collection)) {
            return undefined;
        }
        
        const values: unknown[] = [];
        
        for (const element of collection) {
            if (element && typeof element === 'object') {
                let nestedValue: unknown = element;
                for (const part of parts.slice(1)) {
                    if (nestedValue && typeof nestedValue === 'object') {
                        nestedValue = (nestedValue as Record<string, unknown>)[part];
                    } else {
                        nestedValue = undefined;
                        break;
                    }
                }
                values.push(nestedValue);
            }
        }
        
        return values;
    }

    let value: unknown = item;
    for (const part of parts) {
        if (value && typeof value === 'object') {
            value = (value as Record<string, unknown>)[part];
        } else {
            return undefined;
        }
    }

    return value;
}

function evaluateCondition(fieldValue: unknown, operator: string, compareValue: string): boolean {
    if (Array.isArray(fieldValue)) {
        const positiveOperators = ['=', '==', '~', 'contains', 'startsWith', 'endsWith'];
        const isPositiveOperator = positiveOperators.includes(operator);
        
        if (isPositiveOperator) {
            return fieldValue.some(v => evaluateSingleCondition(v, operator, compareValue));
        } else {
            return fieldValue.every(v => evaluateSingleCondition(v, operator, compareValue));
        }
    }
    
    return evaluateSingleCondition(fieldValue, operator, compareValue);
}

function evaluateSingleCondition(fieldValue: unknown, operator: string, compareValue: string): boolean {
    const compareNum = parseFloat(compareValue);
    const compareStr = compareValue.toLowerCase();
    const fieldStr = String(fieldValue ?? '').toLowerCase();

    switch (operator) {
        case '=':
        case '==':
            return fieldStr === compareStr || (typeof fieldValue === 'number' && fieldValue === compareNum);
        case '!=':
        case '<>':
            return fieldStr !== compareStr && (typeof fieldValue !== 'number' || fieldValue !== compareNum);
        case '>':
            return typeof fieldValue === 'number' && fieldValue > compareNum;
        case '>=':
            return typeof fieldValue === 'number' && fieldValue >= compareNum;
        case '<':
            return typeof fieldValue === 'number' && fieldValue < compareNum;
        case '<=':
            return typeof fieldValue === 'number' && fieldValue <= compareNum;
        case '~':
        case 'contains':
            return fieldStr.includes(compareStr);
        case 'startsWith':
            return fieldStr.startsWith(compareStr);
        case 'endsWith':
            return fieldStr.endsWith(compareStr);
        default:
            return true;
    }
}
