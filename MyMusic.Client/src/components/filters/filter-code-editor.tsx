import Editor, {type Monaco, type OnMount} from "@monaco-editor/react";
import {useEffect, useRef} from "react";
import {i18n} from "../../locales";
import {
    extractFieldName,
    extractListContext,
    extractScopePrefix,
    extractStringContext,
    isAfterField,
    isAfterOperator,
    qualifyField
} from "./filter-completion-context.ts";
import type {FilterFieldMetadata, FilterMetadataResponse} from "./use-filter-metadata.ts";

interface FilterCodeEditorProps {
    value: string;
    onChange: (value: string) => void;
    onApply: () => void;
    height?: number;
    metadata?: FilterMetadataResponse;
    fetchFilterValues?: (field: string, searchTerm: string) => Promise<string[]>;
    /** Whether scopes (`device(name = "a" and copies > 1)`) can be used: only filters evaluated by the server do. */
    scopes?: boolean;
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
    scopes?: boolean;
}

const editorContexts = new Map<string, EditorContext>();
let isProviderRegistered = false;

const getFieldCompletions = (
    range: unknown,
    fields: FilterFieldMetadata[],
    scopePrefix: string | null = null,
    scopes = false
): CompletionItem[] => {
    // Inside a scope only its fields make sense, named relative to it ("device.name" is "name" in "device(")
    const scopedFields = scopePrefix === null
        ? fields.map(field => ({field, name: field.name}))
        : fields
            .filter(field => field.name.startsWith(`${scopePrefix}.`))
            .map(field => ({field, name: field.name.substring(scopePrefix.length + 1)}));

    const fieldCompletions: CompletionItem[] = scopedFields.map(({field, name}) => ({
        label: name,
        kind: 10,
        insertText: name,
        documentation: `${field.description}${field.isComputed ? ` ${i18n.t("filters:codeEditor.computed")}` : ""}`,
        range,
        detail: field.type,
    }));

    if (!scopes) return fieldCompletions;

    const scopeNames = new Set(
        scopedFields
            .filter(({field, name}) => field.isCollection && name.includes("."))
            // The scope is the collection the field belongs to: "song.artist" for "song.artist.name"
            .map(({name}) => name.substring(0, name.lastIndexOf(".")))
    );
    const scopeCompletions: CompletionItem[] = [...scopeNames].map(name => ({
        label: `${name}(…)`,
        kind: 15,
        insertText: `${name}($1)`,
        documentation: i18n.t("filters:codeEditor.keywords.scope", {name}),
        range,
        insertTextRules: 4,
        filterText: name,
    }));

    return [...fieldCompletions, ...scopeCompletions];
};

const getOperatorCompletions = (range: unknown): CompletionItem[] => {
    const operators = [
        {label: "=", insertText: "=", doc: i18n.t("filters:codeEditor.operators.equals")},
        {label: "!=", insertText: "!=", doc: i18n.t("filters:codeEditor.operators.notEquals")},
        {label: ">", insertText: ">", doc: i18n.t("filters:codeEditor.operators.greaterThan")},
        {label: ">=", insertText: ">=", doc: i18n.t("filters:codeEditor.operators.greaterThanOrEqual")},
        {label: "<", insertText: "<", doc: i18n.t("filters:codeEditor.operators.lessThan")},
        {label: "<=", insertText: "<=", doc: i18n.t("filters:codeEditor.operators.lessThanOrEqual")},
        {label: "~", insertText: "~", doc: i18n.t("filters:codeEditor.operators.contains")},
        {label: "contains", insertText: "contains", doc: i18n.t("filters:codeEditor.operators.contains")},
        {label: "startsWith", insertText: "startsWith", doc: i18n.t("filters:codeEditor.operators.startsWith")},
        {label: "endsWith", insertText: "endsWith", doc: i18n.t("filters:codeEditor.operators.endsWith")},
        {label: "in", insertText: "in [$1]", doc: i18n.t("filters:codeEditor.operators.inList")},
        {label: "notIn", insertText: "notIn [$1]", doc: i18n.t("filters:codeEditor.operators.notInList")},
        {label: "between", insertText: "between $1 and $2", doc: i18n.t("filters:codeEditor.operators.between")},
        {label: "isNull", insertText: "isNull", doc: i18n.t("filters:codeEditor.operators.isNull")},
        {label: "isNotNull", insertText: "isNotNull", doc: i18n.t("filters:codeEditor.operators.isNotNull")},
        {label: "isTrue", insertText: "isTrue", doc: i18n.t("filters:codeEditor.operators.isTrue")},
        {label: "isFalse", insertText: "isFalse", doc: i18n.t("filters:codeEditor.operators.isFalse")},
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

const getValueCompletions = (range: unknown, fields: FilterFieldMetadata[], fieldName: string | null): CompletionItem[] => {
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
        {label: "and", kind: 14, insertText: "and", documentation: i18n.t("filters:codeEditor.keywords.and"), range},
        {label: "or", kind: 14, insertText: "or", documentation: i18n.t("filters:codeEditor.keywords.or"), range},
        {
            label: "group",
            kind: 15,
            insertText: "($1)",
            documentation: i18n.t("filters:codeEditor.keywords.group"),
            range,
            insertTextRules: 4,
        },
    ];
};

const getQuantifierCompletions = (range: unknown, scopes = false): CompletionItem[] => {
    // A quantified scope has its parenthesis right after the quantifier: "device[all](...)"
    const scopeCompletions: CompletionItem[] = !scopes ? [] : ["any", "all"].map(quantifier => ({
        label: `${quantifier}](…)`,
        kind: 15,
        insertText: `${quantifier}]($1)`,
        documentation: i18n.t(`filters:codeEditor.quantifiers.${quantifier}`),
        range,
        insertTextRules: 4,
        filterText: quantifier,
    }));

    return [
        ...scopeCompletions,
        {
            label: "any",
            kind: 14,
            insertText: "any].",
            documentation: i18n.t("filters:codeEditor.quantifiers.any"),
            range,
        },
        {
            label: "all",
            kind: 14,
            insertText: "all].",
            documentation: i18n.t("filters:codeEditor.quantifiers.all"),
            range,
        },
    ];
};

function ensureProviderRegistered(monaco: Monaco) {
    if (isProviderRegistered) return;

    monaco.languages.register({id: "filter-dsl"});

    monaco.languages.setMonarchTokensProvider("filter-dsl", {
        keywords: ["and", "or", "in", "notIn", "between", "isNull", "isNotNull", "isTrue", "isFalse", "any", "all"],
        operators: ["=", "!=", ">", ">=", "<", "<=", "~", "contains", "startsWith", "endsWith", "notIn"],
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
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        provideCompletionItems: async (model: any, position: any) => {
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

            // A scope can be opened on a previous line, so look at everything typed before the cursor
            const scopePrefix = context.scopes
                ? extractScopePrefix(model.getValueInRange({
                    startLineNumber: 1,
                    startColumn: 1,
                    endLineNumber: position.lineNumber,
                    endColumn: position.column,
                }))
                : null;
            const scopes = context.scopes ?? false;

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
                        qualifyField(scopePrefix, stringContext.field),
                        stringContext.partialValue,
                        fields,
                        fetchFn,
                        hasClosingQuote
                    );
                    suggestions.push(...dynamicSuggestions);
                }
                return {suggestions};
            }

            const listContext = extractListContext(textBeforeCursor);
            if (listContext) {
                suggestions.push(...getValueCompletions(range, fields, qualifyField(scopePrefix, listContext.field)));
                return {suggestions};
            }

            const isAfterQuantifierBracket = /\[\s*$/i.test(textBeforeCursor);
            if (isAfterQuantifierBracket) {
                suggestions.push(...getQuantifierCompletions(range, scopes));
                return {suggestions};
            }

            const isAfterLogicalOperator = /\b(?:and|or)\s*$/i.test(textBeforeWord);
            const isAfterClosingValue = /"\s*$/i.test(textBeforeWord);
            const isAfterOpeningParenthesis = /\(\s*$/.test(textBeforeWord);

            if (isAfterLogicalOperator || isAfterOpeningParenthesis) {
                suggestions.push(
                    ...getFieldCompletions(range, fields, scopePrefix, scopes),
                    ...getKeywordCompletions(range)
                );
            } else if (isAfterClosingValue) {
                suggestions.push(...getKeywordCompletions(range));
            } else if (isAfterField(textBeforeWord)) {
                suggestions.push(...getOperatorCompletions(range));
            } else if (isAfterOperator(textBeforeWord)) {
                const fieldName = extractFieldName(textBeforeWord);
                suggestions.push(
                    ...getValueCompletions(range, fields, fieldName && qualifyField(scopePrefix, fieldName))
                );
            } else {
                suggestions.push(
                    ...getFieldCompletions(range, fields, scopePrefix, scopes),
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
                                     fetchFilterValues,
                                     scopes
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
        if (modelUriRef.current) {
            const context = editorContexts.get(modelUriRef.current);
            if (context) {
                context.scopes = scopes;
            }
        }
    }, [scopes]);

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
                scopes,
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
            wrapperProps={{"data-testid": "filter-code-editor"}}
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
