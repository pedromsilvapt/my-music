const FIELD = String.raw`([a-zA-Z._[\]]+)`;
const QUOTED_VALUE = String.raw`"(?:[^"\\]|\\.)*"`;
const LIST_ITEM = String.raw`(?:${QUOTED_VALUE}|[^\s",[\]]+)`;
// Every operator spelling accepted by the server's FilterDslParser that is followed by a value.
const VALUE_OPERATOR = String.raw`(?:==|=|!=|<>|>=|<=|>|<|~|contains|startsWith|endsWith|in|notIn)`;

const STRING_CONTEXT_REGEX = new RegExp(String.raw`${FIELD}\s*${VALUE_OPERATOR}\s*"([^"]*)$`);
const FIELD_BEFORE_OPERATOR_REGEX = new RegExp(String.raw`${FIELD}\s*(?:${VALUE_OPERATOR}|between)\s*$`);
const LIST_CONTEXT_REGEX = new RegExp(
    String.raw`${FIELD}\s+(?:in|notIn)\s*\[\s*(?:${LIST_ITEM}\s*,\s*)*(?:"([^"]*)|${LIST_ITEM}\s*)?$`,
    "i"
);

export interface StringContext {
    field: string;
    partialValue: string;
}

export interface ListContext {
    field: string;
    /** The text typed so far inside an open string, or `null` when the cursor is between list items. */
    partialValue: string | null;
}

export const isAfterOperator = (text: string): boolean => {
    const trimmed = text.trimEnd();
    return /[=<>!~]|\b(?:contains|startsWith|endsWith|in|notIn|between|isNull|isNotNull|isTrue|isFalse)\s*$/i.test(trimmed);
};

export const isAfterField = (text: string): boolean => {
    const trimmed = text.trimEnd();
    if (/\b(?:and|or)\s*$/i.test(trimmed)) return false;
    return /[a-zA-Z._]+$/.test(trimmed) && !isAfterOperator(trimmed);
};

/**
 * Returns the field of the condition whose operator ends the given text (e.g. `a = "x" and rating in ` → `rating`).
 */
export const extractFieldName = (textEndingInOperator: string): string | null => {
    const match = textEndingInOperator.match(FIELD_BEFORE_OPERATOR_REGEX);
    return match ? match[1] : null;
};

/**
 * Detects a cursor placed inside an unclosed `in [...]` / `notIn [...]` list, either between items or inside
 * the string of the item being typed.
 */
export const extractListContext = (textBeforeCursor: string): ListContext | null => {
    const match = textBeforeCursor.match(LIST_CONTEXT_REGEX);
    if (!match) return null;
    return {field: match[1], partialValue: match[2] ?? null};
};

/**
 * Detects a cursor placed inside an open string value, be it a single value (`field = "ab`) or a list item
 * (`field notIn ["a", "b`).
 */
export const extractStringContext = (textBeforeCursor: string): StringContext | null => {
    const match = textBeforeCursor.match(STRING_CONTEXT_REGEX);
    if (match) {
        return {field: match[1], partialValue: match[2]};
    }

    const listContext = extractListContext(textBeforeCursor);
    if (listContext && listContext.partialValue !== null) {
        return {field: listContext.field, partialValue: listContext.partialValue};
    }
    return null;
};

// Like the server's FilterDslParser: the quantifier is glued to the name, whitespace may precede the parenthesis
const SCOPE_NAME_REGEX = /([a-zA-Z_][\w.]*)(?:\[\s*(?:any|all)\s*\])?\s*$/i;
// Words that can come right before a plain group: combinators, value-less operators and literal values
const NOT_A_SCOPE_NAME_REGEX = /^(?:and|or|isNull|isNotNull|isTrue|isFalse|true|false|null)$/i;

/**
 * Returns the qualified name of the scopes the cursor is inside of (`device(name = "a" and (` → `device`,
 * `songs(devices(` → `songs.devices`), or `null` when it is not inside any. Plain groups do not count.
 */
export const extractScopePrefix = (textBeforeCursor: string): string | null => {
    const openParentheses: (string | null)[] = [];
    let quote: string | null = null;

    for (let i = 0; i < textBeforeCursor.length; i++) {
        const char = textBeforeCursor[i];

        if (quote) {
            if (char === '\\') i++;
            else if (char === quote) quote = null;
        } else if (char === '"' || char === "'") {
            quote = char;
        } else if (char === '(') {
            const name = textBeforeCursor.substring(0, i).match(SCOPE_NAME_REGEX)?.[1] ?? null;
            openParentheses.push(name && !NOT_A_SCOPE_NAME_REGEX.test(name) ? name : null);
        } else if (char === ')') {
            openParentheses.pop();
        }
    }

    const scopes = openParentheses.filter(name => name !== null);
    return scopes.length > 0 ? scopes.join('.') : null;
};

/**
 * Turns a field typed inside a scope into the name it has in the filter metadata (`name` in `device(` → `device.name`).
 */
export const qualifyField = (scopePrefix: string | null, field: string): string =>
    scopePrefix ? `${scopePrefix}.${field}` : field;
