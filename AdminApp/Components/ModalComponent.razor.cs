namespace ByProxy.AdminApp.Components {
    public partial class ModalComponent : IModalMethods {

        private TaskCompletionSource<DialogResult>? ModalOpen;

        private bool _visible = false;

        private string? _title;
        private string? _prompt;
        private string? _placeholder;
        private string? _error;
        private ElementReference? _input;
        private bool? _success;
        private string _stringValue = string.Empty;
        private bool _readOnly = false;
        private bool _monoSpace = false;

        private ValidationType _validation = ValidationType.NotEmpty;
        private Regex? _validationRegex = null;
        private bool _inputIsValid = true;

        private enum ModalType {
            LargeText,
            ProgressViewer,
            Loading,
            Password,
            PromptOnly,
            RoleSelect,
            SuccessFail,
            SingleLine,
            CommitPrompt,
            CertificateDetails,
            CertificatePicker
        }
        private ModalType _type;

        private ModalControls _controls;
        [Flags]
        public enum ModalControls {
            None = 0,
            OK = 1,
            Cancel = 2,
            Save = 4,
            Discard = 8,
            Yes = 16,
            No = 32,
            Close = 64,
            Pager = 128
        }

        protected override void OnInitialized() {
            _modalService.RegisterModalComponent(this);
        }

        protected override async Task OnAfterRenderAsync(bool firstRender) {
            if (!_visible) return;
            if (_type == ModalType.SingleLine && _input != null) {
                await _input.Value.FocusAsync();
            }
        }

        private async Task<DialogResult> ShowModal() {
            _visible = true;
            ModalOpen = new TaskCompletionSource<DialogResult>();
            await InvokeAsync(StateHasChanged);
            var result = await ModalOpen.Task;
            ModalOpen = null;
            _visible = false;
            _validationRegex = null;
            _inputIsValid = true;
            _title = null;
            _prompt = null;
            await InvokeAsync(StateHasChanged);
            return result;
        }

        private void CheckForExit(KeyboardEventArgs e) {
            if (e.Code == "Escape") ModalOpen?.SetResult(DialogResult.Cancel);
        }

        private async void ValidateInput(ChangeEventArgs e) {
            bool isValid = ValidateInput((string)(e.Value ?? string.Empty));
            if (isValid != _inputIsValid) {
                _inputIsValid = isValid;
                await InvokeAsync(StateHasChanged);
            }
        }

        private bool ValidateInput(string input) {
            switch (_validation) {
                case ValidationType.None:
                    return true;
                case ValidationType.NotEmpty:
                    return !string.IsNullOrWhiteSpace(input);
                case ValidationType.Int:
                    return int.TryParse(input, out _);
                case ValidationType.Long:
                    return long.TryParse(input, out _);
                case ValidationType.Float:
                    return float.TryParse(input, out _);
                case ValidationType.Guid:
                    return Guid.TryParse(input, out _);
                case ValidationType.AlphaNumeric:
                    return Regex.IsMatch(input, @"^\w+$");
                case ValidationType.AlphaOnly:
                    return Regex.IsMatch(input, @"^[a-zA-Z]+$");
                case ValidationType.Uri:
                    return Uri.TryCreate(input, UriKind.Absolute, out _);
                case ValidationType.IP:
                    return IPAddress.TryParse(input, out _);
                case ValidationType.RegEx:
                    return _validationRegex == null ? true : _validationRegex.IsMatch(input);
                default:
                    return false;
            }
        }

        #region Button Handlers
        private void OK() {
            if (_inputIsValid) ModalOpen?.SetResult(DialogResult.OK);
        }

        private void Save() {
            ModalOpen?.SetResult(DialogResult.Save);
        }

        private void Discard() {
            ModalOpen?.SetResult(DialogResult.Discard);
        }

        private void Cancel() {
            ModalOpen?.SetResult(DialogResult.Cancel);
        }

        private void Yes() {
            ModalOpen?.SetResult(DialogResult.Yes);
        }

        private void No() {
            ModalOpen?.SetResult(DialogResult.No);
        }
        #endregion

        private void StartWait(ModalType type, string title) {
            _type = type;
            _controls = ModalControls.None;

            _title = title;
            _prompt = null;
            _success = null;

            _visible = true;
            InvokeAsync(StateHasChanged);
        }

        private async Task EndWait(bool success, string? message, int delay) {
            _success = success;
            await InvokeAsync(StateHasChanged);
            if (delay > 0) await Task.Delay(delay);

            if (message != null) {
                await OkOnly(_title ?? (success ? _strings["Success"] : _strings["Failed"]), message);
            } else {
                _visible = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        public void StartSave(string title) => StartWait(ModalType.SuccessFail, title);
        public void StartLoad(string title) => StartWait(ModalType.Loading, title);

        public Task EndSave(bool success, bool suppressSuccessDisplay) => EndWait(success, null, suppressSuccessDisplay ? 0 : 750);
        public Task EndSave(bool success, string? message = null) => EndWait(success, message, 750);
        public Task EndLoad(bool success, string? message = null) => EndWait(success, message, 0);

        private List<(string Main, string? Detail)> _progress = new();
        public void StartProgressViewer(string title, Progress<(string Main, string? Detail)> progress) {
            _progress.Clear();
            progress.ProgressChanged += (_, update) => {
                _progress.Add((update.Main, update.Detail));
                InvokeAsync(StateHasChanged);
            };

            _type = ModalType.ProgressViewer;
            _controls = ModalControls.Close;

            _title = title;
            _prompt = null;
            _success = null;
            
            _visible = true;
            InvokeAsync(StateHasChanged);
        }

        public async Task EndProgressViewer(bool success, string? message) {
            _success = success;
            _controls = ModalControls.Close;
            _error = message;
            await ShowModal();
            _error = null;
        }

        public Task OkOnly(string title, string prompt) {
            _type = ModalType.PromptOnly;
            _controls = ModalControls.OK;

            _title = title;
            _prompt = prompt;

            return ShowModal();
        }

        public Task<DialogResult> OkCancel(string title, string prompt) {
            _type = ModalType.PromptOnly;
            _controls = ModalControls.OK | ModalControls.Cancel;

            _title = title;
            _prompt = prompt;

            return ShowModal();
        }

        public Task<DialogResult> YesNo(string title, string prompt) {
            _type = ModalType.PromptOnly;
            _controls = ModalControls.Yes | ModalControls.No;

            _title = title;
            _prompt = prompt;

            return ShowModal();
        }

        public Task<DialogResult> SaveDiscardCancel(string title, string prompt) {
            _type = ModalType.PromptOnly;
            _controls = ModalControls.Save | ModalControls.Discard | ModalControls.Cancel;

            _title = title;
            _prompt = prompt;

            return ShowModal();
        }

        public Task<ModalResult<string>> SingleValue(string title, string prompt, string? value = null, ValidationType validation = ValidationType.NotEmpty) {
            return SingleValue(title, prompt, value, validation, null);
        }

        public Task<ModalResult<string>> SingleValue(string title, string prompt, string? value, Regex? validationRegex) {
            return SingleValue(title, prompt, value, ValidationType.RegEx, validationRegex);
        }

        private async Task<ModalResult<string>> SingleValue(string title, string prompt, string? value, ValidationType validation, Regex? validationRegex) {
            _type = ModalType.SingleLine;
            _controls = ModalControls.OK | ModalControls.Cancel;

            _title = title;
            _prompt = prompt;
            _stringValue = value ?? string.Empty;
            _validation = validation;
            _validationRegex = validationRegex;
            _inputIsValid = ValidateInput(_stringValue);

            var result = await ShowModal();
            return new ModalResult<string>(result, _stringValue);
        }

        public async Task<ModalResult<string>> LargeText(string title, string? prompt, string? value, bool readOnly, bool closeOnly, bool monoSpace) {
            _type = ModalType.LargeText;

            if (closeOnly) {
                _controls = ModalControls.Close;
            } else {
                _controls = ModalControls.OK | ModalControls.Cancel;
            }

            _title = title;
            _prompt = prompt;
            _stringValue = value ?? string.Empty;
            _readOnly = readOnly;
            _monoSpace = monoSpace;

            var result = await ShowModal();
            return new ModalResult<string>(result, _stringValue);
        }

        private int _confirmSeconds = 60;
        public async Task<ModalResult<int>> CommitPrompt() {
            _type = ModalType.CommitPrompt;
            _controls = ModalControls.Yes | ModalControls.No;

            _title = _strings["Commit Changes"];
            _prompt = _strings["WARN_CommitChanges"];
            _confirmSeconds = 60;

            var result = await ShowModal();
            return new ModalResult<int>(result, _confirmSeconds);
        }

        public async Task<ModalResult<string?>> PasswordPrompt(string title, string prompt) {
            _type = ModalType.Password;
            _controls = ModalControls.OK | ModalControls.Cancel;

            _title = title;
            _prompt = prompt;
            _placeholder = "Password";
            _stringValue = string.Empty;

            var result = await ShowModal();
            return new ModalResult<string?>(result, _stringValue);
        }

        private string? _selectedRole;
        public async Task<ModalResult<string?>> RoleSelect(string title) {
            _type = ModalType.RoleSelect;
            _controls = ModalControls.OK | ModalControls.Cancel;

            _title = title;
            _prompt = null;
            _selectedRole = null;

            var result = await ShowModal();

            return new ModalResult<string?>(result, _selectedRole);
        }
        private void RoleSelected(string selectedRole) => _selectedRole = selectedRole;


        private CertInfo? _certInfo;
        private bool _fingerprintsOnly = false;
        public async Task CertificateDetails(CertInfo certInfo, bool fingerprintsOnly = false) {
            _type = ModalType.CertificateDetails;
            _controls = ModalControls.OK;

            _title = _strings["Certificate Details"];
            _certInfo = certInfo;
            _fingerprintsOnly = fingerprintsOnly;

            await ShowModal();
            _certInfo = null;
        }

        public async Task CertificateDetails(Guid certId, bool fingerprintsOnly = false) {
            _type = ModalType.CertificateDetails;
            _controls = ModalControls.OK;

            _title = _strings["Certificate Details"];
            _certInfo = null;
            _fingerprintsOnly = fingerprintsOnly;

            var modalTask = ShowModal();

            using var db = _dbFactory.CreateDbContext();
            var metadata = await db.Certificates.AsNoTracking().FirstOrDefaultAsync(_ => _.Id == certId);
            if (metadata == null) {
                ModalOpen?.SetResult(DialogResult.Cancel);
            } else {
                var sniHosts = await db.SniMaps
                    .AsNoTracking()
                    .Where(_ => _.CertificateId == certId)
                    .OrderByHost()
                    .Select(_ => _.Host)
                    .ToListAsync();

                var isFallback = await db.Configurations
                    .AsNoTracking()
                    .Where(_ => _.Revision == _proxyState.CandidateConfigRevision)
                    .AnyAsync(_ => _.FallbackCertId == certId);

                if (isFallback) {
                    sniHosts.Add($"*.* ({_strings["Fallback"]})");
                }

                var cert = _certService.GetCertificate(certId);
                var subjectNames = Certificates.GetSubjectAltNames(cert);
                string issuer = cert.Subject == cert.Issuer ? _strings["Self-Signed"] : cert.GetNameInfo(X509NameType.SimpleName, forIssuer: true); ;

                _certInfo = new CertInfo(metadata, cert, subjectNames, issuer, sniHosts);
            }

            await modalTask;
            _certInfo = null;
        }

        private List<CertInfo>? _availableCerts;
        private ServerCert? _selectedCert;
        public async Task<ModalResult<ServerCert?>> CertificatePicker(string title, Guid? certIdToExclude = null) {
            _type = ModalType.CertificatePicker;
            _controls = ModalControls.Cancel;

            _title = title;
            _availableCerts = null;
            _stringValue = string.Empty;

            var modalTask = ShowModal();

            _availableCerts = await _certService.CompileAvailableServerCerts(_strings);
            if (certIdToExclude != null) _availableCerts.RemoveAll(_ => _.Metadata.Id == certIdToExclude);
            var result = await modalTask;
            _availableCerts = null;

            return new ModalResult<ServerCert?>(_selectedCert == null ? DialogResult.Cancel : result, _selectedCert);
        }
        private void CertSelected(CertInfo cert) {
            if (cert.Metadata is ServerCert serverCert) {
                _selectedCert = serverCert;
                ModalOpen?.SetResult(DialogResult.OK);
            } else {
                ModalOpen?.SetResult(DialogResult.Cancel);
            }
        }
    }
}
