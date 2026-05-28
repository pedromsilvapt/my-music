import type { CollectionSchema } from "./collection-schema";

export interface FilterToken {
    type: 'field' | 'operator' | 'value' | 'combinator' | 'bracket' | 'comma';
    value: string;
}

const KEYWORD_OPERATORS = new Set([
    'contains', 'startswith', 'endswith',
    'in', 'notin',
    'between',
    'isnull', 'isnotnull',
    'istrue', 'isfalse',
]);

const UNARY_OPERATORS = new Set(['isnull', 'isnotnull', 'istrue', 'isfalse']);

export function tokenizeFilter(dsl: string): FilterToken[] {
    const tokens: FilterToken[] = [];
    const regex = /([a-zA-Z_]\w*(?:\.\w+)*)|("[^"]*")|(\d+(?:\.\d+)?)|(and|or)|([=<>!~]+)|([\[\]])|(,)/gi;
    let match: RegExpExecArray | null;

    match = regex.exec(dsl);
    while (match !== null) {
        if (match[1]) {
            const word = match[1];
            const lower = word.toLowerCase();
            if (lower === 'and' || lower === 'or') {
                tokens.push({ type: 'combinator', value: lower });
            } else if (KEYWORD_OPERATORS.has(lower)) {
                tokens.push({ type: 'operator', value: lower });
            } else {
                tokens.push({ type: 'field', value: word });
            }
        } else if (match[2]) {
            tokens.push({ type: 'value', value: match[2].slice(1, -1) });
        } else if (match[3]) {
            tokens.push({ type: 'value', value: match[3] });
        } else if (match[4]) {
            tokens.push({ type: 'combinator', value: match[4].toLowerCase() });
        } else if (match[5]) {
            tokens.push({ type: 'operator', value: match[5] });
        } else if (match[6]) {
            tokens.push({ type: 'bracket', value: match[6] });
        } else if (match[7]) {
            tokens.push({ type: 'comma', value: ',' });
        }
        match = regex.exec(dsl);
    }

    return tokens;
}

export function evaluateTokens<T>(item: T, tokens: FilterToken[], schema: CollectionSchema<T>): boolean {
    if (tokens.length === 0) return true;

    let result = true;
    let currentCombinator: 'and' | 'or' = 'and';
    let i = 0;

    const isUnary = (op: string): boolean => UNARY_OPERATORS.has(op.toLowerCase());

    while (i < tokens.length) {
        if (tokens[i].type === 'combinator') {
            currentCombinator = tokens[i].value as 'and' | 'or';
            i++;
            continue;
        }

        const fieldToken = tokens[i];
        if (fieldToken.type !== 'field') {
            i++;
            continue;
        }
        i++;

        if (i >= tokens.length) break;
        const operatorToken = tokens[i];
        if (operatorToken.type !== 'operator') continue;
        const op = operatorToken.value.toLowerCase();
        i++;

        if (isUnary(op)) {
            const fieldValue = getFieldValue(item, fieldToken.value, schema);
            const conditionResult = evaluateCondition(fieldValue, op, '');
            result = currentCombinator === 'and' ? result && conditionResult : result || conditionResult;
            continue;
        }

        if (op === 'between') {
            const { values: betweenValues, consumed } = collectBetweenValues(tokens, i);
            i += consumed;

            if (betweenValues.length === 2) {
                const fieldValue = getFieldValue(item, fieldToken.value, schema);
                const conditionResult = evaluateBetweenCondition(fieldValue, betweenValues[0], betweenValues[1]);
                result = currentCombinator === 'and' ? result && conditionResult : result || conditionResult;
            }
            continue;
        }

        if (op === 'in' || op === 'notin') {
            const { values: listValues, consumed } = collectListValues(tokens, i);
            i += consumed;

            const fieldValue = getFieldValue(item, fieldToken.value, schema);
            const conditionResult = evaluateCondition(fieldValue, op, '', listValues);
            result = currentCombinator === 'and' ? result && conditionResult : result || conditionResult;
            continue;
        }

        if (i >= tokens.length) break;
        const valueToken = tokens[i];
        if (valueToken.type !== 'value' && valueToken.type !== 'field') break;
        i++;

        const fieldValue = getFieldValue(item, fieldToken.value, schema);
        const conditionResult = evaluateCondition(fieldValue, op, valueToken.value);

        result = currentCombinator === 'and' ? result && conditionResult : result || conditionResult;
    }

    return result;
}

function collectBetweenValues(tokens: FilterToken[], startIndex: number): { values: string[]; consumed: number } {
    const values: string[] = [];
    let i = startIndex;

    if (i < tokens.length && tokens[i].type === 'value') {
        values.push(tokens[i].value);
        i++;
    }

    if (i < tokens.length && tokens[i].type === 'combinator' && tokens[i].value === 'and') {
        i++;
    }

    if (i < tokens.length && tokens[i].type === 'value') {
        values.push(tokens[i].value);
        i++;
    }

    return { values, consumed: i - startIndex };
}

function collectListValues(tokens: FilterToken[], startIndex: number): { values: string[]; consumed: number } {
    const values: string[] = [];
    let i = startIndex;
    let hasBracket = false;

    if (i < tokens.length && tokens[i].type === 'bracket' && tokens[i].value === '[') {
        hasBracket = true;
        i++;
    }

    while (i < tokens.length) {
        if (tokens[i].type === 'bracket' && tokens[i].value === ']') {
            i++;
            break;
        }
        if (tokens[i].type === 'value') {
            values.push(tokens[i].value);
            i++;
            if (i < tokens.length && tokens[i].type === 'comma') {
                i++;
            }
        } else if (tokens[i].type === 'comma') {
            i++;
        } else {
            break;
        }
    }

    if (!hasBracket && values.length === 0 && i < tokens.length && tokens[i].type === 'value') {
        values.push(tokens[i].value);
        i++;
    }

    return { values, consumed: i - startIndex };
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
    
    const valuePath = fieldMetadata?.clientPath ?? fieldPath;
    const valueParts = valuePath.split('.');
    
    if (fieldMetadata?.isCollection && parts.length > 1) {
        const pluralName = getSingularToPluralMapping(valueParts[0] ?? firstPart);
        const collection = (item as Record<string, unknown>)[pluralName];
        
        if (!Array.isArray(collection)) {
            return undefined;
        }
        
        const values: unknown[] = [];
        const nestedParts = parts.slice(1);
        
        for (const element of collection) {
            if (element && typeof element === 'object') {
                let nestedValue: unknown = element;
                for (const part of nestedParts) {
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
    for (const part of valueParts) {
        if (value && typeof value === 'object') {
            value = (value as Record<string, unknown>)[part];
        } else {
            return undefined;
        }
    }

    return value;
}

function evaluateCondition(fieldValue: unknown, operator: string, compareValue: string, listValues?: string[]): boolean {
    if (Array.isArray(fieldValue)) {
        const positiveOperators = ['=', '==', '~', 'contains', 'startswith', 'endswith', 'in'];
        const isPositiveOperator = positiveOperators.includes(operator.toLowerCase());
        
        if (isPositiveOperator) {
            return fieldValue.some(v => evaluateSingleCondition(v, operator, compareValue, listValues));
        } else {
            return fieldValue.every(v => evaluateSingleCondition(v, operator, compareValue, listValues));
        }
    }
    
    return evaluateSingleCondition(fieldValue, operator, compareValue, listValues);
}

function evaluateSingleCondition(fieldValue: unknown, operator: string, compareValue: string, listValues?: string[]): boolean {
    const op = operator.toLowerCase();
    const compareNum = parseFloat(compareValue);
    const compareStr = compareValue.toLowerCase();
    const fieldStr = String(fieldValue ?? '').toLowerCase();

    switch (op) {
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
        case 'startswith':
            return fieldStr.startsWith(compareStr);
        case 'endswith':
            return fieldStr.endsWith(compareStr);
        case 'in':
            return evaluateInCondition(fieldStr, compareValue, listValues);
        case 'notin':
            return !evaluateInCondition(fieldStr, compareValue, listValues);
        case 'isnull':
            return fieldValue === null || fieldValue === undefined;
        case 'isnotnull':
            return fieldValue !== null && fieldValue !== undefined;
        case 'istrue':
            return evaluateTruthy(fieldValue, true);
        case 'isfalse':
            return evaluateTruthy(fieldValue, false);
        default:
            return true;
    }
}

function evaluateInCondition(fieldStr: string, compareValue: string, listValues?: string[]): boolean {
    const values = listValues?.map(v => v.toLowerCase()) ?? (compareValue ? compareValue.split(',').map(v => v.trim().toLowerCase()) : []);
    return values.includes(fieldStr);
}

function evaluateBetweenCondition(fieldValue: unknown, lowStr: string, highStr: string): boolean {
    if (typeof fieldValue !== 'number') return false;
    const low = parseFloat(lowStr);
    const high = parseFloat(highStr);
    if (isNaN(low) || isNaN(high)) return false;
    return fieldValue >= low && fieldValue <= high;
}

function evaluateTruthy(fieldValue: unknown, expectedTrue: boolean): boolean {
    const isTruthy = fieldValue !== null && fieldValue !== undefined && fieldValue !== false && fieldValue !== 0 && fieldValue !== '' && fieldValue !== '0' && fieldValue !== 'false';
    return expectedTrue ? isTruthy : !isTruthy;
}