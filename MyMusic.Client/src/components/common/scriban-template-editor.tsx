import {useComputedColorScheme} from "@mantine/core";
import Editor, {type Monaco, type OnMount} from "@monaco-editor/react";
import {useCallback, useEffect, useRef, useState} from "react";

const LANGUAGE_ID = "scriban-template";
const MARKER_OWNER = "scriban-template";

/** A syntax error of the template. Lines and columns are 1-based; the end is exclusive. */
export interface ScribanTemplateError {
    message: string;
    line: number;
    column: number;
    endLine?: number;
    endColumn?: number;
}

/** A variable the template can use, offered as a completion inside `{{ }}` blocks. */
export interface ScribanTemplateVariable {
    name: string;
    description?: string;
}

export interface ScribanTemplateEditorProps {
    value: string;
    onChange: (value: string) => void;
    height?: number;
    /** Shown while the editor is empty. */
    placeholder?: string;
    errors?: ScribanTemplateError[];
    variables?: ScribanTemplateVariable[];
    readOnly?: boolean;
    testId?: string;
}

type EditorInstance = Parameters<OnMount>[0];

// The variables offered by each editor, by the uri of its model: the completion provider is shared
const editorVariables = new Map<string, ScribanTemplateVariable[]>();
let isLanguageRegistered = false;

/** Whether the text ends inside a `{{ }}` block. */
const isInsideCodeBlock = (textBeforeCursor: string) =>
    textBeforeCursor.lastIndexOf("{{") > textBeforeCursor.lastIndexOf("}}");

/** Whether the element is what its `position: fixed` descendants are placed against, instead of the viewport. */
const containsFixedElements = (element: HTMLElement) => {
    const style = getComputedStyle(element);

    return [style.transform, style.perspective, style.filter, style.backdropFilter].some(value => value && value !== "none")
        || /paint|layout|strict|content/.test(style.contain)
        || /transform|perspective|filter/.test(style.willChange);
};

/**
 * Where the widgets the editor shows over the page (suggestions, hovers) can be placed against the viewport:
 * outside of every ancestor that would place them against itself (e.g. a dialog, that keeps a transform after
 * its transition), but as close to the editor as that allows, to stay inside of what the dialog lets be used.
 */
function findOverflowWidgetsParent(element: HTMLElement): HTMLElement {
    let parent = document.body;

    for (let ancestor = element.parentElement; ancestor && ancestor !== document.body; ancestor = ancestor.parentElement) {
        if (containsFixedElements(ancestor) && ancestor.parentElement) {
            parent = ancestor.parentElement;
        }
    }

    return parent;
}

function ensureLanguageRegistered(monaco: Monaco) {
    if (isLanguageRegistered) return;

    monaco.languages.register({id: LANGUAGE_ID});

    monaco.languages.setMonarchTokensProvider(LANGUAGE_ID, {
        keywords: [
            "if", "else", "end", "for", "in", "while", "case", "when", "func", "ret", "capture", "with", "wrap",
            "break", "continue", "readonly", "import", "include", "null", "true", "false", "empty", "this",
        ],
        tokenizer: {
            // Literal text, up to the next code block
            root: [
                [/\{\{[-~]?/, {token: "delimiter.bracket", next: "@code"}],
                [/[^{]+/, ""],
                [/\{/, ""],
            ],
            code: [
                [/[-~]?\}\}/, {token: "delimiter.bracket", next: "@pop"}],
                [/"([^"\\]|\\.)*"/, "string"],
                [/'([^'\\]|\\.)*'/, "string"],
                [/\d+(\.\d+)?/, "number"],
                [/[a-zA-Z_]\w*/, {cases: {"@keywords": "keyword", "@default": "variable"}}],
                [/\?\?|\|\||&&|==|!=|<=|>=|[|+\-*/%<>=!?:.,]/, "operator"],
                [/[()[\]]/, "@brackets"],
                [/\s+/, "white"],
            ],
        },
    });

    monaco.languages.setLanguageConfiguration(LANGUAGE_ID, {
        brackets: [["{{", "}}"], ["(", ")"], ["[", "]"]],
        autoClosingPairs: [
            {open: "{", close: "}"},
            {open: "(", close: ")"},
            {open: "[", close: "]"},
            {open: '"', close: '"'},
        ],
    });

    monaco.languages.registerCompletionItemProvider(LANGUAGE_ID, {
        triggerCharacters: ["{", " ", "."],
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        provideCompletionItems: (model: any, position: any) => {
            const variables = editorVariables.get(model.uri.toString()) ?? [];
            const textBeforeCursor = model.getValueInRange({
                startLineNumber: 1,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column,
            });

            if (!isInsideCodeBlock(textBeforeCursor)) return {suggestions: []};

            // Variables are dotted paths ("album.artist.name"): replace everything typed of the path so far
            const typedPath = /[\w.[\]]*$/.exec(textBeforeCursor)?.[0] ?? "";
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: position.column - typedPath.length,
                endColumn: position.column,
            };

            return {
                suggestions: variables.map(variable => ({
                    label: variable.name,
                    kind: monaco.languages.CompletionItemKind.Variable,
                    insertText: variable.name,
                    filterText: variable.name,
                    documentation: variable.description,
                    range,
                })),
            };
        },
    });

    isLanguageRegistered = true;
}

/**
 * A multiline editor for Scriban templates (literal text with `{{ ... }}` code blocks): syntax highlighting,
 * error markers and completion of the given variables. It knows nothing about what the template is for.
 */
export default function ScribanTemplateEditor({
                                                  value,
                                                  onChange,
                                                  height = 120,
                                                  placeholder,
                                                  errors,
                                                  variables,
                                                  readOnly,
                                                  testId = "scriban-template-editor",
                                              }: ScribanTemplateEditorProps) {
    const colorScheme = useComputedColorScheme("light");
    const monacoRef = useRef<Monaco | null>(null);
    const [editor, setEditor] = useState<EditorInstance | null>(null);
    const [overflowWidgetsNode, setOverflowWidgetsNode] = useState<HTMLDivElement | null>(null);

    const containerRef = useCallback((container: HTMLDivElement | null) => {
        if (!container) return;

        const node = document.createElement("div");
        // The widgets are styled as descendants of an editor
        node.className = "monaco-editor";
        // A dialog can ignore the mouse outside of its content
        node.style.pointerEvents = "auto";
        findOverflowWidgetsParent(container).appendChild(node);
        setOverflowWidgetsNode(node);

        return () => {
            node.remove();
            setOverflowWidgetsNode(null);
        };
    }, []);

    useEffect(() => {
        overflowWidgetsNode?.classList.toggle("vs-dark", colorScheme === "dark");
        overflowWidgetsNode?.classList.toggle("vs", colorScheme !== "dark");
    }, [overflowWidgetsNode, colorScheme]);

    useEffect(() => {
        const uri = editor?.getModel()?.uri.toString();
        if (!uri) return;

        editorVariables.set(uri, variables ?? []);
        return () => {
            editorVariables.delete(uri);
        };
    }, [editor, variables]);

    useEffect(() => {
        const model = editor?.getModel();
        const monaco = monacoRef.current;
        if (!model || !monaco) return;

        monaco.editor.setModelMarkers(model, MARKER_OWNER, (errors ?? []).map(error => ({
            severity: monaco.MarkerSeverity.Error,
            message: error.message,
            startLineNumber: error.line,
            startColumn: error.column,
            endLineNumber: error.endLine ?? error.line,
            endColumn: error.endColumn ?? error.column + 1,
        })));
    }, [editor, errors]);

    // The editor owns its text: Monaco is not given `value` back on each render. A render can be late (the
    // changes of the editor are not flushed one by one), and giving the editor a value it already left behind
    // would undo what was typed since. Only a value that was not typed in the editor is written to it.
    const typedValuesRef = useRef<string[]>([]);
    const isWritingValueRef = useRef(false);

    useEffect(() => {
        if (!editor) return;

        const typedValues = typedValuesRef.current;
        const typedIndex = typedValues.indexOf(value);
        if (typedIndex >= 0) {
            // The value is one the editor reported: the ones before it are outdated
            typedValues.splice(0, typedIndex + 1);
            return;
        }

        typedValues.length = 0;
        const model = editor.getModel();
        if (!model || editor.getValue() === value) return;

        isWritingValueRef.current = true;
        try {
            if (editor.getOption(monacoRef.current!.editor.EditorOption.readOnly)) {
                editor.setValue(value);
            } else {
                // Unlike setValue, keeps the change in the undo history
                editor.executeEdits("", [{range: model.getFullModelRange(), text: value, forceMoveMarkers: true}]);
                editor.pushUndoStop();
            }
        } finally {
            isWritingValueRef.current = false;
        }
    }, [editor, value]);

    const handleChange = (newValue: string | undefined) => {
        if (isWritingValueRef.current) return;

        typedValuesRef.current.push(newValue ?? "");
        onChange(newValue ?? "");
    };

    const handleEditorMount: OnMount = (editor, monaco) => {
        monacoRef.current = monaco;
        setEditor(editor);
    };

    return (
        <div ref={containerRef} style={{minHeight: height}}>
            {overflowWidgetsNode && <Editor
                height={height}
                language={LANGUAGE_ID}
                defaultValue={value}
                onChange={handleChange}
                beforeMount={ensureLanguageRegistered}
                onMount={handleEditorMount}
                theme={colorScheme === "dark" ? "vs-dark" : "vs"}
                wrapperProps={{"data-testid": testId, "data-errors": errors?.length ?? 0}}
                options={{
                    readOnly,
                    placeholder,
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
                    // The suggestions must not be clipped by the dialog the editor is in: they are placed against
                    // the viewport, in a node that no ancestor of the editor moves
                    fixedOverflowWidgets: true,
                    overflowWidgetsDomNode: overflowWidgetsNode,
                }}
            />}
        </div>
    );
}
