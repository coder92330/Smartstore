﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartstore.Core;
using Smartstore.Engine;
using Smartstore.Engine.Runtimes;
using Smartstore.Threading;
using Smartstore.Utilities;

namespace Smartstore.Pdf.WkHtml
{
    // TODO: (core) Implement BatchMode
    public class WkHtmlToPdfConverter : IPdfConverter
    {
        private static string _tempPath;
        private static AsyncLazy<string> _toolExePath = new(GetToolExePathAsync);
        private readonly static string[] _ignoreErrLines = new string[] 
        { 
            "Exit with code 1 due to network error: ContentNotFoundError", 
            "QFont::setPixelSize: Pixel size <= 0", 
            "Exit with code 1 due to network error: ProtocolUnknownError", 
            "Exit with code 1 due to network error: HostNotFoundError", 
            "Exit with code 1 due to network error: ContentOperationNotPermittedError", 
            "Exit with code 1 due to network error: UnknownContentError" 
        };

        private Process _process;

        private readonly IApplicationContext _appContext;
        private readonly IWkHtmlCommandBuilder _commandBuilder;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly WkHtmlToPdfOptions _options;
        private readonly AsyncRunner _asyncRunner;

        public WkHtmlToPdfConverter(
            IApplicationContext appContext,
            IWkHtmlCommandBuilder commandBuilder, 
            IHttpContextAccessor httpContextAccessor,
            IOptions<WkHtmlToPdfOptions> options,
            AsyncRunner asyncRunner,
            ILogger<WkHtmlToPdfConverter> logger)
        {
            _appContext = appContext;
            _commandBuilder = commandBuilder;
            _httpContextAccessor = httpContextAccessor;
            _options = options.Value;
            _asyncRunner = asyncRunner;

            Logger = logger;
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        private static async Task<string> GetToolExePathAsync()
        {
            var libraryManager = EngineContext.Current.Application.Services.Resolve<INativeLibraryManager>();

            var fi = libraryManager.GetNativeExecutable("wkhtmltopdf");
            if (!fi.Exists)
            {
                using var libraryInstaller = libraryManager.CreateLibraryInstaller();
                fi = await libraryInstaller.InstallFromPackageAsync(new InstallNativePackageRequest("wkhtmltopdf", true, "Smartstore.wkhtmltopdf.Native"));
            }

            if (!fi.Exists)
            {
                throw new InvalidOperationException($"Unable to install PDF processor tool 'wkhtmltopdf'.");
            }

            return fi.FullName;
        }

        /// <summary>
        /// Occurs when log line is received from WkHtmlToPdf process
        /// </summary>
        /// <remarks>
        /// Quiet mode should be disabled if you want to get wkhtmltopdf info/debug messages
        /// </remarks>
        public event EventHandler<DataReceivedEventArgs> LogReceived;

        public virtual IPdfInput CreateFileInput(string urlOrPath)
        {
            Guard.NotEmpty(urlOrPath, nameof(urlOrPath));
            return new WkFileInput(urlOrPath, _options, _httpContextAccessor.HttpContext);
        }

        public virtual IPdfInput CreateHtmlInput(string html)
        {
            Guard.NotEmpty(html, nameof(html));
            return new WkHtmlInput(html, _options, _httpContextAccessor.HttpContext);
        }

        public Task<Stream> GeneratePdfAsync(PdfConversionSettings settings, CancellationToken cancelToken = default)
        {
            Guard.NotNull(settings, nameof(settings));

            if (settings.Page == null)
            {
                throw new ArgumentException($"The '{nameof(settings.Page)}' property of the '{nameof(settings)}' argument cannot be null.", nameof(settings));
            }

            return GeneratePdfCoreAsync(settings, cancelToken);
        }

        protected virtual async Task<Stream> GeneratePdfCoreAsync(PdfConversionSettings settings, CancellationToken cancelToken = default)
        {
            // Check that process is not already running
            CheckProcess();
            
            try
            {
                // Build command / arguments
                using var psb = StringBuilderPool.Instance.Get(out var sb);
                await _commandBuilder.BuildCommandAsync(settings, sb);

                // Create output PDF temp file name
                var outputFileName = GetTempFileName(_options, ".pdf");
                sb.AppendFormat(" \"{0}\" ", outputFileName);

                var arguments = sb.ToString();

                // Run process
                var compositeCancelToken = CreateCancellationToken(cancelToken);
                await RunProcessAsync(arguments, settings.Page, compositeCancelToken);

                compositeCancelToken.ThrowIfCancellationRequested();

                // Return wkhtml output file as temp file stream (auto-deletes on close)
                if (File.Exists(outputFileName))
                {
                    return new FileStream(outputFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                }
                else
                {
                    throw new FileNotFoundException($"PDF converter cannot find output file '{outputFileName}'.");
                }
            }
            catch (Exception ex)
            {
                EnsureProcessStopped();

                Logger.Error(ex, $"Html to Pdf conversion error: {ex.Message}.");
                throw;

            }
            finally
            {
                // Teardown / clear inputs
                settings.Page.Teardown();
                settings.Header?.Teardown();
                settings.Footer?.Teardown();
                settings.Cover?.Teardown();
            }
        }

        #region WkHtml utilities

        internal static string GetTempFileName(WkHtmlToPdfOptions options, string extension)
        {
            return Path.Combine(GetTempPath(options), "pdfgen-" + Path.GetRandomFileName() + extension.EmptyNull());
        }

        internal static string GetTempPath(WkHtmlToPdfOptions options)
        {
            LazyInitializer.EnsureInitialized(ref _tempPath, () =>
            {
                if (options.TempFilesPath.HasValue() && !Directory.Exists(options.TempFilesPath))
                {
                    Directory.CreateDirectory(options.TempFilesPath);
                }

                return options.TempFilesPath ?? Path.GetTempPath();
            });

            return _tempPath;
        }

        private async Task RunProcessAsync(string arguments, IPdfInput input, CancellationToken cancelToken)
        {
            var lastErrorLine = string.Empty;
            DataReceivedEventHandler onDataReceived = ((o, e) =>
            {
                if (e.Data == null) return;
                if (e.Data.HasValue()) 
                {
                    lastErrorLine = e.Data;
                    Logger.Error("WkHtml error: {0}.", e.Data);
                }
                LogReceived?.Invoke(this, e);
            });

            try
            {
                _process = Process.Start(new ProcessStartInfo
                {
                    FileName = await _toolExePath,
                    WorkingDirectory = _appContext.RuntimeInfo.NativeLibraryDirectory,
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    StandardInputEncoding = Encoding.UTF8,
                    RedirectStandardInput = input.Kind == PdfInputKind.Html,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true
                });

                if (_options.ProcessPriority != ProcessPriorityClass.Normal)
                {
                    _process.PriorityClass = _options.ProcessPriority;
                }  

                _process.ErrorDataReceived += onDataReceived;
                _process.BeginErrorReadLine();

                if (input.Kind == PdfInputKind.Html)
                {
                    using var sIn = _process.StandardInput;
                    sIn.WriteLine(input.Content);
                }

                await _process.WaitForExitAsync(cancelToken);
            }
            finally
            {
                EnsureProcessStopped();
            }
        }

        private void CheckProcess()
        {
            if (_process != null)
            {
                throw new InvalidOperationException("WkHtmlToPdf process has already been started.");
            }
        }

        private CancellationToken CreateCancellationToken(CancellationToken userCancelToken = default)
        {
            var result = _asyncRunner.CreateCompositeCancellationToken(userCancelToken);
            if (_options.ExecutionTimeout.HasValue)
            {
                var cts = new CancellationTokenSource(_options.ExecutionTimeout.Value);
                result = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, result).Token;
            }
            
            return result;
        }

        private void EnsureProcessStopped()
        {
            if (_process != null)
            {
                _process.EnsureStopped();
                _process = null;
            }
        }

        #endregion
    }
}
