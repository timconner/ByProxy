function prefersLightTheme() {
    return window.matchMedia("(prefers-color-scheme: light)").matches;
}

function setCookie(cookieData) {
    document.cookie = cookieData
}

function startReorder(element, dno) {
    Sortable.create(element, {
        handle: ".drag-handle",
        ghostClass: "drop-target",
        animation: 150,
        onUpdate: (event => {
            // Revert the DOM to match the .NET state
            event.item.remove();
            event.to.insertBefore(event.item, event.to.childNodes[event.oldIndex]);

            dno.invokeMethodAsync('SortableJsUpdate', event.oldDraggableIndex, event.newDraggableIndex);
        })
    });
}

// ** Monaco Editor ** //
var monacoLoading = false;
var monacoEditor;

function isMonacoLoaded() {
    return !!window.monaco;
}
function loadMonaco() {
    if (monacoLoading) return;
    monacoLoading = true;
    
    const script = document.createElement('script');
    script.src = 'monaco/vs/editor/editor.main.js';
    document.head.appendChild(script);
}

function createMonacoDiff(editorElement, currentConfig, candidateConfig, darkTheme) {
    var currentModel = monaco.editor.createModel(currentConfig, 'json');
    var candidateModel = monaco.editor.createModel(candidateConfig, 'json');

    const theme = darkTheme ? 'vs-dark' : 'vs'

    monacoEditor = monaco.editor.createDiffEditor(editorElement, {
        automaticLayout: true,
        readOnly: true,
        theme: theme
    });

    monacoEditor.setModel({
        original: currentModel,
        modified: candidateModel
    });
}

function createMonacoEditor(editorElement, language, code, darkTheme) {
    const theme = darkTheme ? 'vs-dark' : 'vs'

    monacoEditor = monaco.editor.create(editorElement, {
        value: code,
        language: language,
        theme: theme
    });
}

var monacoContentBuffer;
function startMonacoGetContent() {
    if (monacoEditor) {
        monacoContentBuffer = monacoEditor.getValue();
        return monacoContentBuffer.length;
    }
    return null;
}

function getMonacoContentBuffer(startIndex, capacity) {
    if (startIndex < 0) startIndex = 0;
    let endIndex = startIndex + capacity;
    if (endIndex > monacoContentBuffer.length) endIndex = monacoContentBuffer.length
    return monacoContentBuffer.substring(startIndex, endIndex);
}

function endMonacoGetContent() {
    monacoEditorValueBuffer = null;
}

function resizeMonacoEditor() {
    if (monacoEditor) monacoEditor.layout();
}

function disposeMonacoEditor() {
    if (monacoEditor) monacoEditor.dispose();
}

window.addEventListener('resize', resizeMonacoEditor);