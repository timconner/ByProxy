namespace ByProxy.AdminApp.Services {
    public static class ServiceCollectionExtension {
        public static IServiceCollection AddModalService(this IServiceCollection services) => services.AddScoped<ModalService>();
    }

    public interface IModalMethods {
        public Task OkOnly(string title, string prompt);
        public Task<DialogResult> OkCancel(string title, string prompt);
        public Task<DialogResult> YesNo(string title, string prompt);
        public Task<DialogResult> SaveDiscardCancel(string title, string prompt);
        public Task<ModalResult<string>> SingleValue(string title, string prompt, string? value = null, ValidationType validation = ValidationType.NotEmpty);
        public Task<ModalResult<string>> SingleValue(string title, string prompt, string? value, Regex? validationRegex);
        public Task<ModalResult<string>> LargeText(string title, string? prompt, string? value, bool readOnly, bool closeOnly, bool monoSpace);
        public void StartSave(string title);
        public Task EndSave(bool success, string? message = null);
        public void StartLoad(string title);
        public Task EndLoad(bool success, string? message = null);
        public Task<ModalResult<int>> CommitPrompt();
        public Task<ModalResult<string?>> PasswordPrompt(string title, string prompt);
        public Task<ModalResult<string?>> RoleSelect(string title);
        public Task CertificateDetails(Guid certId, bool fingerprintsOnly = false);
        public Task CertificateDetails(CertInfo certInfo, bool fingerprintsOnly = false);
        public Task<ModalResult<ServerCert?>> CertificatePicker(string title, Guid? certIdToExclude = null);
        public void StartProgressViewer(string title, Progress<(string Main, string? Detail)> progress);
        public Task EndProgressViewer(bool success, string? message = null);
    }

    public struct ModalResult<T> {
        public DialogResult DialogResult { get; set; }
        public T Value { get; set; }

        public ModalResult(DialogResult result, T value) {
            DialogResult = result;
            Value = value;
        }
    }
    
    public enum DialogResult {
        OK,
        Save = OK,
        Yes = OK,
        Cancel,
        No = Cancel,
        Discard
    }

    public class ModalService : IModalMethods {
        private readonly IStringLocalizer<Language> _strings;

        private ModalComponent? _modal;

        public ModalService(IStringLocalizer<Language> strings) {
            _strings = strings;
        }

        public void RegisterModalComponent(ModalComponent modal) {
            _modal = modal;
        }
        [MemberNotNull(nameof(_modal))]
        private void AssertComponentRegistered() {
            if (_modal == null) throw new Exception("Modal Component Not Registered");
        }

        public async Task<bool> PerformSave(Func<Task> saveAction, bool showSuccess = false) {
            AssertComponentRegistered();
            _modal.StartSave($"{_strings["Saving"]}...");
            try {
                await saveAction();
                await _modal.EndSave(true, !showSuccess);
                return true;
            } catch (DbUpdateException ex) {
                await _modal.EndSave(false, ex.InnerException?.Message ?? ex.Message);
                return false;
            } catch (Exception ex) {
                await _modal.EndSave(false, ex.Message);
                return false;
            }
        }

        public async Task<bool> PerformProcessing(Func<Task> processingAction) {
            AssertComponentRegistered();
            _modal.StartLoad($"{_strings["Processing"]}...");
            try {
                await processingAction();
                await _modal.EndLoad(true);
                return true;
            } catch (DbUpdateException ex) {
                await _modal.EndSave(false, ex.InnerException?.Message ?? ex.Message);
                return false;
            } catch (Exception ex) {
                await _modal.EndLoad(false, ex.Message);
                return false;
            }
        }

        public async Task<bool> RunWithProgressViewer(string title, Func<IProgress<(string Main, string? Detail)>, Task> processingAction) {
            AssertComponentRegistered();
            var progress = new Progress<(string Main, string? Detail)>();
            _modal.StartProgressViewer(title, progress);
            try {
                await processingAction(progress);
                await _modal.EndProgressViewer(true, null);
                return true;
            } catch (DbUpdateException ex) {
                await _modal.EndProgressViewer(false, ex.InnerException?.Message ?? ex.Message);
                return false;
            } catch (Exception ex) {
                await _modal.EndProgressViewer(false, ex.Message);
                return false;
            }
        }

        public void StartSave(string title) {
            AssertComponentRegistered();
            _modal.StartSave(title);
        }

        public Task EndSave(bool success, string? message = null) {
            AssertComponentRegistered();
            return _modal.EndSave(success, message);
        }

        public void StartProgressViewer(string title, Progress<(string Main, string? Detail)> progress) {
            AssertComponentRegistered();
            _modal.StartProgressViewer(title, progress);
        }

        public Task EndProgressViewer(bool success, string? message = null) {
            AssertComponentRegistered();
            return _modal.EndProgressViewer(success, message);
        }

        public void StartLoad(string title) {
            AssertComponentRegistered();
            _modal.StartLoad(title);
        }

        public Task EndLoad(bool success, string? message = null) {
            AssertComponentRegistered();
            return _modal.EndLoad(success, message);
        }

        public Task OkOnly(string title, string prompt) {
            AssertComponentRegistered();
            return _modal.OkOnly(title, prompt);
        }

        public Task<DialogResult> OkCancel(string title, string prompt) {
            AssertComponentRegistered();
            return _modal.OkCancel(title, prompt);
        }

        public Task<DialogResult> YesNo(string title, string prompt) {
            AssertComponentRegistered();
            return _modal.YesNo(title, prompt);
        }

        public Task<DialogResult> SaveDiscardCancel(string title, string prompt) {
            AssertComponentRegistered();
            return _modal.SaveDiscardCancel(title, prompt);
        }

        public Task<ModalResult<string>> SingleValue(string title, string prompt, string? value, Regex? validationRegex) {
            AssertComponentRegistered();
            return _modal.SingleValue(title, prompt, value, validationRegex);
        }

        public Task<ModalResult<string>> SingleValue(string title, string prompt, string? value = null, ValidationType validation = ValidationType.NotEmpty) {
            AssertComponentRegistered();
            return _modal.SingleValue(title, prompt, value, validation);
        }

        public Task<ModalResult<string>> LargeText(string title, string? prompt, string? value = null, bool readOnly = false, bool closeOnly = false, bool monoSpace = false) {
            AssertComponentRegistered();
            return _modal.LargeText(title, prompt, value, readOnly, closeOnly, monoSpace);
        }

        public Task<ModalResult<int>> CommitPrompt() {
            AssertComponentRegistered();
            return _modal.CommitPrompt();
        }

        public Task<ModalResult<string?>> PasswordPrompt(string title, string prompt) {
            AssertComponentRegistered();
            return _modal.PasswordPrompt(title, prompt);
        }

        public Task<ModalResult<string?>> RoleSelect(string title) {
            AssertComponentRegistered();
            return _modal.RoleSelect(title);
        }

        public Task CertificateDetails(Guid certId, bool fingerprintsOnly = false) {
            AssertComponentRegistered();
            return _modal.CertificateDetails(certId, fingerprintsOnly);
        }

        public Task CertificateDetails(CertInfo certInfo, bool fingerprintsOnly = false) {
            AssertComponentRegistered();
            return _modal.CertificateDetails(certInfo, fingerprintsOnly);
        }

        public Task<ModalResult<ServerCert?>> CertificatePicker(string title, Guid? certIdToExclude = null) {
            AssertComponentRegistered();
            return _modal.CertificatePicker(title, certIdToExclude);
        }
    }
}
