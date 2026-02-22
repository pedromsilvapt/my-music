import Editor, {type Monaco, type OnMount} from "@monaco-editor/react";
import {useEffect, useRef} from "react";
import type {FilterFieldMetadata, FilterMetadataResponse} from "./use-filter-metadata.ts";

interface FilterCodeEditorProps {
    value: string;
    onChange: (value: string) => void;
    onApply: () => void;
    height?: number;
    metadata?: FilterMetadataResponse;
    fetchFilterValues?: (field: string, searchTerm: string) => Promise<string[]>;
}

interface CompletionItem {
    label: string;
    kind: number;
    insertText: string;
    documentation?: string;
    range: unknown;
    detail?: string;
    insertTextRules?: number;
    filterText?: string;
}

interface EditorContext {
    metadata?: FilterMetadataResponse;
    fetchFilterValues?: (field: string, searchTerm: string) => Promise<string[]>;
    onApply?: () => void;
}

const editorContexts = new Map<string, EditorContext>();
let isProviderRegistered = false;

const isAfterOperator = (text: string): boolean => {
    const trimmed = text.trimEnd();
    return /[=<>!~]|\b(?:contains|startsWith|endsWith|in|between|isNull|isNotNull|isTrue|isFalse)\s*$/i.test(trimmed);
};

const isAfterField = (text: string): boolean => {
    const trimmed = text.trimEnd();
    if (/\b(?:and|or)\s*$/i.test(trimmed)) return false;
    return /[a-zA-Z._]+$/.test(trimmed) && !isAfterOperator(trimmed);
};

const extractFieldName = (text: string): string | null => {
    const match = text.match(/([a-zA-Z._[\]]+)\s*(?:=|!=|>|>=|<|<=|~|contains|startsWith|endsWith|in|between)/);
    return match ? match[1] : null;
};

const extractStringContext = (textBeforeCursor: string): { field: string; partialValue: string } | null => {
    const match = textBeforeCursor.match(/([a-zA-Z._[\]]+)\s*(?:=|!=|>|>=|<|<=|~|contains|startsWith|endsWith)\s*"([^"]*)$/);
    if (match) {
        return {field: match[1], partialValue: match[2]};
    }
    return null;
};

const getFieldCompletions = (range: unknown, fields: FilterFieldMetadata[]): CompletionItem[] => {
    return fields.map((field) => ({
        label: field.name,
        kind: 10,
        insertText: field.name,
        documentation: `${field.description}${field.isComputed ? " (computed)" : ""}`,
        range,
        detail: field.type,
    }));
};

const getOperatorCompletions = (range: unknown): CompletionItem[] => {
    const operators = [
        {label: "=", insertText: "=", doc: "Equals"},
        {label: "!=", insertText: "!=", doc: "Not equals"},
        {label: ">", insertText: ">", doc: "Greater than"},
        {label: ">=", insertText: ">=", doc: "Greater than or equal"},
        {label: "<", insertText: "<", doc: "Less than"},
        {label: "<=", insertText: "<=", doc: "Less than or equal"},
        {label: "~", insertText: "~", doc: "Contains (string)"},
        {label: "contains", insertText: "contains", doc: "Contains (string)"},
        {label: "startsWith", insertText: "startsWith", doc: "Starts with"},
        {label: "endsWith", insertText: "endsWith", doc: "Ends with"},
        {label: "in", insertText: "in [$1]", doc: "In list"},
        {label: "between", insertText: "between $1 and $2", doc: "Between two values"},
        {label: "isNull", insertText: "isNull", doc: "Is null"},
        {label: "isNotNull", insertText: "isNotNull", doc: "Is not null"},
        {label: "isTrue", insertText: "isTrue", doc: "Is true"},
        {label: "isFalse", insertText: "isFalse", doc: "Is false"},
    ];

    return operators.map((op) => ({
        label: op.label,
        kind: 14,
        insertText: op.insertText,
        documentation: op.doc,
        range,
        insertTextRules: 4,
    }));
};

const getValueCompletions = (range: unknown, fields: FilterFieldMetadata[], textBefore: string): CompletionItem[] => {
    const fieldName = extractFieldName(textBefore);
    if (!fieldName) return [];

    const field = fields.find((f) => f.name === fieldName);
    if (!field) return [];

    const suggestions: CompletionItem[] = [];

    if (field.type === "boolean") {
        suggestions.push(
            {label: "true", kind: 12, insertText: "true", range},
            {label: "false", kind: 12, insertText: "false", range}
        );
    }

    if (field.values) {
        suggestions.push(
            ...field.values.map((v) => ({
                label: v,
                kind: 12,
                insertText: `"${v}"`,
                range,
            }))
        );
    }

    return suggestions;
};

const getDynamicValueCompletions = async (
    range: unknown,
    field: string,
    partialValue: string,
    fields: FilterFieldMetadata[],
    fetchFn: (field: string, searchTerm: string) => Promise<string[]>,
    hasClosingQuote: boolean
): Promise<CompletionItem[]> => {
    const fieldMeta = fields.find(f => f.name === field);
    if (!fieldMeta) return [];

    const getInsertText = (v: string) => hasClosingQuote ? v : `${v}"`;

    if (fieldMeta.values) {
        const lowerPartial = partialValue.toLowerCase();
        return fieldMeta.values
            .filter(v => v.toLowerCase().includes(lowerPartial))
            .map(v => ({
                label: v,
                kind: 12,
                insertText: getInsertText(v),
                range,
                filterText: partialValue,
            }));
    }

    if (!fieldMeta.supportsDynamicValues || fieldMeta.type !== "string") {
        return [];
    }

    try {
        const values = await fetchFn(field, partialValue);
        return values.map(v => ({
            label: v,
            kind: 12,
            insertText: getInsertText(v),
            range,
            filterText: partialValue,
        }));
    } catch {
        return [];
    }
};

const getKeywordCompletions = (range: unknown): CompletionItem[] => {
    return [
        {label: "and", kind: 14, insertText: "and", documentation: "Logical AND", range},
        {label: "or", kind: 14, insertText: "or", documentation: "Logical OR", range},
        {
            label: "group",
            kind: 15,
            insertText: "($1)",
            documentation: "Group expressions",
            range,
            insertTextRules: 4,
        },
    ];
};

const getQuantifierCompletions = (range: unknown): CompletionItem[] => {
    return [
        {
            label: "any",
            kind: 14,
            insertText: "any].",
            documentation: "At least one element matches (default for positive operators)",
            range,
        },
        {
            label: "all",
            kind: 14,
            insertText: "all].",
            documentation: "All elements must match (default for negative operators)",
            range,
        },
    ];
};

function ensureProviderRegistered(monaco: Monaco) {
    if (isProviderRegistered) return;

    monaco.languages.register({id: "filter-dsl"});

    monaco.languages.setMonarchTokensProvider("filter-dsl", {
        keywords: ["and", "or", "in", "between", "isNull", "isNotNull", "isTrue", "isFalse", "any", "all"],
        operators: ["=", "!=", ">", ">=", "<", "<=", "~", "contains", "startsWith", "endsWith"],
        symbols: /[=><!~]+/,
        tokenizer: {
            root: [
                [/"([^"\\]|\\.)*$/, "string.invalid"],
                [/"/, "string", "@string"],
                [/\d+/, "number"],
                [/[a-zA-Z_][\w.]*/, "identifier"],
                [/[{}()[\]]/, "@brackets"],
                [/[;,.]/, "delimiter"],
            ],
            string: [
                [/[^\\"]+/, "string"],
                [/\\./, "string.escape"],
                [/"/, "string", "@pop"],
            ],
        },
    });

    monaco.editor.defineTheme("filter-dsl-theme", {
        base: "vs",
        inherit: true,
        rules: [
            {token: "keyword", foreground: "0000FF"},
            {token: "string", foreground: "A31515"},
            {token: "number", foreground: "098658"},
            {token: "identifier", foreground: "001080"},
        ],
        colors: {},
    });

    monaco.languages.registerCompletionItemProvider("filter-dsl", {
        triggerCharacters: ['"', ' '],
        provideCompletionItems: async (model, position) => {
            const context = editorContexts.get(model.uri.toString());
            if (!context?.metadata) return {suggestions: []};

            const fields = context.metadata.fields;
            const word = model.getWordUntilPosition(position);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endColumn: word.endColumn,
            };

            const lineContent = model.getLineContent(position.lineNumber);
            const textBeforeWord = lineContent.substring(0, word.startColumn - 1);
            const textBeforeCursor = lineContent.substring(0, position.column - 1);

            const suggestions: CompletionItem[] = [];

            const stringContext = extractStringContext(textBeforeCursor);
            if (stringContext) {
                const fetchFn = context.fetchFilterValues;
                if (fetchFn) {
                    const wordInString = stringContext.partialValue;
                    const stringRange = {
                        startLineNumber: position.lineNumber,
                        endLineNumber: position.lineNumber,
                        startColumn: position.column - wordInString.length,
                        endColumn: position.column,
                    };
                    const textAfterCursor = lineContent.substring(position.column - 1);
                    const hasClosingQuote = textAfterCursor.startsWith('"');
                    const dynamicSuggestions = await getDynamicValueCompletions(
                        stringRange,
                        stringContext.field,
                        stringContext.partialValue,
                        fields,
                        fetchFn,
                        hasClosingQuote
                    );
                    suggestions.push(...dynamicSuggestions);
                }
                return {suggestions};
            }

            const isAfterQuantifierBracket = /\[\s*$/i.test(textBeforeCursor);
            if (isAfterQuantifierBracket) {
                suggestions.push(...getQuantifierCompletions(range));
                return {suggestions};
            }

            const isAfterLogicalOperator = /\b(?:and|or)\s*$/i.test(textBeforeWord);
            const isAfterClosingValue = /"\s*$/i.test(textBeforeWord);

            if (isAfterLogicalOperator) {
                suggestions.push(
                    ...getFieldCompletions(range, fields),
                    ...getKeywordCompletions(range)
                );
            } else if (isAfterClosingValue) {
                suggestions.push(...getKeywordCompletions(range));
            } else if (isAfterField(textBeforeWord)) {
                suggestions.push(...getOperatorCompletions(range));
            } else if (isAfterOperator(textBeforeWord)) {
                suggestions.push(...getValueCompletions(range, fields, textBeforeCursor));
            } else {
                suggestions.push(
                    ...getFieldCompletions(range, fields),
                    ...getKeywordCompletions(range)
                );
            }

            return {suggestions};
        },
    });

    isProviderRegistered = true;
}

export function FilterCodeEditor({
                                     value,
                                     onChange,
                                     onApply,
                                     height = 120,
                                     metadata,
                                     fetchFilterValues
                                 }: FilterCodeEditorProps) {
    const editorRef = useRef<unknown>(null);
    const modelUriRef = useRef<string | null>(null);

    useEffect(() => {
        if (modelUriRef.current) {
            const context = editorContexts.get(modelUriRef.current);
            if (context) {
                context.onApply = onApply;
            }
        }
    }, [onApply]);

    useEffect(() => {
        if (modelUriRef.current) {
            const context = editorContexts.get(modelUriRef.current);
            if (context) {
                context.metadata = metadata;
            }
        }
    }, [metadata]);

    useEffect(() => {
        if (modelUriRef.current) {
            const context = editorContexts.get(modelUriRef.current);
            if (context) {
                context.fetchFilterValues = fetchFilterValues;
            }
        }
    }, [fetchFilterValues]);

    useEffect(() => {
        return () => {
            if (modelUriRef.current) {
                editorContexts.delete(modelUriRef.current);
            }
        };
    }, []);

    const handleEditorMount: OnMount = (editor, monaco: Monaco) => {
        editorRef.current = editor;

        ensureProviderRegistered(monaco);

        const model = editor.getModel();
        if (model) {
            const uri = model.uri.toString();
            modelUriRef.current = uri;
            editorContexts.set(uri, {
                metadata,
                fetchFilterValues,
                onApply,
            });
        }

        editor.onDidChangeModelContent(() => {
            const position = editor.getPosition();
            if (!position) return;

            const model = editor.getModel();
            if (!model) return;

            const lineContent = model.getLineContent(position.lineNumber);
            const textBeforeCursor = lineContent.substring(0, position.column - 1);

            if (extractStringContext(textBeforeCursor)) {
                editor.trigger('keyboard', 'hideSuggestWidget', {});
                editor.trigger('keyboard', 'editor.action.triggerSuggest', {});
            }
        });

        editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, () => {
            if (modelUriRef.current) {
                const context = editorContexts.get(modelUriRef.current);
                context?.onApply?.();
            }
        });
    };

    const handleChange = (value: string | undefined) => {
        onChange(value || "");
    };

    return (
        <Editor
            height={height}
            language="filter-dsl"
            value={value}
            onChange={handleChange}
            onMount={handleEditorMount}
            theme="filter-dsl-theme"
            options={{
                minimap: {enabled: false},
                lineNumbers: "off",
                glyphMargin: false,
                folding: false,
                lineDecorationsWidth: 0,
                lineNumbersMinChars: 0,
                renderLineHighlight: "none",
                scrollBeyondLastLine: false,
                wordWrap: "on",
                fontSize: 13,
                fontFamily: "JetBrains Mono, Consolas, monospace",
                padding: {top: 8, bottom: 8},
                suggestOnTriggerCharacters: true,
                quickSuggestions: true,
                tabCompletion: "on",
            }}
        />
    );
}
