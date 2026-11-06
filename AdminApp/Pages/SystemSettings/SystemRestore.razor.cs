namespace ByProxy.AdminApp.Pages.SystemSettings {
    public partial class SystemRestore {
        private ExportOptions _areasToImport = ExportOptions.ImportAll;
        private ImportMergeOptions _mergeOptions = ImportMergeOptions.MergeNone;
        private bool _isEncrypted = false;
        private string _password = string.Empty;
        private IBrowserFile? _selectedFile;
        private ProxyExport? _export = null;
        private ProxyConfig? _preview = null;
        private MonacoEditor? _editor = null;
        private bool _viewCandidateConfig = false;
        private bool _resizeEditor = false;

        private const string EDITOR_LANGUAGE = "json";

        [MemberNotNullWhen(true, "_selectedFile")]
        private bool IsReady() {
            return _selectedFile != null && (!_isEncrypted || !string.IsNullOrEmpty(_password));
        }

        protected override async Task OnInitializedAsync() {
            if (_config.ChangesPending) {
                var result = await _modal.YesNo(_strings["Changes Pending"], _strings["WARN_RestoreChangesPending"]);
                if (result != DialogResult.Yes) _nav.NavigateTo("/system");
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender) {
            if (_resizeEditor) {
                _resizeEditor = false;
                await _js.InvokeVoidAsync("resizeMonacoEditor");
            }
        }

        private void Reset() {
            _areasToImport = ExportOptions.ImportAll;
            _mergeOptions = ImportMergeOptions.MergeNone;
            _viewCandidateConfig = false;
            _resizeEditor = false;
            _isEncrypted = false;
            _password = string.Empty;
            _selectedFile = null;
            _export = null;
            _preview = null;
        }

        private void FileSelected(InputFileChangeEventArgs e) {
            _selectedFile = e.File;
            if (_selectedFile.Name.EndsWith(".encrypted") || _selectedFile.Name.EndsWith(".enc")) _isEncrypted = true;
        }

        private async Task BeginImport() {
            if (!IsReady()) return;

            var success = await _modal.RunWithProgressViewer(_strings["Reading File"], async (progress) => {
                progress.Report((_strings["Uploading File"], null));
                byte[] fileBytes = new byte[_selectedFile.Size];
                await using var stream = _selectedFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
                await stream.ReadExactlyAsync(fileBytes);

                if (_isEncrypted) {
                    try {
                        progress.Report((_strings["Decrypting File"], null));
                        _export = _importExport.DecryptExport(fileBytes, _password);
                    } catch (AuthenticationTagMismatchException) {
                        throw new CryptographicException(_strings["ERR_ExportDecryptionFailed"]);
                    }
                } else {
                    progress.Report((_strings["Deserializing File"], null));
                    _export = JsonSerializer.Deserialize<ProxyExport>(fileBytes) ?? throw new InvalidDataException("Invalid file contents.");
                }
                _preview = await _importExport.RealizeExportAsync(_export, _areasToImport, _mergeOptions);
            }, true);
            if (!success || _preview == null) return;

            await InvokeAsync(StateHasChanged);
            if (_editor != null) await _editor.SetContent(_preview.GenerateComparableJson(false), EDITOR_LANGUAGE);
        }

        private async Task ToggleArea(ExportOptions option) {
            _areasToImport ^= option;

            if (!_areasToImport.HasFlag(ExportOptions.IncludeSystemSettings)) {
                _areasToImport &= ~ExportOptions.IncludeSystemCertificates;
            }

            await UpdatePreview();
        }

        private async Task ToggleMergeOption(ImportMergeOptions option) {
            _mergeOptions ^= option;
            await UpdatePreview();
        }

        private async Task UpdatePreview() {
            if (_export == null || _editor == null) return;
            _preview = await _importExport.RealizeExportAsync(_export, _areasToImport, _mergeOptions);
            await _editor.SetContent(_preview.GenerateComparableJson(false), EDITOR_LANGUAGE);
            ChangeSummaryView(true);
        }

        private void ChangeSummaryView(bool viewCandidateConfig) {
            _viewCandidateConfig = viewCandidateConfig;
            _resizeEditor = _viewCandidateConfig;
        }

        private async Task ProcessImport() {
            if (_export == null) return;

            _appState.ConfigurationChangeInProgress = true;
            try {
                var success = await _modal.RunWithProgressViewer(_strings["Restore Progress"], async (progress) => {
                    var localizedProgress = new LocalizingProgress(progress, _strings);
                    await _importExport.ProcessImport(_export, _areasToImport, _mergeOptions, localizedProgress);
                    progress.Report((_strings["Restore Finished Successfully"], null));
                });
                if (success) _nav.NavigateTo("/");
            } finally {
                _appState.ConfigurationChangeInProgress = false;
            }
        }
    }
}
