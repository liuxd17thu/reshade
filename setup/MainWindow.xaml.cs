/*
 * Copyright (C) 2014 Patrick Mours
 * SPDX-License-Identifier: BSD-3-Clause
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Win32;
using ReShade.Setup.Pages;
using ReShade.Setup.Utilities;

namespace ReShade.Setup
{
	public static class SetupConfig
	{
		public static string CN2Version = @"CN2-v0.72";
		public static string CN2PackDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CN2Version);
		public static string SCFontName = @"sarasa-mono-sc-gb2312.ttf";
		public static string ShutterSEName = @"350d-shutter.wav";
		public static string SCFontPath = Path.Combine(CN2PackDir, SCFontName);
		public static string ShutterSEPath = Path.Combine(CN2PackDir, ShutterSEName);
	}
	public partial class MainWindow
	{
		readonly bool isHeadless = false;
		readonly bool isElevated = WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);

		IniFile addonsIni;
		IniFile packagesIni;
		IniFile compatibilityIni;

		readonly StatusPage status = new StatusPage();
		readonly SelectAppPage appPage = new SelectAppPage();

		Api targetApi = Api.Unknown;
		InstallOperation operation = InstallOperation.Default;
		internal static bool is64Bit;
		string targetPath;
		string targetName;
		string configPath;
		string modulePath;
		string presetPath;
		static readonly string commonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ReShade");
		string tempPath;
		string tempPathEffects;
		string tempPathTextures;
		string targetPathEffects;
		string targetPathTextures;
		string downloadPath;
		Queue<EffectPackage> packages;
		string[] effects;
		EffectPackage package;
		Queue<Addon> addons;
		Addon addon;

		public MainWindow()
		{
			InitializeComponent();

			var assembly = Assembly.GetExecutingAssembly();
			var productVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
			Title = "ReShade-CN2 安装程序 | 版本 " + productVersion + " - " + SetupConfig.CN2Version;

			if (productVersion.Contains(" "))
			{
				NavigationPanel.Background = Brushes.Crimson;
			}

			// Add support for TLS 1.2 and 1.3, so that HTTPS connection to GitHub succeeds
			if (ServicePointManager.SecurityProtocol != 0 /* Default */)
			{
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)0x3000 /* Tls13 */;
			}

			var args = Environment.GetCommandLineArgs().Skip(1).ToArray();

			// Parse command line arguments
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "--headless")
				{
					Visibility = Visibility.Hidden;

					isHeadless = true;
					continue;
				}
				if (args[i] == "--elevated")
				{
					isElevated = true;
					continue;
				}

				if (i + 1 < args.Length)
				{
					if (args[i] == "--api")
					{
						string api = args[++i];

						if (api == "d3d9")
						{
							targetApi = Api.D3D9;
						}
						else if (api == "d3d10")
						{
							targetApi = Api.D3D10;
						}
						else if (api == "d3d11")
						{
							targetApi = Api.D3D11;
						}
						else if (api == "d3d12")
						{
							targetApi = Api.D3D12;
						}
						else if (api == "dxgi")
						{
							targetApi = Api.DXGI;
						}
						else if (api == "opengl")
						{
							targetApi = Api.OpenGL;
						}
						else if (api == "vulkan")
						{
							targetApi = Api.Vulkan;
						}
						continue;
					}

					if (args[i] == "--top")
					{
						Top = double.Parse(args[++i]);
						continue;
					}
					if (args[i] == "--left")
					{
						Left = double.Parse(args[++i]);
						continue;
					}

					if (args[i] == "--state")
					{
						string state = args[++i];

						if (state == "finished")
						{
							operation = InstallOperation.Finished;
						}
						else if (state == "update")
						{
							operation = InstallOperation.Update;
						}
						else if (state == "modify")
						{
							operation = InstallOperation.Modify;
						}
						else if (state == "uninstall")
						{
							operation = InstallOperation.Uninstall;
						}
					}
				}

				if (File.Exists(args[i]))
				{
					targetPath = args[i];
					targetName = Path.GetFileNameWithoutExtension(targetPath);
				}
			}

			if (targetPath != null)
			{
				if (operation == InstallOperation.Finished)
				{
					InstallStep_Finish();
				}
				else if (targetApi != Api.Unknown)
				{
					var peInfo = new PEInfo(targetPath);
					is64Bit = peInfo.Type == PEInfo.BinaryType.IMAGE_FILE_MACHINE_AMD64;

					RunTaskWithExceptionHandling(InstallStep_CheckExistingInstallation);
				}
				else
				{
					RunTaskWithExceptionHandling(InstallStep_AnalyzeExecutable);
				}
			}
			else if (isHeadless)
			{
				UpdateStatusAndFinish(false, "No target application was provided.");
			}
			else
			{
				NextButton.IsEnabled = false;

				appPage.PathBox.TextChanged += (sender2, e2) => NextButton.IsEnabled = !string.IsNullOrEmpty(appPage.FileName) && Path.GetExtension(appPage.FileName).Equals(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(appPage.FileName);

				ResetStatus();

#if RESHADE_ADDON
				MessageBox.Show(this, "此ReShade构建版本旨在用于单人游戏，在某些多人在线游戏中可能导致账号封禁。\n*此外，ReShade-CN2（及改版安装器）的新增功能仅针对《最终幻想14》进行了测试，不保证在其他游戏中的可用性。", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
#endif
			}
		}

		static void MoveFiles(string sourcePath, string targetPath)
		{
			if (Directory.Exists(targetPath) == false)
			{
				Directory.CreateDirectory(targetPath);
			}

			foreach (string source in Directory.GetFiles(sourcePath))
			{
				string target = targetPath + source.Replace(sourcePath, string.Empty);

				File.Copy(source, target, true);
			}

			// Copy files recursively
			foreach (string source in Directory.GetDirectories(sourcePath))
			{
				string target = targetPath + source.Replace(sourcePath, string.Empty);

				MoveFiles(source, target);
			}
		}
		static bool IsWritable(string targetPath)
		{
			try
			{
				File.Create(Path.Combine(targetPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose);
				return true;
			}
			catch
			{
				return false;
			}
		}
		static bool MakeWritable(string targetPath)
		{
			try
			{
				// Ensure the file exists
				File.Open(targetPath, FileMode.OpenOrCreate).Dispose();

				var user = WindowsIdentity.GetCurrent().User;
				var access = File.GetAccessControl(targetPath);
				access.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.Modify, AccessControlType.Allow));
				File.SetAccessControl(targetPath, access);
				return true;
			}
			catch
			{
				return false;
			}
		}

		static bool ModuleExists(string path, out bool isReShade)
		{
			if (File.Exists(path))
			{
				isReShade = FileVersionInfo.GetVersionInfo(path).ProductName == "ReShade";
				return true;
			}
			else
			{
				isReShade = false;
				return false;
			}
		}

		static void RunTaskWithExceptionHandling(Action action)
		{
			Task.Run(action).ContinueWith(c => Environment.FailFast("安装过程中发生了未经处理的异常", c.Exception), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
		}

		void AddSearchPath(List<string> searchPaths, string newPath)
		{
			const string wildcard = "**";

			// Use a wildcard search path by default
			if (searchPaths.Count == 0)
			{
				searchPaths.Add(newPath + Path.DirectorySeparatorChar + wildcard);
				return;
			}

			// Avoid adding search paths already covered by an existing wildcard search path
			if (searchPaths.Any(searchPath => searchPath.EndsWith(wildcard) && newPath.StartsWith(searchPath.Remove(searchPath.Length - wildcard.Length))))
			{
				return;
			}

			// Filter out invalid search paths (and those with remaining wildcards that were not handled above)
			var validSearchPaths = searchPaths.Where(searchPath => searchPath.IndexOfAny(Path.GetInvalidPathChars()) < 0);

			try
			{
				// Avoid adding duplicate search paths (relative or absolute)
				if (validSearchPaths.Any(searchPath => Path.GetFullPath(searchPath) == Path.GetFullPath(newPath)))
				{
					return;
				}
			}
			catch
			{
				return;
			}

			searchPaths.Add(newPath);
		}
		void WriteSearchPaths(string targetPathEffects, string targetPathTextures)
		{
			// Change current directory so that "Path.GetFullPath" resolves correctly
			string currentPath = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(Path.GetDirectoryName(configPath));

			// Vulkan uses a common ReShade DLL for all applications, which is not in the location the effects and textures are installed to, so make paths absolute
			if (targetApi == Api.Vulkan)
			{
				string targetDir = Path.GetDirectoryName(targetPath);
				targetPathEffects = Path.GetFullPath(Path.Combine(targetDir, targetPathEffects));
				targetPathTextures = Path.GetFullPath(Path.Combine(targetDir, targetPathTextures));
			}

			var iniFile = new IniFile(configPath);
			List<string> paths = null;

			iniFile.GetValue("GENERAL", "EffectSearchPaths", out var effectSearchPaths);

			paths = new List<string>(effectSearchPaths ?? new string[0]);
			paths.RemoveAll(string.IsNullOrWhiteSpace);
			{
				AddSearchPath(paths, targetPathEffects);
				iniFile.SetValue("GENERAL", "EffectSearchPaths", paths.ToArray());
			}

			iniFile.GetValue("GENERAL", "TextureSearchPaths", out var textureSearchPaths);

			paths = new List<string>(textureSearchPaths ?? new string[0]);
			paths.RemoveAll(string.IsNullOrWhiteSpace);
			{
				AddSearchPath(paths, targetPathTextures);
				iniFile.SetValue("GENERAL", "TextureSearchPaths", paths.ToArray());
			}

			iniFile.SaveFile();

			Directory.SetCurrentDirectory(currentPath);
		}

		void ResetStatus()
		{
			operation = InstallOperation.Default;

			targetApi = Api.Unknown;
			targetPath = targetName = configPath = modulePath = presetPath = tempPath = tempPathEffects = tempPathTextures = targetPathEffects = targetPathTextures = downloadPath = null;
			packages = null; effects = null; package = null;

			Dispatcher.Invoke(() =>
			{
				CurrentPage.Navigate(appPage);

				int statusTitleIndex = Math.Max(Title.IndexOf("已经"), Title.IndexOf("未能"));
				if (statusTitleIndex > 0)
				{
					Title = Title.Remove(statusTitleIndex);
				}
			});
		}
		void UpdateStatus(string message)
		{
			Dispatcher.Invoke(() =>
			{
				status.UpdateStatus(message);

				if (CurrentPage.Content != status)
				{
					CurrentPage.Navigate(status);
				}

				AeroGlass.HideSystemMenu(this, true);
			});

			if (isHeadless)
			{
				Console.WriteLine(message);
			}
		}
		void UpdateStatusAndFinish(bool success, string message)
		{
			operation = InstallOperation.Finished;

			Dispatcher.Invoke(() =>
			{
				status.UpdateStatus(message, success);

				CurrentPage.Navigate(status);

				if (!Title.Contains("成功"))
				{
					Title += success ? "已经成功！" : "未能成功！";
				}

				AeroGlass.HideSystemMenu(this, false);
			});

			if (isHeadless)
			{
				Console.WriteLine(message);

				Environment.Exit(success ? 0 : 1);
			}
		}

		bool RestartWithElevatedPrivileges()
		{
			var startInfo = new ProcessStartInfo
			{
				Verb = "runas",
				FileName = Assembly.GetExecutingAssembly().Location,
				Arguments = $"\"{targetPath}\" --elevated --left {Left} --top {Top}"
			};

			switch (targetApi)
			{
				case Api.D3D9:
					startInfo.Arguments += " --api d3d9";
					break;
				case Api.D3D10:
					startInfo.Arguments += " --api d3d10";
					break;
				case Api.D3D11:
					startInfo.Arguments += " --api d3d11";
					break;
				case Api.D3D12:
					startInfo.Arguments += " --api d3d12";
					break;
				case Api.DXGI:
					startInfo.Arguments += " --api dxgi";
					break;
				case Api.OpenGL:
					startInfo.Arguments += " --api opengl";
					break;
				case Api.Vulkan:
					startInfo.Arguments += " --api vulkan";
					break;
			}

			switch (operation)
			{
				case InstallOperation.Finished:
					startInfo.Arguments += " --state finished";
					break;
				case InstallOperation.Update:
					startInfo.Arguments += " --state update";
					break;
				case InstallOperation.Modify:
					startInfo.Arguments += " --state modify";
					break;
				case InstallOperation.Uninstall:
					startInfo.Arguments += " --state uninstall";
					break;
			}

			try
			{
				Process.Start(startInfo);
				Close();
				return true;
			}
			catch
			{
				return false;
			}
		}

		void DownloadAddonsIni()
		{
			if (addonsIni != null)
			{
				return;
			}

			// Attempt to download add-ons list
			using (var client = new WebClient())
			{
				// Ensure files are downloaded again if they changed
				client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);

				try
				{
					using (var addonsStream = client.OpenRead("https://raw.githubusercontent.com/crosire/reshade-shaders/list/Addons.ini"))
					{
						addonsIni = new IniFile(addonsStream);
					}
				}
				catch
				{
					// Ignore if this list failed to download, since setup can still proceed without them
				}
			}
		}
		void DownloadCompatibilityIni()
		{
			if (compatibilityIni != null)
			{
				return;
			}

			// Attempt to download compatibility list
			using (var client = new WebClient())
			{
				// Ensure files are downloaded again if they changed
				client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);

				try
				{
					using (var compatibilityStream = client.OpenRead("https://raw.githubusercontent.com/crosire/reshade-shaders/list/Compatibility.ini"))
					{
						compatibilityIni = new IniFile(compatibilityStream);
					}
				}
				catch
				{
					// Ignore if this list failed to download, since setup can still proceed without them
				}
			}
		}
		void DownloadEffectPackagesIni()
		{
			if (packagesIni != null)
			{
				return;
			}

			// Attempt to download effect package list
			using (var client = new WebClient())
			{
				// Ensure files are downloaded again if they changed
				client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);

				try
				{
					using (var packagesStream = client.OpenRead("https://raw.githubusercontent.com/crosire/reshade-shaders/list/EffectPackages.ini"))
					{
						packagesIni = new IniFile(packagesStream);
					}
				}
				catch (Exception ex)
				{
					// Ignore if this list failed to download, since setup can still proceed without them
					if (!isHeadless)
					{
						Dispatcher.Invoke(() =>
						{
							MessageBox.Show("未能成功下载可用着色器包的列表：\n" + ex.Message + "\n\n请尝试使用代理或VPN，确保你能成功访问 https://raw.githubusercontent.com。\n\n接下来将会尝试检测随附的CN2整合包是否可用……", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
						});
					}
				}
			}
		}

		void InstallStep_CheckPrivileges()
		{
			if (!isElevated && !IsWritable(Path.GetDirectoryName(targetPath)))
			{
				RestartWithElevatedPrivileges();
			}
			else
			{
				RunTaskWithExceptionHandling(InstallStep_AnalyzeExecutable);
			}
		}
		void InstallStep_AnalyzeExecutable()
		{
			UpdateStatus("分析可执行文件…");

			// In case this is the bootstrap executable of an Unreal Engine game, try and find the actual game executable for it
			string targetPathUnrealEngine = PEInfo.ReadResourceString(targetPath, 201); // IDI_EXEC_FILE (see BootstrapPackagedGame.cpp in Unreal Engine source code)
			if (!string.IsNullOrEmpty(targetPathUnrealEngine) && File.Exists(targetPathUnrealEngine = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(targetPath), targetPathUnrealEngine))))
			{
				targetPath = targetPathUnrealEngine;
			}

			var info = FileVersionInfo.GetVersionInfo(targetPath);
			targetName = info.FileDescription;
			if (targetName is null || targetName.Trim().Length == 0)
			{
				targetName = Path.GetFileNameWithoutExtension(targetPath);
				if (char.IsLower(targetName[0]))
				{
					targetName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(targetName);
				}
			}

			var peInfo = new PEInfo(targetPath);
			is64Bit = peInfo.Type == PEInfo.BinaryType.IMAGE_FILE_MACHINE_AMD64;

			bool isApiD3D9 = false;
			bool isApiDXGI = false;
			bool isApiOpenGL = false;
			bool isApiVulkan = false;

			// Check whether the API is specified in the compatibility list, in which case setup can continue right away
			DownloadCompatibilityIni();

			var executableName = Path.GetFileName(targetPath);
			if (compatibilityIni != null && compatibilityIni.HasValue(executableName, "RenderApi"))
			{
				string api = compatibilityIni.GetString(executableName, "RenderApi");

				if (api == "D3D8" || api == "D3D9")
				{
					isApiD3D9 = true;
				}
				else if (api == "D3D10" || api == "D3D11" || api == "D3D12" || api == "DXGI")
				{
					isApiDXGI = true;
				}
				else if (api == "OpenGL")
				{
					isApiOpenGL = true;
				}
				else if (api == "Vulkan")
				{
					isApiVulkan = true;
				}
			}
			else
			{
				bool isApiD3D8 = peInfo.Modules.Any(s => s.StartsWith("d3d8", StringComparison.OrdinalIgnoreCase));
				isApiD3D9 = isApiD3D8 || peInfo.Modules.Any(s => s.StartsWith("d3d9", StringComparison.OrdinalIgnoreCase));
				isApiDXGI = peInfo.Modules.Any(s => s.StartsWith("dxgi", StringComparison.OrdinalIgnoreCase) || s.StartsWith("d3d1", StringComparison.OrdinalIgnoreCase) || s.Contains("GFSDK")); // Assume DXGI when GameWorks SDK is in use
				isApiOpenGL = peInfo.Modules.Any(s => s.StartsWith("opengl32", StringComparison.OrdinalIgnoreCase));
				isApiVulkan = peInfo.Modules.Any(s => s.StartsWith("vulkan-1", StringComparison.OrdinalIgnoreCase));

				if (isApiD3D9 && isApiDXGI)
				{
					isApiD3D9 = false; // Prefer DXGI over D3D9
				}
				if (isApiD3D8 && !isHeadless)
				{
					UpdateStatus("等待用户确认…");

					Dispatcher.Invoke(() =>
					{
						MessageBox.Show(this, "目标程序似乎使用的是Direct3D 8 API。\n为了使用ReShade，你还需要前往 https://github.com/crosire/d3d8to9/releases 下载兼容层，将所有API调用转换为Direct3D 9。", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
					});
				}
				if (isApiDXGI && isApiVulkan)
				{
					isApiDXGI = false; // Prefer Vulkan over Direct3D 12
				}
				if (isApiOpenGL && (isApiD3D8 || isApiD3D9 || isApiDXGI || isApiVulkan))
				{
					isApiOpenGL = false; // Prefer Vulkan and Direct3D over OpenGL
				}
			}

			if (isHeadless)
			{
				if (isApiD3D9)
				{
					targetApi = Api.D3D9;
				}
				else if (isApiDXGI)
				{
					targetApi = Api.DXGI;
				}
				else if (isApiOpenGL)
				{
					targetApi = Api.OpenGL;
				}
				else if (isApiVulkan)
				{
					targetApi = Api.Vulkan;
				}

				InstallStep_CheckExistingInstallation();
				return;
			}

			Dispatcher.Invoke(() =>
			{
				var page = new SelectApiPage(targetName);
				page.ApiD3D9.IsChecked = isApiD3D9;
				page.ApiDXGI.IsChecked = isApiDXGI;
				page.ApiOpenGL.IsChecked = isApiOpenGL;
				page.ApiVulkan.IsChecked = isApiVulkan;

				CurrentPage.Navigate(page);
			});
		}
		void InstallStep_CheckExistingInstallation()
		{
			UpdateStatus("检查安装状态…");

			var basePath = Path.GetDirectoryName(targetPath);
			var executableName = Path.GetFileName(targetPath);

			DownloadCompatibilityIni();

			if (targetApi != Api.Vulkan && compatibilityIni != null)
			{
				if (compatibilityIni.HasValue(executableName, "InstallTarget"))
				{
					basePath = Path.Combine(basePath, compatibilityIni.GetString(executableName, "InstallTarget"));

					var globalConfig = new IniFile(Path.Combine(Path.GetDirectoryName(targetPath), "ReShade.ini"));
					globalConfig.SetValue("INSTALL", "BasePath", basePath);
					globalConfig.SaveFile();
				}

				if (compatibilityIni.GetString(executableName, "ForceInstallApi") == "1")
				{
					string api = compatibilityIni.GetString(executableName, "RenderApi");

					if (api == "D3D8" || api == "D3D9")
					{
						targetApi = Api.D3D9;
					}
					else if (api == "D3D10")
					{
						targetApi = Api.D3D10;
					}
					else if (api == "D3D11")
					{
						targetApi = Api.D3D11;
					}
					else if (api == "D3D12")
					{
						targetApi = Api.D3D12;
					}
					else if (api == "OpenGL")
					{
						targetApi = Api.OpenGL;
					}
				}
			}

			configPath = Path.Combine(basePath, "ReShade.ini");

			if (operation == InstallOperation.Uninstall)
			{
				UninstallStep_UninstallReShadeModule();
				return;
			}

			var isReShade = false;

			if (targetApi == Api.Vulkan)
			{
				var moduleName = is64Bit ? "ReShade64" : "ReShade32";
				modulePath = Path.Combine(commonPath, moduleName, moduleName + ".dll");

				if (operation != InstallOperation.Update && operation != InstallOperation.Modify && File.Exists(configPath))
				{
					if (isHeadless)
					{
						UpdateStatusAndFinish(false, "发现已有ReShade安装，请先卸载。");
					}
					else
					{
						Dispatcher.Invoke(() =>
						{
							var page = new SelectUninstallPage();

							CurrentPage.Navigate(page);
						});
					}
					return;
				}
			}
			else
			{
				switch (targetApi)
				{
					case Api.D3D9:
						modulePath = "d3d9.dll";
						break;
					case Api.D3D10:
						modulePath = "d3d10.dll";
						break;
					case Api.D3D11:
						modulePath = "d3d11.dll";
						break;
					case Api.D3D12:
						modulePath = "d3d12.dll";
						break;
					case Api.DXGI:
						modulePath = "dxgi.dll";
						break;
					case Api.OpenGL:
						modulePath = "opengl32.dll";
						break;
					default: // No API selected, abort immediately
						UpdateStatusAndFinish(false, "无法探测该应用程序使用的渲染API。");
						return;
				}

				modulePath = Path.Combine(basePath, modulePath);

				var configPathAlt = Path.ChangeExtension(modulePath, ".ini");
				if (File.Exists(configPathAlt) && !File.Exists(configPath))
				{
					configPath = configPathAlt;
				}

				if (operation != InstallOperation.Update && operation != InstallOperation.Modify && ModuleExists(modulePath, out isReShade))
				{
					if (isReShade)
					{
						if (isHeadless)
						{
							UpdateStatusAndFinish(false, "发现已有ReShade安装，请先卸载。");
							return;
						}

						Dispatcher.Invoke(() =>
						{
							var page = new SelectUninstallPage();

							CurrentPage.Navigate(page);
						});
					}
					else
					{
						UpdateStatusAndFinish(false, Path.GetFileName(modulePath) + "已经存在，但并非来自ReShade。\n请确认它不是游戏所需的系统文件。");
					}
					return;
				}
			}

			foreach (string conflictingModuleName in new[] { "d3d9.dll", "d3d10.dll", "d3d11.dll", "d3d12.dll", "dxgi.dll", "opengl32.dll" })
			{
				string conflictingModulePath = Path.Combine(basePath, conflictingModuleName);

				if (operation != InstallOperation.Update && operation != InstallOperation.Modify && ModuleExists(conflictingModulePath, out isReShade) && isReShade)
				{
					if (isHeadless)
					{
						UpdateStatusAndFinish(false, "已有用于其他API的ReShade安装。\n不支持同时安装多个ReShade，请先卸载已有的安装。");
					}
					else
					{
						Dispatcher.Invoke(() =>
						{
							var page = new SelectUninstallPage();

							CurrentPage.Navigate(page);
						});
					}
					return;
				}
			}

			InstallStep_InstallReShadeModule();
		}
		void InstallStep_InstallReShadeModule()
		{
			UpdateStatus("安装ReShade…");

			ZipArchive zip;

			try
			{
				// Extract archive attached to this executable
				MemoryStream output;

				using (var input = File.OpenRead(Assembly.GetExecutingAssembly().Location))
				{
					output = new MemoryStream((int)input.Length);

					byte[] block = new byte[512];
					byte[] signature = { 0x50, 0x4B, 0x03, 0x04 }; // PK..

					// Look for archive at the end of this executable and copy it to a memory stream
					while (input.Read(block, 0, block.Length) >= signature.Length)
					{
						if (block.Take(signature.Length).SequenceEqual(signature) && block.Skip(signature.Length).Take(26).Max() != 0)
						{
							output.Write(block, 0, block.Length);
							input.CopyTo(output);
							break;
						}
					}
				}

				zip = new ZipArchive(output, ZipArchiveMode.Read, false);

				// Validate archive contains the ReShade DLLs
				if (zip.GetEntry("ReShade32.dll") == null || zip.GetEntry("ReShade64.dll") == null)
				{
					throw new InvalidDataException();
				}
			}
			catch (Exception)
			{
				UpdateStatusAndFinish(false, "此安装包文件已损坏！请前往 https://reshade.me 下载官方版，或重新下载CN2整合版！");
				return;
			}

			string basePath = Path.GetDirectoryName(configPath);

			// Delete any existing and conflicting ReShade installations first
			foreach (string conflictingModuleName in new[] { "d3d9.dll", "d3d10.dll", "d3d11.dll", "d3d12.dll", "dxgi.dll", "opengl32.dll" })
			{
				string conflictingModulePath = Path.Combine(basePath, conflictingModuleName);

				try
				{
					if (ModuleExists(conflictingModulePath, out bool isReShade) && isReShade)
					{
						File.Delete(conflictingModulePath);
					}
				}
				catch
				{
					// Ignore errors
					continue;
				}
			}

			if (targetApi == Api.Vulkan)
			{
				if (!isElevated)
				{
					Dispatcher.Invoke(() =>
					{
						if (!RestartWithElevatedPrivileges())
						{
							UpdateStatusAndFinish(false, "提升权限失败，因此无法安装Vulkan层。");
						}
					});
					return;
				}

				try
				{
					if (!Directory.Exists(commonPath))
					{
						Directory.CreateDirectory(commonPath);
					}
				}
				catch (Exception ex)
				{
					UpdateStatusAndFinish(false, "创建安装目录失败：\n" + ex.Message);
					return;
				}

				try
				{
					string commonPathLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReShade");
					string appConfigPath = Path.Combine(commonPath, "ReShadeApps.ini");
					string appConfigPathLocal = Path.Combine(commonPathLocal, "ReShadeApps.ini");

					// Try to migrate previous ReShade installation
					if (!File.Exists(appConfigPath) && File.Exists(appConfigPathLocal))
					{
						File.Move(appConfigPathLocal, appConfigPath);
					}

					// Unregister any layers from previous ReShade installations
					using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Khronos\Vulkan\ExplicitLayers", true))
					{
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade32", "ReShade32.json"), false);
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade64", "ReShade64.json"), false);
					}
					using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Khronos\Vulkan\ImplicitLayers", true))
					{
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade.json"), false);
						key.DeleteValue(Path.Combine(commonPathLocal, "VkLayer_override.json"), false);
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade32_vk_override_layer.json"), false);
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade64_vk_override_layer.json"), false);
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade32", "ReShade32.json"), false);
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade64", "ReShade64.json"), false);
					}
					using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Khronos\Vulkan\ImplicitLayers", true))
					{
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade32", "ReShade32.json"), false);
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade64", "ReShade64.json"), false);
					}
					using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Khronos\Vulkan\ImplicitLayers", true))
					{
						key.DeleteValue(Path.Combine(commonPathLocal, "ReShade32", "ReShade32.json"), false);
					}
				}
				catch
				{
					// Ignore errors
				}

				foreach (string layerModuleName in new[] { "ReShade32", "ReShade64" })
				{
					string layerModulePath = Path.Combine(commonPath, layerModuleName + ".dll");
					string layerManifestPath = Path.Combine(commonPath, layerModuleName + ".json");

					try
					{
						var module = zip.GetEntry(Path.GetFileName(layerModulePath));
						if (module == null)
						{
							throw new FileFormatException("安装包缺少ReShade DLL主程序。");
						}

						module.ExtractToFile(layerModulePath, true);
					}
					catch (Exception ex)
					{
						UpdateStatusAndFinish(false, "安装" + Path.GetFileName(layerModulePath) + "失败：\n" + ex.Message);
						return;
					}

					try
					{
						var manifest = zip.GetEntry(Path.GetFileName(layerManifestPath));
						if (manifest == null)
						{
							throw new FileFormatException("安装包缺少Vulkan层清单文件。");
						}

						manifest.ExtractToFile(layerManifestPath, true);

						// Register this layer manifest
						using (RegistryKey key = Registry.LocalMachine.CreateSubKey(Environment.Is64BitOperatingSystem && layerModuleName == "ReShade32" ? @"Software\Wow6432Node\Khronos\Vulkan\ImplicitLayers" : @"Software\Khronos\Vulkan\ImplicitLayers"))
						{
							key.SetValue(layerManifestPath, 0, RegistryValueKind.DWord);
						}
					}
					catch (Exception ex)
					{
						UpdateStatusAndFinish(false, "安装Vulkan层清单文件失败：\n" + ex.Message);
						return;
					}
				}

				var appConfig = new IniFile(Path.Combine(commonPath, "ReShadeApps.ini"));
				if (appConfig.GetValue(string.Empty, "Apps", out string[] appKeys) == false || !appKeys.Contains(targetPath))
				{
					var appKeysList = appKeys != null ? appKeys.ToList() : new List<string>();
					appKeysList.Add(targetPath);
					appConfig.SetValue(string.Empty, "Apps", appKeysList.ToArray());
					appConfig.SaveFile();
				}
			}
			else
			{
				string parentPath = Path.GetDirectoryName(modulePath);

				try
				{
					var module = zip.GetEntry(is64Bit ? "ReShade64.dll" : "ReShade32.dll");
					if (module == null)
					{
						throw new FileFormatException("安装包缺少ReShade DLL主程序。");
					}

					module.ExtractToFile(modulePath, true);
				}
				catch (Exception ex)
				{
					UpdateStatusAndFinish(false, "安装" + Path.GetFileName(modulePath) + "失败：\n" + ex.Message +
							(operation != InstallOperation.Default ? "\n\n请确保目标程序已经停止运行。" : string.Empty));
					return;
				}

				// Create a default log file for troubleshooting
				File.WriteAllText(Path.Combine(basePath, "ReShade.log"), @"
如果你装完ReShade并至少启动过一次游戏后，打开本日志文件读到了这些内容，那么很可能你的ReShade没有装好。

可以尝试以下这些解决方法：

1) 确保本文件以及相关的ReShade DLL文件与游戏的主程序都在同一文件夹下。
   如果仍然无法正常工作，检查是否有一个“bin”文件夹，将本文件和ReShade DLL文件移动过去，再试一次。

2) 尝试以管理员权限运行游戏。

3) 如果游戏崩溃，尝试关闭所有游戏内覆盖层（如Origin）、录制软件（如Fraps）、帧数显示软件（如MSI Afterburner/微星小飞机）、
   显卡超频软件，或者其他的代理DLL（如ENB、Helix或Umod）。

4) [CN2] 对于FF14，尝试将安装出的dxgi.dll更名为d3d11.dll。

5) 如果以上所有都没有作用，可以前往ReShade官方论坛https://reshade.me/forum获取帮助。但请在发帖前搜索你遇到的问题，可能别人已经解决了。
   [CN2] 你也可以参考随附的PDF教程，联系汉化整合包作者 路障MKXX 获得帮助。
");
			}

			// [CN2] Font & Sound Effects
			if (SetupConfig.SCFontPath != null)
			{
				var destFontPath = Path.Combine(basePath, SetupConfig.SCFontName);
				if (File.Exists(destFontPath))
					File.Delete(destFontPath);
				File.Copy(SetupConfig.SCFontPath, Path.Combine(basePath, SetupConfig.SCFontName));
			}
			if (SetupConfig.ShutterSEPath != null)
			{
				var destSEPath = Path.Combine(basePath, SetupConfig.ShutterSEName);
				if (File.Exists(destSEPath))
					File.Delete(destSEPath);
				File.Copy(SetupConfig.ShutterSEPath, Path.Combine(basePath, SetupConfig.ShutterSEName));
			}

			// Copy potential pre-made configuration file to target
			if (File.Exists("ReShade.ini") && !File.Exists(configPath))
			{
				try
				{
					File.Copy("ReShade.ini", configPath);
				}
				catch (Exception ex)
				{
					UpdateStatusAndFinish(false, "安装" + Path.GetFileName(configPath) + "失败：\n" + ex.Message);
					return;
				}
			}

			DownloadCompatibilityIni();

			// Add default configuration
			var config = new IniFile(configPath);

			// [CN2] offline compatibilityIni
			if (compatibilityIni == null)
			{
				var tmp = Path.Combine(SetupConfig.CN2PackDir, "Compatibility.ini");
				compatibilityIni = File.Exists(tmp) ? new IniFile(tmp) : null;
				if (targetName == "ffxiv_dx11.exe")
				{
					config.SetValue("GENERAL", "PreprocessorDefinitions",
						"RESHADE_DEPTH_LINEARIZATION_FAR_PLANE=1000.0",
						"RESHADE_DEPTH_INPUT_IS_UPSIDE_DOWN=" + "0",
						"RESHADE_DEPTH_INPUT_IS_REVERSED=" + "0",
						"RESHADE_DEPTH_INPUT_IS_LOGARITHMIC=" + "0");
				}
			}
			if (compatibilityIni != null && !config.HasValue("GENERAL", "PreprocessorDefinitions"))
			{
				string depthReversed = compatibilityIni.GetString(targetName, "DepthReversed", "0");
				string depthUpsideDown = compatibilityIni.GetString(targetName, "DepthUpsideDown", "0");
				string depthLogarithmic = compatibilityIni.GetString(targetName, "DepthLogarithmic", "0");
				if (!compatibilityIni.HasValue(targetName, "DepthReversed"))
				{
					var info = FileVersionInfo.GetVersionInfo(targetPath);
					if (info.LegalCopyright != null)
					{
						Match match = new Regex("(20[0-9]{2})", RegexOptions.RightToLeft).Match(info.LegalCopyright);
						if (match.Success && int.TryParse(match.Groups[1].Value, out int year))
						{
							// Modern games usually use reversed depth
							depthReversed = (year >= 2012) ? "1" : "0";
						}
					}
				}

				config.SetValue("GENERAL", "PreprocessorDefinitions",
					"RESHADE_DEPTH_LINEARIZATION_FAR_PLANE=1000.0",
					"RESHADE_DEPTH_INPUT_IS_UPSIDE_DOWN=" + depthUpsideDown,
					"RESHADE_DEPTH_INPUT_IS_REVERSED=" + depthReversed,
					"RESHADE_DEPTH_INPUT_IS_LOGARITHMIC=" + depthLogarithmic);

				if (compatibilityIni.HasValue(targetName, "DepthCopyBeforeClears") ||
					compatibilityIni.HasValue(targetName, "DepthCopyAtClearIndex") ||
					compatibilityIni.HasValue(targetName, "UseAspectRatioHeuristics"))
				{
					config.SetValue("DEPTH", "DepthCopyBeforeClears",
						compatibilityIni.GetString(targetName, "DepthCopyBeforeClears", "0"));
					config.SetValue("DEPTH", "DepthCopyAtClearIndex",
						compatibilityIni.GetString(targetName, "DepthCopyAtClearIndex", "0"));
					config.SetValue("DEPTH", "UseAspectRatioHeuristics",
						compatibilityIni.GetString(targetName, "UseAspectRatioHeuristics", "1"));
				}
			}

			// Update old configurations to new format
			if (!config.HasValue("INPUT", "KeyOverlay") && config.HasValue("INPUT", "KeyMenu"))
			{
				config.RenameValue("INPUT", "KeyMenu", "KeyOverlay");

				config.RenameValue("GENERAL", "CurrentPresetPath", "PresetPath");

				config.RenameValue("GENERAL", "ShowFPS", "OVERLAY", "ShowFPS");
				config.RenameValue("GENERAL", "ShowClock", "OVERLAY", "ShowClock");
				config.RenameValue("GENERAL", "ShowFrameTime", "OVERLAY", "ShowFrameTime");
				config.RenameValue("GENERAL", "ShowScreenshotMessage", "OVERLAY", "ShowScreenshotMessage");
				config.RenameValue("GENERAL", "FPSPosition", "OVERLAY", "FPSPosition");
				config.RenameValue("GENERAL", "ClockFormat", "OVERLAY", "ClockFormat");
				config.RenameValue("GENERAL", "NoFontScaling", "OVERLAY", "NoFontScaling");
				config.RenameValue("GENERAL", "TutorialProgress", "OVERLAY", "TutorialProgress");
				config.RenameValue("GENERAL", "VariableUIHeight", "OVERLAY", "VariableListHeight");
				config.RenameValue("GENERAL", "NewVariableUI", "OVERLAY", "VariableListUseTabs");
				config.RenameValue("GENERAL", "ScreenshotFormat", "SCREENSHOT", "FileFormat");
				config.RenameValue("GENERAL", "ScreenshotSaveBefore", "SCREENSHOT", "SaveBeforeShot");
				config.RenameValue("GENERAL", "ScreenshotSaveUI", "SCREENSHOT", "SaveOverlayShot");
				config.RenameValue("GENERAL", "ScreenshotPath", "SCREENSHOT", "SavePath");
				config.RenameValue("GENERAL", "ScreenshotIncludePreset", "SCREENSHOT", "SavePresetFile");
			}

			if (!config.HasValue("DEPTH"))
			{
				if (config.HasValue("D3D9"))
				{
					config.RenameValue("D3D9", "DisableINTZ", "DEPTH", "DisableINTZ");
					config.RenameValue("D3D9", "DepthCopyBeforeClears", "DEPTH", "DepthCopyBeforeClears");
					config.RenameValue("D3D9", "DepthCopyAtClearIndex", "DEPTH", "DepthCopyAtClearIndex");
					config.RenameValue("D3D9", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}
				else if (config.HasValue("DX9_BUFFER_DETECTION"))
				{
					config.RenameValue("DX9_BUFFER_DETECTION", "DisableINTZ", "DEPTH", "DisableINTZ");
					config.RenameValue("DX9_BUFFER_DETECTION", "PreserveDepthBuffer", "DEPTH", "DepthCopyBeforeClears");
					config.RenameValue("DX9_BUFFER_DETECTION", "PreserveDepthBufferIndex", "DEPTH", "DepthCopyAtClearIndex");
					config.RenameValue("DX9_BUFFER_DETECTION", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}

				if (config.HasValue("D3D10"))
				{
					config.RenameValue("D3D10", "DepthCopyBeforeClears", "DEPTH", "DepthCopyBeforeClears");
					config.RenameValue("D3D10", "DepthCopyAtClearIndex", "DEPTH", "DepthCopyAtClearIndex");
					config.RenameValue("D3D10", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}
				else if (config.HasValue("DX10_BUFFER_DETECTION"))
				{
					config.RenameValue("DX10_BUFFER_DETECTION", "DepthBufferRetrievalMode", "DEPTH", "DepthCopyBeforeClears");
					config.RenameValue("DX10_BUFFER_DETECTION", "DepthBufferClearingNumber", "DEPTH", "DepthCopyAtClearIndex");
					config.RenameValue("DX10_BUFFER_DETECTION", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}

				if (config.HasValue("D3D11"))
				{
					config.RenameValue("D3D11", "DepthCopyBeforeClears", "DEPTH", "DepthCopyBeforeClears");
					config.RenameValue("D3D11", "DepthCopyAtClearIndex", "DEPTH", "DepthCopyAtClearIndex");
					config.RenameValue("D3D11", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}
				else if (config.HasValue("DX11_BUFFER_DETECTION"))
				{
					config.RenameValue("DX11_BUFFER_DETECTION", "DepthBufferRetrievalMode", "DEPTH", "DepthCopyBeforeClears");
					config.RenameValue("DX11_BUFFER_DETECTION", "DepthBufferClearingNumber", "DEPTH", "DepthCopyAtClearIndex");
					config.RenameValue("DX11_BUFFER_DETECTION", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}

				if (config.HasValue("D3D12"))
				{
					config.RenameValue("D3D12", "DepthCopyBeforeClears", "DEPTH", "DepthCopyBeforeClears");
					config.RenameValue("D3D12", "DepthCopyAtClearIndex", "DEPTH", "DepthCopyAtClearIndex");
					config.RenameValue("D3D12", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}
				else if (config.HasValue("DX12_BUFFER_DETECTION"))
				{
					config.RenameValue("DX12_BUFFER_DETECTION", "DepthBufferRetrievalMode", "DEPTH", "DepthCopyBeforeClears");
					config.RenameValue("DX12_BUFFER_DETECTION", "DepthBufferClearingNumber", "DEPTH", "DepthCopyAtClearIndex");
					config.RenameValue("DX12_BUFFER_DETECTION", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}

				if (config.HasValue("OPENGL"))
				{
					config.RenameValue("OPENGL", "ReserveTextureNames", "APP", "ReserveTextureNames");
					config.RenameValue("OPENGL", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}

				if (config.HasValue("VULKAN"))
				{
					config.RenameValue("VULKAN", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}
				else if (config.HasValue("VULKAN_BUFFER_DETECTION"))
				{
					config.RenameValue("VULKAN_BUFFER_DETECTION", "UseAspectRatioHeuristics", "DEPTH", "UseAspectRatioHeuristics");
				}
			}

			if (!config.HasValue("SCREENSHOT"))
			{
				config.RenameValue("SCREENSHOTS", "FileFormat", "SCREENSHOT", "FileFormat");
				config.RenameValue("SCREENSHOTS", "SaveBeforeShot", "SCREENSHOT", "SaveBeforeShot");
				config.RenameValue("SCREENSHOTS", "SaveOverlayShot", "SCREENSHOT", "SaveOverlayShot");
				config.RenameValue("SCREENSHOTS", "SavePath", "SCREENSHOT", "SavePath");
				config.RenameValue("SCREENSHOTS", "SavePresetFile", "SCREENSHOT", "SavePresetFile");
			}

			if (!config.HasValue("SCREENSHOT", "FileNaming") && config.HasValue("SCREENSHOT", "FileNamingFormat"))
			{
				if (int.TryParse(config.GetString("SCREENSHOT", "FileNamingFormat", "0"), out int formatIndex))
				{
					if (formatIndex == 0)
					{
						config.SetValue("SCREENSHOT", "FileNaming", "%AppName% %Date% %Time%");
					}
					else if (formatIndex == 1)
					{
						config.SetValue("SCREENSHOT", "FileNaming", "%AppName% %Date% %Time% %PresetName%");
					}
				}
			}

			if (!config.HasValue("GENERAL", "PresetPath") && config.HasValue("GENERAL", "CurrentPreset"))
			{
				if (config.GetValue("GENERAL", "PresetFiles", out string[] presetFiles) &&
					int.TryParse(config.GetString("GENERAL", "CurrentPreset", "0"), out int presetIndex) && presetIndex < presetFiles.Length)
				{
					config.SetValue("GENERAL", "PresetPath", presetFiles[presetIndex]);
				}
			}

			if (!config.HasValue("GENERAL", "PresetTransitionDuration") && config.HasValue("GENERAL", "PresetTransitionDelay"))
			{
				config.RenameValue("GENERAL", "PresetTransitionDelay", "GENERAL", "PresetTransitionDuration");
			}

			if (!config.HasValue("ADDON", "AddonPath") && config.HasValue("INSTALL", "AddonPath"))
			{
				config.RenameValue("INSTALL", "AddonPath", "ADDON", "AddonPath");
			}

			if (!config.HasValue("OVERLAY", "AutoSavePreset") && config.HasValue("OVERLAY", "SavePresetOnModification"))
			{
				config.RenameValue("OVERLAY", "SavePresetOnModification", "AutoSavePreset");
			}

			// Always add app section if this is the global config
			if (Path.GetDirectoryName(configPath) == Path.GetDirectoryName(targetPath) && !config.HasValue("APP"))
			{
				config.SetValue("APP", "ForceVsync", "0");
				config.SetValue("APP", "ForceWindowed", "0");
				config.SetValue("APP", "ForceFullscreen", "0");
				config.SetValue("APP", "ForceDefaultRefreshRate", "0");
			}

			// Always add input section
			if (!config.HasValue("INPUT"))
			{
				config.SetValue("INPUT", "KeyOverlay", "48,1,0,0");
				config.SetValue("INPUT", "GamepadNavigation", "1");
			}

			config.SaveFile();

			// Change file permissions for files ReShade needs write access to
			MakeWritable(configPath);
			MakeWritable(Path.Combine(basePath, "ReShade.log"));
			MakeWritable(Path.Combine(basePath, "ReShadePreset.ini"));

			if (!isHeadless && operation != InstallOperation.Update)
			{
				// Only show the selection dialog if there are actually packages to choose
				DownloadEffectPackagesIni();

				if (packagesIni != null && packagesIni.GetSections().Length != 0)
				{
					presetPath = config.GetString("GENERAL", "PresetPath", string.Empty);

					Dispatcher.Invoke(() =>
					{
						var page = new SelectPresetPage();
						page.FileName = presetPath;

						CurrentPage.Navigate(page);
					});
					return;
				}
				else if (Directory.Exists(SetupConfig.CN2PackDir))
				{
					presetPath = config.GetString("GENERAL", "PresetPath", string.Empty);
					Dispatcher.Invoke(() =>
					{
						var page = new SelectPackagesPage(packagesIni, new List<string>());
						CurrentPage.Navigate(page);
					});
					return;
				}
			}

			// Add default search paths if no config exists
			if (!config.HasValue("GENERAL", "EffectSearchPaths") && !config.HasValue("GENERAL", "TextureSearchPaths"))
			{
				WriteSearchPaths(".\\reshade-shaders\\Shaders\\**", ".\\reshade-shaders\\Textures\\**");
			}

			// [CN2] Add default font and sound effects
			if (SetupConfig.SCFontPath != null && !config.HasValue("STYLE", "Font"))
			{
				config.SetValue("STYLE", ".\\" + SetupConfig.SCFontName);
			}
			if (SetupConfig.ShutterSEPath != null && !config.HasValue("SCREENSHOT", "SoundPath"))
			{
				config.SetValue("SCREENSHOTS", ".\\" + SetupConfig.ShutterSEName);
			}

			InstallStep_Finish();
		}
		void InstallStep_CheckPreset()
		{
			var effectFiles = new List<string>();

			if (!string.IsNullOrEmpty(presetPath))
			{
				// Change current directory so that "Path.GetFullPath" resolves correctly
				string currentPath = Directory.GetCurrentDirectory();
				Directory.SetCurrentDirectory(Path.GetDirectoryName(configPath));
				presetPath = Path.GetFullPath(presetPath);
				Directory.SetCurrentDirectory(currentPath);

				if (File.Exists(presetPath))
				{
					var config = new IniFile(configPath);
					config.SetValue("GENERAL", "PresetPath", presetPath);
					config.SaveFile();

					var preset = new IniFile(presetPath);

					if (preset.GetValue(string.Empty, "Techniques", out string[] techniques))
					{
						foreach (string technique in techniques)
						{
							var filenameIndex = technique.IndexOf('@');
							if (filenameIndex > 0)
							{
								string filename = technique.Substring(filenameIndex + 1);

								effectFiles.Add(filename);
							}
						}
					}

					MakeWritable(presetPath);
				}
			}

			DownloadEffectPackagesIni();

			Dispatcher.Invoke(() =>
			{
				var page = new SelectPackagesPage(packagesIni, effectFiles);

				CurrentPage.Navigate(page);
			});
		}
		void InstallStep_DownloadEffectPackage()
		{
			package = packages.Dequeue();
			downloadPath = Path.GetTempFileName();

			UpdateStatus("从" + package.DownloadUrl + "下载" + package.PackageName + "…");

			var client = new WebClient();

			client.DownloadFileCompleted += (object sender, System.ComponentModel.AsyncCompletedEventArgs e) =>
			{
				if (e.Error != null)
				{
					UpdateStatusAndFinish(false, "从" + package.DownloadUrl + "下载失败：\n" + e.Error.Message);
				}
				else
				{
					InstallStep_ExtractEffectPackage();
				}
			};

			client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
			{
				// Avoid negative percentage values
				if (e.TotalBytesToReceive > 0)
				{
					UpdateStatus("下载" + package.PackageName + "… (" + ((100 * e.BytesReceived) / e.TotalBytesToReceive) + "%)");
				}
			};

			try
			{
				client.DownloadFileAsync(new Uri(package.DownloadUrl), downloadPath);
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "从" + package.DownloadUrl + "下载失败：\n" + ex.Message);
			}
		}
		void InstallStep_ExtractEffectPackage()
		{
			UpdateStatus("提取" + package.PackageName + "…");

			tempPath = Path.Combine(Path.GetTempPath(), "reshade-shaders");

			string basePath = Path.GetDirectoryName(configPath);
			targetPathEffects = Path.Combine(basePath, package.InstallPath);
			targetPathTextures = Path.Combine(basePath, package.TextureInstallPath);

			try
			{
				// Delete existing directories since extraction fails if the target is not empty
				if (Directory.Exists(tempPath))
				{
					Directory.Delete(tempPath, true);
				}

				ZipFile.ExtractToDirectory(downloadPath, tempPath);

				effects = Directory.GetFiles(tempPath, "*.fx", SearchOption.AllDirectories);

				// First check for a standard directory name
				tempPathEffects = Directory.GetDirectories(tempPath, "Shaders", SearchOption.AllDirectories).FirstOrDefault();
				tempPathTextures = Directory.GetDirectories(tempPath, "Textures", SearchOption.AllDirectories).FirstOrDefault();

				// If that does not exist, look for the first directory that contains shaders/textures
				if (tempPathEffects == null)
				{
					tempPathEffects = effects.Select(x => Path.GetDirectoryName(x)).OrderBy(x => x.Length).FirstOrDefault();
				}
				if (tempPathTextures == null)
				{
					string[] tempTextureExtensions = { "*.png", "*.jpg", "*.jpeg" };

					foreach (string extension in tempTextureExtensions)
					{
						string path = Directory.GetFiles(tempPath, extension, SearchOption.AllDirectories).Select(x => Path.GetDirectoryName(x)).OrderBy(x => x.Length).FirstOrDefault();
						if (!string.IsNullOrEmpty(path))
						{
							tempPathTextures = tempPathTextures != null ? tempPathTextures.Union(path).ToString() : path;
						}
					}
				}

				// Skip any effects no in the shader directory
				effects = effects.Where(x => x.StartsWith(tempPathEffects)).ToArray();

				// Delete denied effects
				if (package.DenyEffectFiles != null)
				{
					var denyEffectFiles = effects.Where(x => package.DenyEffectFiles.Contains(Path.GetFileName(x)));

					foreach (string filePath in denyEffectFiles)
					{
						File.Delete(filePath);
					}

					effects = effects.Except(denyEffectFiles).ToArray();
				}
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "提取" + package.PackageName + "失败：\n" + ex.Message);
				return;
			}

			// Show file selection dialog
			if (!isHeadless && package.Selected == null)
			{
				effects = effects.Select(x => targetPathEffects + x.Remove(0, tempPathEffects.Length)).ToArray();

				Dispatcher.Invoke(() =>
				{
					var page = new SelectEffectsPage(package.PackageName, effects);

					CurrentPage.Navigate(page);
				});
				return;
			}

			InstallStep_InstallEffectPackage();
		}
		void InstallStep_InstallEffectPackage()
		{
			try
			{
				// Move only the relevant files to the target
				if (tempPathEffects != null)
				{
					MoveFiles(tempPathEffects, targetPathEffects);
				}
				if (tempPathTextures != null)
				{
					MoveFiles(tempPathTextures, targetPathTextures);
				}

				File.Delete(downloadPath);
				Directory.Delete(tempPath, true);
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "安装" + package.PackageName + "失败：\n" + ex.Message);
				return;
			}

			WriteSearchPaths(package.InstallPath, package.TextureInstallPath);

			if (packages.Count != 0)
			{
				InstallStep_DownloadEffectPackage();
			}
			else
			{
				InstallStep_CheckAddons();
			}
		}
		void InstallStep_AutoCN2_InstallEffectPackage()
		{
			try
			{
				tempPathEffects = Path.Combine(SetupConfig.CN2PackDir, @"reshade-shaders\Shaders");
				tempPathTextures = Path.Combine(SetupConfig.CN2PackDir, @"reshade-shaders\Textures");
				string basePath = Path.GetDirectoryName(configPath);
				targetPathEffects = Path.Combine(basePath, @"reshade-shaders\Shaders");
				targetPathTextures = Path.Combine(basePath, @"reshade-shaders\Textures");
				MoveFiles(tempPathEffects, targetPathEffects);
				MoveFiles(tempPathTextures, targetPathTextures);
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "安装CN2整合失败：\n" + ex.Message);
				return;
			}
			InstallStep_AutoCN2_CheckAddons();
		}
		void InstallStep_AutoCN2_CheckAddons()
		{
#if RESHADE_ADDON
			if (!isHeadless)
			{
				DownloadAddonsIni();

				Dispatcher.Invoke(() =>
				{
					var page = new SelectAddonsPage(addonsIni);

					CurrentPage.Navigate(page);
				});
			}
			else
#endif
			{
				InstallStep_Finish();
			}
		}
		void InstallStep_AutoCN2_InstallAddon()
		{
			string addonPath = Path.GetDirectoryName(targetPath);

			var globalConfigPath = Path.Combine(addonPath, "ReShade.ini");
			if (File.Exists(globalConfigPath))
			{
				var globalConfig = new IniFile(globalConfigPath);
				addonPath = globalConfig.GetString("ADDON", "AddonPath", addonPath);
			}

			try
			{
				File.Copy(downloadPath, Path.Combine(addonPath, Path.GetFileName(tempPath)), true);
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "安装" + addon.Name + "失败：\n" + ex.Message);
				return;
			}

			if (addons.Count != 0)
			{
				InstallStep_DownloadAddon();
			}
			else
			{
				InstallStep_Finish();
			}
		}
		void InstallStep_CheckAddons()
		{
#if RESHADE_ADDON
			if (!isHeadless)
			{
				DownloadAddonsIni();

				if (addonsIni != null)
				{
					Dispatcher.Invoke(() =>
					{
						var page = new SelectAddonsPage(addonsIni);

						CurrentPage.Navigate(page);
					});
				}
				else
				{
					InstallStep_Finish();
				}
			}
			else
#endif
			{
				InstallStep_Finish();
			}
		}
		void InstallStep_DownloadAddon()
		{
			addon = addons.Dequeue();
			downloadPath = Path.GetTempFileName();

			UpdateStatus("从" + addon.DownloadUrl + "下载" + addon.Name + "…");

			tempPath = (MainWindow.is64Bit ? addon.DownloadUrl64 : addon.DownloadUrl32);
			if (tempPath[0] == '@')
			{
				tempPath = tempPath.Trim('@');
				downloadPath = Path.Combine(SetupConfig.CN2PackDir, tempPath);
				InstallStep_AutoCN2_InstallAddon();
				return;
			}

			var client = new WebClient();

			client.DownloadFileCompleted += (object sender, System.ComponentModel.AsyncCompletedEventArgs e) =>
			{
				if (e.Error != null)
				{
					UpdateStatusAndFinish(false, "从" + addon.DownloadUrl + "下载失败：\n" + e.Error.Message);
				}
				else
				{
					string ext = Path.GetExtension(new Uri(addon.DownloadUrl).AbsolutePath);

					if (ext == ".addon" || ext == ".addon32" || ext == ".addon64")
					{
						InstallStep_InstallAddon();
					}
					else
					{
						InstallStep_ExtractAddon();
					}
				}
			};

			client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
			{
				// Avoid negative percentage values
				if (e.TotalBytesToReceive > 0)
				{
					UpdateStatus("下载" + addon.Name + "… (" + ((100 * e.BytesReceived) / e.TotalBytesToReceive) + "%)");
				}
			};

			try
			{
				client.DownloadFileAsync(new Uri(addon.DownloadUrl), downloadPath);
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "从" + addon.DownloadUrl + "下载失败：\n" + ex.Message);
			}
		}
		void InstallStep_ExtractAddon()
		{
			UpdateStatus("提取" + addon.Name + "…");

			tempPath = Path.Combine(Path.GetTempPath(), "reshade-addons");

			try
			{
				// Delete existing directories since extraction fails if the target is not empty
				if (Directory.Exists(tempPath))
				{
					Directory.Delete(tempPath, true);
				}

				ZipFile.ExtractToDirectory(downloadPath, tempPath);

				string addonPath = Directory.GetFiles(tempPath, is64Bit ? "*.addon64" : "*.addon32", SearchOption.AllDirectories).FirstOrDefault();
				if (addonPath == null)
				{
					addonPath = Directory.GetFiles(tempPath, "*.addon").FirstOrDefault(x => x.Contains(is64Bit ? "x64" : "x86"));
				}
				if (addonPath == null)
				{
					Directory.Delete(tempPath, true);
					File.Delete(downloadPath);

					throw new FileFormatException("插件安装包内没有找到插件。");
				}

				downloadPath = addonPath;
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "提取" + addon.Name + "失败：\n" + ex.Message);
				return;
			}

			InstallStep_InstallAddon();
		}
		void InstallStep_InstallAddon()
		{
			string addonPath = Path.GetDirectoryName(targetPath);

			var globalConfigPath = Path.Combine(addonPath, "ReShade.ini");
			if (File.Exists(globalConfigPath))
			{
				var globalConfig = new IniFile(globalConfigPath);
				addonPath = globalConfig.GetString("ADDON", "AddonPath", addonPath);
			}

			try
			{
				File.Copy(downloadPath, Path.Combine(addonPath, Path.GetFileNameWithoutExtension(tempPath != null ? downloadPath : new Uri(addon.DownloadUrl).AbsolutePath) + (is64Bit ? ".addon64" : ".addon32")), true);

				File.Delete(downloadPath);
				if (tempPath != null)
				{
					Directory.Delete(tempPath, true);
				}
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "安装" + addon.Name + "失败：\n" + ex.Message);
				return;
			}

			if (addons.Count != 0)
			{
				InstallStep_DownloadAddon();
			}
			else
			{
				InstallStep_Finish();
			}
		}
		void InstallStep_Finish()
		{
			UpdateStatusAndFinish(true, "成功安装ReShade。" + (isHeadless ? string.Empty : "\n点击“完成”按钮退出安装程序。"));
		}

		void UninstallStep_UninstallReShadeModule()
		{
			if (targetApi == Api.Vulkan)
			{
				if (!isElevated)
				{
					Dispatcher.Invoke(() =>
					{
						if (!RestartWithElevatedPrivileges())
						{
							UpdateStatusAndFinish(false, "提升权限失败，因此无法卸载Vulkan层。");
						}
					});
					return;
				}

				var appConfig = new IniFile(Path.Combine(commonPath, "ReShadeApps.ini"));
				if (appConfig.GetValue(string.Empty, "Apps", out string[] appKeys))
				{
					var appKeysList = appKeys.ToList();
					appKeysList.Remove(targetPath);
					appConfig.SetValue(string.Empty, "Apps", appKeysList.ToArray());

					if (appKeysList.Count == 0)
					{
						try
						{
							Directory.Delete(commonPath, true);

							using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"Software\Khronos\Vulkan\ImplicitLayers"))
							{
								key.DeleteValue(Path.Combine(commonPath, "ReShade32.json"), false);
								key.DeleteValue(Path.Combine(commonPath, "ReShade64.json"), false);
							}

							if (Environment.Is64BitOperatingSystem)
							{
								using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"Software\Wow6432Node\Khronos\Vulkan\ImplicitLayers"))
								{
									key.DeleteValue(Path.Combine(commonPath, "ReShade32.json"), false);
								}
							}
						}
						catch (Exception ex)
						{
							UpdateStatusAndFinish(false, "删除Vulkan层清单文件失败：\n" + ex.Message);
							return;
						}
					}
					else
					{
						appConfig.SaveFile();
					}
				}
			}

			try
			{
				string basePath = Path.GetDirectoryName(configPath);

				if (targetApi != Api.Vulkan)
				{
					File.Delete(modulePath);
				}

				if (File.Exists(configPath))
				{
					File.Delete(configPath);
				}

				if (File.Exists(Path.Combine(basePath, "ReShade.log")))
				{
					File.Delete(Path.Combine(basePath, "ReShade.log"));
				}
				// [CN2] Remove Font & Shutter SE
				if (File.Exists(Path.Combine(basePath, SetupConfig.SCFontName)))
				{
					File.Delete(Path.Combine(basePath, SetupConfig.SCFontName));
				}
				if (File.Exists(Path.Combine(basePath, SetupConfig.ShutterSEName)))
				{
					File.Delete(Path.Combine(basePath, SetupConfig.ShutterSEName));
				}

				var remove_addons = Directory.GetFiles(basePath, "*.addon*", SearchOption.TopDirectoryOnly);
				foreach (var ad in remove_addons)
				{
					File.Delete(ad);
				}

				if (Directory.Exists(Path.Combine(basePath, "reshade-shaders")))
				{
					Directory.Delete(Path.Combine(basePath, "reshade-shaders"), true);
				}

				// Delete all other existing ReShade installations too
				foreach (string conflictingModuleName in new[] { "d3d9.dll", "d3d10.dll", "d3d11.dll", "d3d12.dll", "dxgi.dll", "opengl32.dll" })
				{
					string conflictingModulePath = Path.Combine(basePath, conflictingModuleName);

					if (ModuleExists(conflictingModulePath, out bool isReShade) && isReShade)
					{
						File.Delete(conflictingModulePath);
					}
				}
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "删除部分ReShade文件失败：\n" + ex.Message +
					(operation != InstallOperation.Default ? "\n\n请确保目标程序已经停止运行！" : string.Empty));
				return;
			}

			UninstallStep_Finish();
		}
		void UninstallStep_Finish()
		{
			UpdateStatusAndFinish(true, "成功卸载ReShade。" + (isHeadless ? string.Empty : "\n点击“完成”按钮退出安装程序。"));
		}

		void OnWindowInit(object sender, EventArgs e)
		{
			AeroGlass.HideIcon(this);
			AeroGlass.HideSystemMenu(this, targetPath != null);
		}

		void OnNextButtonClick(object sender, RoutedEventArgs e)
		{
			if (operation == InstallOperation.Finished)
			{
				Close();
				return;
			}

			if (CurrentPage.Content is SelectAppPage appPage)
			{
				appPage.Cancel();

				targetPath = appPage.FileName;
				SetupConfig.SCFontPath = appPage.InstallSCFontCheckBox.IsChecked??false ? SetupConfig.SCFontPath : null;
				SetupConfig.ShutterSEPath = appPage.InstallShutterSECheckBox.IsChecked??false ? SetupConfig.ShutterSEPath : null;

				InstallStep_CheckPrivileges();
				return;
			}

			if (CurrentPage.Content is SelectApiPage apiPage)
			{
				if (apiPage.ApiD3D9.IsChecked == true)
				{
					targetApi = Api.D3D9;
				}
				if (apiPage.ApiDXGI.IsChecked == true)
				{
					targetApi = Api.DXGI;
				}
				if (apiPage.ApiOpenGL.IsChecked == true)
				{
					targetApi = Api.OpenGL;
				}
				if (apiPage.ApiVulkan.IsChecked == true)
				{
					targetApi = Api.Vulkan;
				}

				RunTaskWithExceptionHandling(InstallStep_CheckExistingInstallation);
				return;
			}

			if (CurrentPage.Content is SelectUninstallPage uninstallPage)
			{
				if (uninstallPage.UninstallButton.IsChecked == true)
				{
					operation = InstallOperation.Uninstall;

					RunTaskWithExceptionHandling(UninstallStep_UninstallReShadeModule);
				}
				else
				{
					if (uninstallPage.UpdateButton.IsChecked == true)
					{
						operation = InstallOperation.Update;

					}
					if (uninstallPage.ModifyButton.IsChecked == true)
					{
						operation = InstallOperation.Modify;
					}

					RunTaskWithExceptionHandling(InstallStep_InstallReShadeModule);
				}
				return;
			}

			if (CurrentPage.Content is SelectPresetPage presetPage)
			{
				presetPath = presetPage.FileName;

				RunTaskWithExceptionHandling(InstallStep_CheckPreset);
				return;
			}

			if (CurrentPage.Content is SelectPackagesPage packagesPage)
			{
				packages = new Queue<EffectPackage>(packagesPage.SelectedItems);

				if (packagesPage.AutoCN2.IsChecked ?? false)
				{
					RunTaskWithExceptionHandling(InstallStep_AutoCN2_InstallEffectPackage);
				}
				else if (packages.Count != 0)
				{
					RunTaskWithExceptionHandling(InstallStep_DownloadEffectPackage);
				}
				else
				{
					RunTaskWithExceptionHandling(InstallStep_CheckAddons);
				}
				return;
			}

			if (CurrentPage.Content is SelectEffectsPage effectsPage)
			{
				RunTaskWithExceptionHandling(() =>
				{
					try
					{
						// Delete all unselected effect files before moving
						foreach (string filePath in effects.Except(effectsPage.SelectedItems.Select(x => x.FilePath)))
						{
							File.Delete(tempPathEffects + filePath.Remove(0, targetPathEffects.Length));
						}
					}
					catch (Exception ex)
					{
						UpdateStatusAndFinish(false, "Failed to delete an effect file from " + package.PackageName + ":\n" + ex.Message);
						return;
					}

					InstallStep_InstallEffectPackage();
				});
				return;
			}

			if (CurrentPage.Content is SelectAddonsPage addonsPage)
			{
				addons = new Queue<Addon>(addonsPage.SelectedItems);

				if (addons.Count != 0)
				{
					RunTaskWithExceptionHandling(InstallStep_DownloadAddon);
				}
				else
				{
					RunTaskWithExceptionHandling(InstallStep_Finish);
				}
				return;
			}
		}

		void OnCancelButtonClick(object sender, RoutedEventArgs e)
		{
			if (CurrentPage.Content is SelectAppPage appPage)
			{
				appPage.Cancel();
			}

			if (CurrentPage.Content is SelectPresetPage)
			{
				presetPath = null;

				RunTaskWithExceptionHandling(InstallStep_CheckPreset);
				return;
			}

			if (CurrentPage.Content is SelectPackagesPage)
			{
				RunTaskWithExceptionHandling(InstallStep_Finish);
				return;
			}

			if (CurrentPage.Content is SelectEffectsPage)
			{
				RunTaskWithExceptionHandling(InstallStep_InstallEffectPackage);
				return;
			}

			if (CurrentPage.Content is SelectAddonsPage)
			{
				RunTaskWithExceptionHandling(InstallStep_Finish);
				return;
			}

			if (operation == InstallOperation.Finished)
			{
				ResetStatus();
				return;
			}

			Close();
		}

		void OnCurrentPageNavigated(object sender, NavigationEventArgs e)
		{
			bool isFinished = operation == InstallOperation.Finished;

			NextButton.Content = isFinished ? "完成(_F)" : "下一步(_N)";
			CancelButton.Content = isFinished ? "返回(_B)" : (e.Content is SelectPresetPage || e.Content is SelectPackagesPage || e.Content is SelectEffectsPage) ? "跳过(_S)" : (e.Content is SelectAppPage) ? "关闭(_C)" : "取消(_C)";

			CancelButton.IsEnabled = !(e.Content is StatusPage) || isFinished;

			if (!(e.Content is SelectAppPage))
			{
				NextButton.IsEnabled = isFinished || !(e.Content is StatusPage);
			}
		}
	}
}
