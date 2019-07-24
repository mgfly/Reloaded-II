﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Reloaded.Mod.Launcher.Models.ViewModel;
using Reloaded.Mod.Launcher.Utility;
using Reloaded.Mod.Loader.IO;
using Reloaded.Mod.Loader.IO.Config;
using Reloaded.WPF.Utilities;

namespace Reloaded.Mod.Launcher
{
    /// <summary>
    /// Contains all code for setting up the running of Reloaded at startup.
    /// </summary>
    public static class Setup
    {
        private static bool _loadExecuted = false;
        private static XamlResource<string> _xamlSplashCreatingDefaultConfig = new XamlResource<string>("SplashCreatingDefaultConfig");
        private static XamlResource<string> _xamlSplashPreparingResources = new XamlResource<string>("SplashPreparingResources");
        private static XamlResource<string> _xamlSplashLoadCompleteIn = new XamlResource<string>("SplashLoadCompleteIn");

        /// <summary>
        /// Sets up the overall application state for either running or testing.
        /// Note: This method uses Task.Delay for waiting the minimum splash delay without blocking the thread, it is synchronous.
        /// </summary>
        /// <param name="updateText">A function that updates the visible text onscreen.</param>
        /// <param name="minimumSplashDelay">Minimum amount of time to wait to complete the loading process.</param>
        public static async Task SetupApplication(Action<string> updateText, int minimumSplashDelay)
        {
            if (!_loadExecuted)
            {
                // Benchmark load time.
                _loadExecuted = true;
                Stopwatch watch = new Stopwatch();
                watch.Start();

                // Allow for debugging before crashing.
                AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

                // Setting up localization.
                SetupLocalization();

                // Make Default Config if Necessary.
                updateText(_xamlSplashCreatingDefaultConfig.Get());
                CreateNewConfigIfNotExist();

                // Preparing viewmodels.
                updateText(_xamlSplashPreparingResources.Get());
                SetupViewModels();

                // Wait until splash screen time.
                updateText($"{_xamlSplashLoadCompleteIn.Get()} {watch.ElapsedMilliseconds}ms");
                
                while (watch.ElapsedMilliseconds < minimumSplashDelay)
                {
                    await Task.Delay(100);
                }
            }
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (!Debugger.IsAttached)
                Debugger.Launch();
            else 
                Debugger.Break();
        }

        /// <summary>
        /// Sets up localization for the system language.
        /// </summary>
        private static void SetupLocalization()
        {
            // Set language dictionary.
            var langDict = new ResourceDictionary();
            string culture = Thread.CurrentThread.CurrentCulture.ToString();
            string languageFilePath = AppDomain.CurrentDomain.BaseDirectory + $"/Languages/{culture}.xaml";
            if (File.Exists(languageFilePath))
                langDict.Source = new Uri(languageFilePath, UriKind.Absolute);

            Application.Current.Resources.MergedDictionaries.Add(langDict);
        }

        /// <summary>
        /// Creates a new configuration if the config does not exist.
        /// </summary>
        private static void CreateNewConfigIfNotExist()
        {
            if (!LoaderConfigReader.ConfigurationExists())
                LoaderConfigReader.WriteConfiguration(new LoaderConfig());
        }

        /// <summary>
        /// Sets up viewmodels to be used in the individual mod loader pages.
        /// </summary>
        private static void SetupViewModels()
        {
            var loaderConfig = LoaderConfigReader.ReadConfiguration();
            IoC.Kernel.Bind<LoaderConfig>().ToConstant(loaderConfig);
            IoC.GetConstant<MainPageViewModel>();
            IoC.GetConstant<AddAppViewModel>();     // Consumes MainPageViewModel, make sure it goes after it.
            IoC.GetConstant<ManageModsViewModel>(); // Consumes MainPageViewModel, LoaderConfig

            IoC.GetConstant<SettingsPageViewModel>(); // Consumes ManageModsViewModel, AddAppViewModel

            // Preload.
            BasicDllInjector.PreloadAddresses();

            /* Set loader DLL path. */
            SetLoaderPaths(loaderConfig, Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]));
            LoaderConfigReader.WriteConfiguration(loaderConfig);
        }

        /// <summary>
        /// Sets the Reloaded Mod Loader DLL paths for a launcher config.
        /// </summary>
        private static void SetLoaderPaths(LoaderConfig config, string launcherDirectory)
        {
            if (String.IsNullOrEmpty(launcherDirectory))
                throw new DllNotFoundException("The provided launcher directory is null or empty. This is a bug. Report this to the developer.");

            // Loader configuration.
            var loaderPath = Path.Combine(launcherDirectory, $"Loader\\{LoaderConfig.LoaderDllName}");
            if (! File.Exists(loaderPath))
                throw new DllNotFoundException($"{LoaderConfig.LoaderDllName} {Errors.LoaderNotFound()}");

            // Bootstrappers.
            var bootstrapper32Path = Path.Combine(launcherDirectory, $"Loader\\X86\\{LoaderConfig.Bootstrapper32Name}");
            if (!File.Exists(bootstrapper32Path))
                throw new DllNotFoundException($"{LoaderConfig.Bootstrapper32Name} {Errors.LoaderNotFound()}");

            var bootstrapper64Path = Path.Combine(launcherDirectory, $"Loader\\X64\\{LoaderConfig.Bootstrapper64Name}");
            if (!File.Exists(bootstrapper64Path))
                throw new DllNotFoundException($"{LoaderConfig.Bootstrapper64Name} {Errors.LoaderNotFound()}");

            // Set to config.
            config.LauncherPath = Assembly.GetExecutingAssembly().Location;
            config.LoaderPath = loaderPath;
            config.Bootstrapper32Path = bootstrapper32Path;
            config.Bootstrapper64Path = bootstrapper64Path;
        }
    }
}