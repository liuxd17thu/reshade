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
using System.Windows.Shell;
using Microsoft.Win32;
using ReShade.Setup.Pages;
using ReShade.Setup.Utilities;

namespace ReShade.Setup
{
	public static class SetupConfig
	{
		public static string CN2Version = @"AUR";
		public static string CN2PackDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CN2Version);
		public static string SCFontName = @"sarasa-mono-sc-gb2312.ttf";
		public static string ShutterSEName = @"350d-shutter.wav";
		public static string SCFontPath = Path.Combine(CN2PackDir, SCFontName);
		public static string ShutterSEPath = Path.Combine(CN2PackDir, ShutterSEName);
	}
	public partial class MainWindow
	{
		public MainWindow()
		{
			InitializeComponent();

			var assembly = Assembly.GetExecutingAssembly();
			var productVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
			Title = "AuroraShade安装程序" + assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description + " [ReShade: " + productVersion + "]";

			if (productVersion.Contains(" "))
			{
				NavigationPanel.Background = Brushes.Crimson;
				productVersion = productVersion.Remove(productVersion.IndexOf(" "));
			}

			// Add support for TLS 1.2 and 1.3, so that HTTPS connection to GitHub succeeds
			if (ServicePointManager.SecurityProtocol != 0 /* Default */)
			{
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)0x3000 /* Tls13 */;
			}

			string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();

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
						switch (args[++i])
						{
							case "d3d9":
								currentInfo.targetApi = Api.D3D9;
								break;
							case "d3d10":
								currentInfo.targetApi = Api.D3D10;
								break;
							case "d3d11":
								currentInfo.targetApi = Api.D3D11;
								break;
							case "d3d12":
								currentInfo.targetApi = Api.D3D12;
								break;
							case "dxgi":
								currentInfo.targetApi = Api.DXGI;
								break;
							case "ddraw":
								currentInfo.targetApi = Api.DDraw;
								break;
							case "opengl":
								currentInfo.targetApi = Api.OpenGL;
								break;
							case "vulkan":
								currentInfo.targetApi = Api.Vulkan;
								break;
							case "openxr":
								currentInfo.targetOpenXR = true;
								break;
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
						switch (args[++i])
						{
							case "finished":
								currentOperation = InstallOperation.Finished;
								break;
							case "update":
								currentOperation = InstallOperation.Update;
								break;
							case "modify":
								currentOperation = InstallOperation.UpdateWithEffects;
								break;
							case "uninstall":
								currentOperation = InstallOperation.Uninstall;
								break;
						}
					}
				}

				if (File.Exists(args[i]))
				{
					currentInfo.targetPath = args[i];
					currentInfo.targetName = Path.GetFileNameWithoutExtension(currentInfo.targetPath);
				}
			}

			if (currentInfo.targetPath != null)
			{
				if (currentOperation == InstallOperation.Finished)
				{
					InstallStep_Finish();
				}
				else if (currentInfo.targetApi != Api.Unknown)
				{
					RunTaskWithExceptionHandling(() =>
					{
						var peInfo = new PEInfo(currentInfo.targetPath);
						currentInfo.is64Bit = peInfo.Type == PEInfo.BinaryType.IMAGE_FILE_MACHINE_AMD64;

						InstallStep_CheckExistingInstallation();
					});
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
				MessageBox.Show(this, "此ReShade修改版本旨在用于单人游戏，在某些多人在线游戏中可能导致账号封禁。\n*此外，AuroraShade仅针对《最终幻想14》进行了测试，不保证在其他游戏中的可用性。", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
#endif
			}
		}

		readonly bool isHeadless = false;
		readonly bool isElevated = WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
		static readonly string commonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ReShade");

		IniFile compatibilityIni;

		readonly StatusPage status = new StatusPage();
		readonly SelectAppPage appPage = new SelectAppPage();

		struct InstallInfo
		{
			public bool is64Bit;
			public Api targetApi;
			public bool targetOpenXR;
			public string targetPath, targetName;
			public string modulePath, configPath, presetPath;
		}

		InstallInfo currentInfo = new InstallInfo();
		InstallOperation currentOperation = InstallOperation.Install;

		static void MoveFiles(string sourcePath, string targetPath)
		{
			if (Directory.Exists(targetPath) == false)
			{
				Directory.CreateDirectory(targetPath);
			}

			foreach (string source in Directory.EnumerateFiles(sourcePath))
			{
				string ext = Path.GetExtension(source);
				if (ext == ".addon" || ext == ".addon32" || ext == ".addon64")
				{
					continue;
				}

				string target = targetPath + source.Replace(sourcePath, string.Empty);

				File.Copy(source, target, true);
			}

			// Copy files recursively
			foreach (string source in Directory.EnumerateDirectories(sourcePath))
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

				FileSecurity access = File.GetAccessControl(targetPath);
				access.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.Modify, AccessControlType.Allow));
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
				isReShade = FileVersionInfo.GetVersionInfo(path).ProductName == "ReShade"
					|| FileVersionInfo.GetVersionInfo(path).ProductName == "AuroraShade";
				return true;
			}
			else
			{
				isReShade = false;
				return false;
			}
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
			if (searchPaths.Any(searchPath => searchPath.EndsWith(wildcard) && newPath.StartsWith(searchPath.Remove(searchPath.Length - 1 - wildcard.Length))))
			{
				return;
			}

			// Filter out invalid search paths (and those with remaining wildcards that were not handled above)
			var validSearchPaths = searchPaths.Where(searchPath => searchPath.IndexOfAny(Path.GetInvalidPathChars()) < 0 && searchPath.IndexOf('*') < 0);

			// Avoid adding duplicate search paths (relative or absolute)
			if (validSearchPaths.Any(searchPath => Path.GetFullPath(searchPath) == Path.GetFullPath(newPath)))
			{
				return;
			}

			searchPaths.Add(newPath);
		}
		void WriteSearchPaths(string targetPathEffects, string targetPathTextures)
		{
			string basePath = Path.GetDirectoryName(currentInfo.configPath);

			// Change current directory so that "Path.GetFullPath" resolves correctly
			string currentPath = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(basePath);

			var iniFile = new IniFile(currentInfo.configPath);
			List<string> paths = null;

			if (!string.IsNullOrEmpty(targetPathEffects))
			{
				iniFile.GetValue("GENERAL", "EffectSearchPaths", out string[] effectSearchPaths);

				paths = new List<string>(effectSearchPaths ?? new string[0]);
				paths.RemoveAll(string.IsNullOrWhiteSpace);

				AddSearchPath(paths, targetPathEffects);
				iniFile.SetValue("GENERAL", "EffectSearchPaths", paths.ToArray());
			}

			if (!string.IsNullOrEmpty(targetPathTextures))
			{
				iniFile.GetValue("GENERAL", "TextureSearchPaths", out string[] textureSearchPaths);

				paths = new List<string>(textureSearchPaths ?? new string[0]);
				paths.RemoveAll(string.IsNullOrWhiteSpace);

				AddSearchPath(paths, targetPathTextures);
				iniFile.SetValue("GENERAL", "TextureSearchPaths", paths.ToArray());
			}

			iniFile.SaveFile();

			Directory.SetCurrentDirectory(currentPath);
		}
		void GetEffectSearchPaths(out List<KeyValuePair<string, bool>> searchPaths)
		{
			const string wildcard = "**";

			string basePath = Path.GetDirectoryName(currentInfo.configPath);

			// Change current directory so that "Path.GetFullPath" resolves correctly
			string currentPath = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(basePath);

			var iniFile = new IniFile(currentInfo.configPath);

			if (iniFile.GetValue("GENERAL", "EffectSearchPaths", out string[] effectSearchPaths))
			{
				searchPaths = effectSearchPaths
					.Where(searchPath => !string.IsNullOrWhiteSpace(searchPath))
					.Select(searchPath => searchPath.EndsWith(wildcard) ? new KeyValuePair<string, bool>(searchPath.Remove(searchPath.Length - 1 - wildcard.Length), true) : new KeyValuePair<string, bool>(searchPath, false))
					.Where(searchPath => searchPath.Key.IndexOfAny(Path.GetInvalidPathChars()) < 0 && searchPath.Key.IndexOf('*') < 0)
					.Select(searchPath => new KeyValuePair<string, bool>(Path.GetFullPath(searchPath.Key), searchPath.Value))
					.ToList();
			}
			else
			{
				searchPaths = new List<KeyValuePair<string, bool>>();
			}

			Directory.SetCurrentDirectory(currentPath);
		}

		void ResetStatus()
		{
			currentInfo = new InstallInfo();
			currentOperation = InstallOperation.Install;

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
			currentOperation = InstallOperation.Finished;

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

		void RunTaskWithExceptionHandling(Action action)
		{
			Task.Run(action).ContinueWith(c => UpdateStatusAndFinish(false, "安装过程中出现未处理的异常：\n" + c.Exception.InnerException.Message + "\n\n" + c.Exception.InnerException.StackTrace), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
		}

		bool RestartWithElevatedPrivileges()
		{
			var startInfo = new ProcessStartInfo
			{
				Verb = "runas",
				FileName = Assembly.GetExecutingAssembly().Location,
				Arguments = $"\"{currentInfo.targetPath}\" --elevated --left {Left} --top {Top}"
			};

			switch (currentInfo.targetApi)
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
				case Api.DDraw:
					startInfo.Arguments += " --api ddraw";
					break;
				case Api.OpenGL:
					startInfo.Arguments += " --api opengl";
					break;
				case Api.Vulkan:
					startInfo.Arguments += " --api vulkan";
					break;
			}

			if (currentInfo.targetOpenXR)
			{
				startInfo.Arguments += " --api openxr";
			}

			switch (currentOperation)
			{
				case InstallOperation.Finished:
					startInfo.Arguments += " --state finished";
					break;
				case InstallOperation.Update:
					startInfo.Arguments += " --state update";
					break;
				case InstallOperation.UpdateWithEffects:
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
					using (Stream compatibilityStream = client.OpenRead("https://raw.githubusercontent.com/crosire/reshade-shaders/list/Compatibility.ini"))
					{
						compatibilityIni = new IniFile(compatibilityStream);
					}
				}
				catch (WebException)
				{
					// Ignore if this list failed to download, since setup can still proceed without them
				}
			}
		}

		void InstallStep_AnalyzeExecutable()
		{
			UpdateStatus("分析可执行文件...");

			// In case this is the bootstrap executable of an Unreal Engine game, try and find the actual game executable for it
			string targetPathUnrealEngine = PEInfo.ReadResourceString(currentInfo.targetPath, 201); // IDI_EXEC_FILE (see BootstrapPackagedGame.cpp in Unreal Engine source code)
			if (!string.IsNullOrEmpty(targetPathUnrealEngine) && File.Exists(targetPathUnrealEngine = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(currentInfo.targetPath), targetPathUnrealEngine))))
			{
				currentInfo.targetPath = targetPathUnrealEngine;
			}

			var info = FileVersionInfo.GetVersionInfo(currentInfo.targetPath);
			currentInfo.targetName = info.FileDescription;
			if (currentInfo.targetName is null || currentInfo.targetName.Trim().Length == 0)
			{
				currentInfo.targetName = Path.GetFileNameWithoutExtension(currentInfo.targetPath);
				if (char.IsLower(currentInfo.targetName[0]))
				{
					currentInfo.targetName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(currentInfo.targetName);
				}
			}

			var peInfo = new PEInfo(currentInfo.targetPath);
			currentInfo.is64Bit = peInfo.Type == PEInfo.BinaryType.IMAGE_FILE_MACHINE_AMD64;

			bool isApiD3D9 = false;
			bool isApiDXGI = false;
			bool isApiDDraw = false;
			bool isApiOpenGL = false;
			bool isApiVulkan = false;
			currentInfo.targetOpenXR = false;

			string basePath = Path.GetDirectoryName(currentInfo.targetPath);
			if (basePath.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows)))
			{
				UpdateStatusAndFinish(false, "Installation to the Windows directory is prohibited.");
				return;
			}

			// Check whether the API is specified in the compatibility list, in which case setup can continue right away
			string executableName = Path.GetFileName(currentInfo.targetPath);
			if (compatibilityIni?.GetString(executableName, "Banned") == "1")
			{
				UpdateStatusAndFinish(false, "The target application is known to have blocked or banned the usage of ReShade. Cannot continue installation.");
				return;
			}

			if (compatibilityIni != null && compatibilityIni.HasValue(executableName, "RenderApi"))
			{
				if (compatibilityIni.HasValue(executableName, "InstallTarget"))
				{
					basePath = Path.Combine(basePath, compatibilityIni.GetString(executableName, "InstallTarget"));
				}

				string api = compatibilityIni.GetString(executableName, "RenderApi");

				if (api == "D3D8" || api == "D3D9")
				{
					isApiD3D9 = true;
				}
				else if (api == "D3D10" || api == "D3D11" || api == "D3D12" || api == "DXGI")
				{
					isApiDXGI = true;
				}
				else if (api == "DDraw")
				{
					isApiDDraw = true;
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
				isApiDDraw = peInfo.Modules.Any(s => s.StartsWith("ddraw", StringComparison.OrdinalIgnoreCase));
				isApiOpenGL = peInfo.Modules.Any(s => s.StartsWith("opengl32", StringComparison.OrdinalIgnoreCase));
				isApiVulkan = peInfo.Modules.Any(s => s.StartsWith("vulkan-1", StringComparison.OrdinalIgnoreCase));
				// currentInfo.targetOpenXR = peInfo.Modules.Any(s => s.StartsWith("openxr_loader", StringComparison.OrdinalIgnoreCase));

				if (isApiD3D9 && isApiDXGI)
				{
					isApiD3D9 = false; // Prefer DXGI over D3D9
				}
				if (isApiD3D8 && !isHeadless)
				{
					UpdateStatus("等待用户确认...");

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

			// In case this game is modded with NVIDIA RTX Remix, install to the Remix Bridge
			string targetPathRemixBridge = Path.Combine(basePath, ".trex", "NvRemixBridge.exe");
			if (File.Exists(targetPathRemixBridge))
			{
				isApiVulkan = true;
				currentInfo.is64Bit = true;
				currentInfo.targetPath = targetPathRemixBridge;
			}

			if (isApiVulkan)
			{
				currentInfo.targetApi = Api.Vulkan;
			}
			else if (isApiD3D9)
			{
				currentInfo.targetApi = Api.D3D9;
			}
			else if (isApiDXGI)
			{
				currentInfo.targetApi = Api.DXGI;
			}
			else if (isApiOpenGL)
			{
				currentInfo.targetApi = Api.OpenGL;
			}
			else if (isApiDDraw)
			{
				currentInfo.targetApi = Api.DDraw;
			}

			if (isHeadless)
			{
				InstallStep_CheckExistingInstallation();
				return;
			}

			Dispatcher.Invoke(() =>
			{
				CurrentPage.Navigate(new SelectApiPage(currentInfo.targetName)
				{
					SelectedApi = currentInfo.targetApi,
					SelectedOpenXR = currentInfo.targetOpenXR
				});
			});
		}
		void InstallStep_CheckExistingInstallation()
		{
			DownloadCompatibilityIni();

			UpdateStatus("检查安装状态...");

			string basePath = Path.GetDirectoryName(currentInfo.targetPath);
			string executableName = Path.GetFileName(currentInfo.targetPath);

			if (currentInfo.targetApi != Api.Vulkan && compatibilityIni != null)
			{
				if (compatibilityIni.HasValue(executableName, "InstallTarget"))
				{
					basePath = Path.Combine(basePath, compatibilityIni.GetString(executableName, "InstallTarget"));

					var globalConfig = new IniFile(Path.Combine(Path.GetDirectoryName(currentInfo.targetPath), "ReShade.ini"));
					globalConfig.SetValue("INSTALL", "BasePath", basePath);
					globalConfig.SaveFile();
				}

				if (compatibilityIni.GetString(executableName, "ForceInstallApi") == "1")
				{
					string api = compatibilityIni.GetString(executableName, "RenderApi");

					if (api == "D3D8" || api == "D3D9")
					{
						currentInfo.targetApi = Api.D3D9;
					}
					else if (api == "D3D10")
					{
						currentInfo.targetApi = Api.D3D10;
					}
					else if (api == "D3D11")
					{
						currentInfo.targetApi = Api.D3D11;
					}
					else if (api == "D3D12")
					{
						currentInfo.targetApi = Api.D3D12;
					}
					else if (api == "DDraw")
					{
						currentInfo.targetApi = Api.DDraw;
					}
					else if (api == "OpenGL")
					{
						currentInfo.targetApi = Api.OpenGL;
					}
				}
			}

			currentInfo.configPath = Path.Combine(basePath, "ReShade.ini");

			bool isReShade = false;

			if (currentInfo.targetApi == Api.Vulkan || currentInfo.targetOpenXR)
			{
				string moduleName = currentInfo.is64Bit ? "ReShade64" : "ReShade32";
				currentInfo.modulePath = Path.Combine(commonPath, moduleName, moduleName + ".dll");

				if (currentOperation == InstallOperation.Install && File.Exists(currentInfo.configPath))
				{
					if (isHeadless)
					{
						UpdateStatusAndFinish(false, "发现已有ReShade安装，请先卸载。");
					}
					else
					{
						Dispatcher.Invoke(() =>
						{
							CurrentPage.Navigate(new SelectOperationPage());
						});
					}
					return;
				}
			}
			else
			{
				switch (currentInfo.targetApi)
				{
					case Api.D3D9:
						currentInfo.modulePath = "d3d9.dll";
						break;
					case Api.D3D10:
						currentInfo.modulePath = "d3d10.dll";
						break;
					case Api.D3D11:
						currentInfo.modulePath = "d3d11.dll";
						break;
					case Api.D3D12:
						currentInfo.modulePath = "d3d12.dll";
						break;
					case Api.DXGI:
						currentInfo.modulePath = "dxgi.dll";
						break;
					case Api.DDraw:
						currentInfo.modulePath = "ddraw.dll";
						break;
					case Api.OpenGL:
						currentInfo.modulePath = "opengl32.dll";
						break;
					default: // No API selected, abort immediately
						UpdateStatusAndFinish(false, "无法探测该应用程序使用的渲染API。");
						return;
				}

				currentInfo.modulePath = Path.Combine(basePath, currentInfo.modulePath);

				if (currentOperation == InstallOperation.Install && ModuleExists(currentInfo.modulePath, out isReShade))
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
							CurrentPage.Navigate(new SelectOperationPage());
						});
					}
					else
					{
						UpdateStatusAndFinish(false, Path.GetFileName(currentInfo.modulePath) + "已经存在，但并非来自ReShade。\n请确认它不是游戏所需的系统文件。");
					}
					return;
				}
			}

			foreach (string conflictingModuleName in new[] { "d3d9.dll", "d3d10.dll", "d3d11.dll", "d3d12.dll", "dxgi.dll", "opengl32.dll" })
			{
				string conflictingModulePath = Path.Combine(basePath, conflictingModuleName);

				if (currentOperation == InstallOperation.Install && ModuleExists(conflictingModulePath, out isReShade) && isReShade)
				{
					if (isHeadless)
					{
						UpdateStatusAndFinish(false, "已有用于其他API的ReShade安装。\n不支持同时安装多个ReShade，请先卸载已有的安装。");
					}
					else
					{
						Dispatcher.Invoke(() =>
						{
							CurrentPage.Navigate(new SelectOperationPage());
						});
					}
					return;
				}
			}

			if (currentOperation != InstallOperation.Uninstall)
			{
				InstallStep_InstallReShadeModule();
			}
			else
			{
				InstallStep_UninstallReShadeModule();
			}
		}
		void InstallStep_InstallReShadeModule()
		{
			UpdateStatus("安装ReShade中...");

			ZipArchive zip;

			try
			{
				// Extract archive attached to this executable
				MemoryStream output;

				using (FileStream input = File.OpenRead(Assembly.GetExecutingAssembly().Location))
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
			catch (InvalidDataException)
			{
				UpdateStatusAndFinish(false, "此安装包文件已损坏！请前往 https://reshade.me 下载官方版，或重新下载AuroraShade！");
				return;
			}

			string basePath = Path.GetDirectoryName(currentInfo.configPath);

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
				catch (SystemException)
				{
					// Ignore errors
					continue;
				}
			}

			if (currentInfo.targetApi == Api.Vulkan || currentInfo.targetOpenXR)
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

					// Make sure the DLLs will have permissions set up for 'ALL_APPLICATION_PACKAGES', so that loading the layer won't fail in UWP apps
					var sid = new SecurityIdentifier("S-1-15-2-1");

					DirectorySecurity access = Directory.GetAccessControl(commonPath);
					access.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
					Directory.SetAccessControl(commonPath, access);
				}
				catch (SystemException ex)
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
				catch (SystemException)
				{
					// Ignore errors
				}

				foreach (string layerModuleName in new[] { "ReShade32", "ReShade64" })
				{
					string layerModulePath = Path.Combine(commonPath, layerModuleName + ".dll");

					try
					{
						ZipArchiveEntry module = zip.GetEntry(Path.GetFileName(layerModulePath)) ?? throw new FileFormatException("安装包缺少ReShade DLL主程序。");
						module.ExtractToFile(layerModulePath, true);
					}
					catch (SystemException ex)
					{
						UpdateStatusAndFinish(false, "安装" + Path.GetFileName(layerModulePath) + "失败：\n" + ex.Message);
						return;
					}

					if (currentInfo.targetApi == Api.Vulkan)
					{
						string layerManifestPath = Path.Combine(commonPath, layerModuleName + ".json");

						try
						{
							ZipArchiveEntry manifest = zip.GetEntry(Path.GetFileName(layerManifestPath)) ?? throw new FileFormatException("安装包缺少Vulkan层清单文件。");
							manifest.ExtractToFile(layerManifestPath, true);

							// Register this layer manifest
							using (RegistryKey key = Registry.LocalMachine.CreateSubKey(Environment.Is64BitOperatingSystem && layerModuleName == "ReShade32" ? @"Software\Wow6432Node\Khronos\Vulkan\ImplicitLayers" : @"Software\Khronos\Vulkan\ImplicitLayers"))
							{
								key.SetValue(layerManifestPath, 0, RegistryValueKind.DWord);
							}
						}
						catch (SystemException ex)
						{
							UpdateStatusAndFinish(false, "安装Vulkan层清单文件失败：\n" + ex.Message);
							return;
						}
					}

					if (currentInfo.targetOpenXR)
					{
						string layerManifestPathXR = Path.Combine(commonPath, layerModuleName + "_XR.json");

						try
						{
							ZipArchiveEntry manifest = zip.GetEntry(Path.GetFileName(layerManifestPathXR)) ?? throw new FileFormatException("安装包缺少OpenXR层清单文件。");
							manifest.ExtractToFile(layerManifestPathXR, true);

							// Register this layer manifest
							using (RegistryKey key = Registry.LocalMachine.CreateSubKey(Environment.Is64BitOperatingSystem && layerModuleName == "ReShade32" ? @"Software\Wow6432Node\Khronos\OpenXR\1\ApiLayers\Implicit" : @"Software\Khronos\OpenXR\1\ApiLayers\Implicit"))
							{
								key.SetValue(layerManifestPathXR, 0, RegistryValueKind.DWord);
							}
						}
						catch (SystemException ex)
						{
							UpdateStatusAndFinish(false, "安装OpenXR层清单文件失败：\n" + ex.Message);
							return;
						}
					}
				}

				var appConfig = new IniFile(Path.Combine(commonPath, "ReShadeApps.ini"));
				if (appConfig.GetValue(string.Empty, "Apps", out string[] appKeys) == false || !appKeys.Contains(currentInfo.targetPath))
				{
					List<string> appKeysList = appKeys?.ToList() ?? new List<string>();
					appKeysList.Add(currentInfo.targetPath);

					appConfig.SetValue(string.Empty, "Apps", appKeysList.ToArray());
					appConfig.SaveFile();
				}
			}
			else
			{
				string parentPath = Path.GetDirectoryName(currentInfo.modulePath);

				try
				{
					ZipArchiveEntry module = zip.GetEntry(currentInfo.is64Bit ? "ReShade64.dll" : "ReShade32.dll") ?? throw new FileFormatException("安装包缺少ReShade DLL主程序。");
					module.ExtractToFile(currentInfo.modulePath, true);
				}
				catch (SystemException ex)
				{
					UpdateStatusAndFinish(false, "安装" + Path.GetFileName(currentInfo.modulePath) + "失败：\n" + ex.Message +
							(currentOperation != InstallOperation.Install ? "\n\n请确保目标程序已经停止运行。" : string.Empty));
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

4) [AUR] 对于FF14，尝试将安装出的dxgi.dll更名为d3d11.dll。

5) 如果以上所有都没有作用，可以前往ReShade官方论坛https://reshade.me/forum获取帮助。但请在发帖前搜索你遇到的问题，可能别人已经解决了。
   [AUR] 你也可以参考随附的PDF教程，联系汉化整合包作者 路障MKXX 获得帮助。
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
			if (!File.Exists(currentInfo.configPath))
			{
				try
				{
					foreach (string premadeConfigPath in new[] { "ReShade.ini", Path.Combine(Path.GetDirectoryName(currentInfo.configPath), "GShade.ini") })
					{
						if (File.Exists(premadeConfigPath))
						{
							File.Copy(premadeConfigPath, currentInfo.configPath);
							break;
						}
					}
				}
				catch (SystemException ex)
				{
					UpdateStatusAndFinish(false, "安装" + Path.GetFileName(currentInfo.configPath) + "失败：\n" + ex.Message);
					return;
				}
			}

			// Add default configuration
			var config = new IniFile(currentInfo.configPath);

			// offline compatibilityIni
			if (compatibilityIni == null)
			{
				var tmp = Path.Combine(SetupConfig.CN2PackDir, "Compatibility.ini");
				compatibilityIni = File.Exists(tmp) ? new IniFile(tmp) : null;
				if (currentInfo.targetName == "FINAL FANTASY XIV")
				{
					config.SetValue("GENERAL", "PreprocessorDefinitions",
						"RESHADE_DEPTH_LINEARIZATION_FAR_PLANE=1000.0",
						"RESHADE_DEPTH_INPUT_IS_UPSIDE_DOWN=" + "0",
						"RESHADE_DEPTH_INPUT_IS_REVERSED=" + "0",
						"RESHADE_DEPTH_INPUT_IS_LOGARITHMIC=" + "0");
					config.SetValue("DEPTH", "UseAspectRatioHeuristics", "1");
					config.SetValue("DEPTH", "DepthCopyBeforeClears", "1");
				}
			}
			if (compatibilityIni != null && !config.HasValue("GENERAL", "PreprocessorDefinitions"))
			{
				string executableName = Path.GetFileName(currentInfo.targetPath);

				string depthReversed = compatibilityIni.GetString(executableName, "DepthReversed", "0");
				string depthUpsideDown = compatibilityIni.GetString(executableName, "DepthUpsideDown", "0");
				string depthLogarithmic = compatibilityIni.GetString(executableName, "DepthLogarithmic", "0");
				if (!compatibilityIni.HasValue(executableName, "DepthReversed"))
				{
					var info = FileVersionInfo.GetVersionInfo(currentInfo.targetPath);
					if (info.LegalCopyright != null)
					{
						Match match = new Regex(@"(20[0-9]{2})", RegexOptions.RightToLeft).Match(info.LegalCopyright);
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

				if (compatibilityIni.HasValue(executableName, "DepthCopyBeforeClears") ||
					compatibilityIni.HasValue(executableName, "DepthCopyAtClearIndex") ||
					compatibilityIni.HasValue(executableName, "UseAspectRatioHeuristics"))
				{
					config.SetValue("DEPTH", "DepthCopyBeforeClears",
						compatibilityIni.GetString(executableName, "DepthCopyBeforeClears", "0"));
					config.SetValue("DEPTH", "DepthCopyAtClearIndex",
						compatibilityIni.GetString(executableName, "DepthCopyAtClearIndex", "0"));
					config.SetValue("DEPTH", "UseAspectRatioHeuristics",
						compatibilityIni.GetString(executableName, "UseAspectRatioHeuristics", "1"));
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

			// Always add input section
			if (!config.HasValue("INPUT"))
			{
				config.SetValue("INPUT", "KeyOverlay", "48,1,0,0");
				// Only enable gamepad input in cases where keyboard and mouse input is known to not work (when installed to UWP apps or the NVIDIA RTX Remix Bridge)
				config.SetValue("INPUT", "GamepadNavigation", currentInfo.targetPath.Contains("WindowsApps") || Path.GetFileName(currentInfo.targetPath) == "NvRemixBridge.exe" ? "1" : "0");
			}

			// [CN2] Add default font and sound effects
			if (SetupConfig.SCFontPath != null && !config.HasValue("STYLE", "Font"))
			{
				config.SetValue("STYLE", "Font", ".\\" + SetupConfig.SCFontName);
				config.SetValue("STYLE", "FontSize", "16");
				config.SetValue("STYLE", "LatinFont", ".\\" + SetupConfig.SCFontName);
				config.SetValue("STYLE", "EditorFont", ".\\" + SetupConfig.SCFontName);
				config.SetValue("STYLE", "EditorFontSize", "16");
			}
			if (SetupConfig.ShutterSEPath != null && !config.HasValue("SCREENSHOT", "SoundPath"))
			{
				config.SetValue("SCREENSHOTS", "SoundPath", ".\\" + SetupConfig.ShutterSEName);
			}
			// Add default search paths if no config exists
			if (!config.HasValue("GENERAL", "EffectSearchPaths") || config.GetString("GENERAL", "EffectSearchPaths") == string.Empty)
			{
				config.SetValue("GENERAL", "EffectSearchPaths", ".\\reshade-shaders\\Shaders\\**");
			}
			if (!config.HasValue("GENERAL", "TextureSearchPaths") || config.GetString("GENERAL", "TextureSearchPaths") == string.Empty)
			{
				config.SetValue("GENERAL", "TextureSearchPaths", ".\\reshade-shaders\\Textures\\**");
			}

			config.SaveFile();

			// Change file permissions for files ReShade needs write access to
			MakeWritable(currentInfo.configPath);
			MakeWritable(Path.Combine(Path.GetDirectoryName(currentInfo.targetPath), "ReShade.log"));
			MakeWritable(Path.Combine(basePath, "ReShadePreset.ini"));

			if (!isHeadless && currentOperation != InstallOperation.Update)
			{
				currentInfo.presetPath = config.GetString("GENERAL", "PresetPath", string.Empty);

				if (!string.IsNullOrEmpty(currentInfo.presetPath))
				{
					// Change current directory so that "Path.GetFullPath" resolves correctly
					string currentPath = Directory.GetCurrentDirectory();
					Directory.SetCurrentDirectory(basePath);
					currentInfo.presetPath = Path.GetFullPath(currentInfo.presetPath);
					Directory.SetCurrentDirectory(currentPath);
				}

				Dispatcher.Invoke(() =>
				{
					CurrentPage.Navigate(new SelectEffectsPage
					{
						PresetPath = currentInfo.presetPath
					});
				});
				return;
			}

			// Add default search paths if no config exists
			if (!config.HasValue("GENERAL", "EffectSearchPaths") && !config.HasValue("GENERAL", "TextureSearchPaths"))
			{
				WriteSearchPaths(".\\reshade-shaders\\Shaders", ".\\reshade-shaders\\Textures");
			}

			config.SaveFile();
			InstallStep_Finish();
		}
		void InstallStep_UninstallReShadeModule()
		{
			if (currentInfo.targetApi == Api.Vulkan || currentInfo.targetOpenXR)
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
					List<string> appKeysList = appKeys.ToList();
					appKeysList.Remove(currentInfo.targetPath);
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

							using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"Software\Khronos\OpenXR\1\ApiLayers\Implicit"))
							{
								key.DeleteValue(Path.Combine(commonPath, "ReShade32_XR.json"), false);
								key.DeleteValue(Path.Combine(commonPath, "ReShade64_XR.json"), false);
							}

							if (Environment.Is64BitOperatingSystem)
							{
								using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"Software\Wow6432Node\Khronos\Vulkan\ImplicitLayers"))
								{
									key.DeleteValue(Path.Combine(commonPath, "ReShade32.json"), false);
								}

								using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"Software\Wow6432Node\Khronos\OpenXR\1\ApiLayers\Implicit"))
								{
									key.DeleteValue(Path.Combine(commonPath, "ReShade32_XR.json"), false);
								}
							}
						}
						catch (SystemException ex)
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
				string basePath = Path.GetDirectoryName(currentInfo.configPath);

				if (currentInfo.targetApi != Api.Vulkan && !currentInfo.targetOpenXR)
				{
					File.Delete(currentInfo.modulePath);
				}

				// Read search paths from config file before deleting it
				GetEffectSearchPaths(out List<KeyValuePair<string, bool>> effectSearchPaths);

				if (File.Exists(currentInfo.configPath))
				{
					File.Delete(currentInfo.configPath);
				}

				if (File.Exists(Path.Combine(Path.GetDirectoryName(currentInfo.targetPath), "ReShade.log")))
				{
					File.Delete(Path.Combine(Path.GetDirectoryName(currentInfo.targetPath), "ReShade.log"));
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

				foreach (KeyValuePair<string, bool> searchPath in effectSearchPaths)
				{
					if (searchPath.Key.StartsWith(basePath) && Directory.Exists(searchPath.Key))
					{
						string[] extensions = { "*.fx", "*.fxh" };

						foreach (string effectFile in extensions.SelectMany(ext => Directory.EnumerateFiles(searchPath.Key, ext, searchPath.Value ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)))
						{
							File.Delete(effectFile);
						}

						foreach (string parentPath in Directory.EnumerateDirectories(searchPath.Key, "*", searchPath.Value ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Union(new string[] { searchPath.Key }).OrderByDescending(path => path.Length))
						{
							if (Directory.EnumerateFiles(parentPath, "*", SearchOption.AllDirectories).Any() == false)
							{
								Directory.Delete(parentPath, false);
							}
						}
					}
				}

				// Delete add-ons
				foreach (string addonFile in Directory.EnumerateFiles(Path.GetDirectoryName(currentInfo.targetPath), currentInfo.is64Bit ? "*.addon64" : "*.addon32", SearchOption.TopDirectoryOnly))
				{
					File.Delete(addonFile);
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
			catch (SystemException ex)
			{
				UpdateStatusAndFinish(false, "删除部分ReShade文件失败：\n" + ex.Message +
					(currentOperation != InstallOperation.Install ? "\n\n请确保目标程序已经停止运行！" : string.Empty));
				return;
			}

			InstallStep_Finish();
		}
		void InstallStep_CheckPreset()
		{
			if (!string.IsNullOrEmpty(currentInfo.presetPath) && File.Exists(currentInfo.presetPath))
			{
				string basePath = Path.GetDirectoryName(currentInfo.configPath);
				string presetPath = currentInfo.presetPath;
				if (presetPath.StartsWith(basePath))
				{
					// Try and make preset path relative
					presetPath = "." + presetPath.Substring(basePath.Length);
				}

				var config = new IniFile(currentInfo.configPath);
				config.SetValue("GENERAL", "PresetPath", presetPath);
				config.SaveFile();

				MakeWritable(currentInfo.presetPath);
			}
		}
		void InstallStep_DownloadEffectPackage(EffectPackage package)
		{
			UpdateStatus("从" + package.Name + "下载" + package.DownloadUrl + " ...");

			string downloadPath = Path.Combine(Path.GetTempPath(), "ReShadeSetupDownload.tmp");

			using (var client = new WebClient())
			{
				client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
				{
					// Avoid negative percentage values
					if (e.TotalBytesToReceive > 0)
					{
						UpdateStatus("下载" + package.Name + " ... (" + ((100 * e.BytesReceived) / e.TotalBytesToReceive) + "%)");
					}
				};

				try
				{
					client.DownloadFile(new Uri(package.DownloadUrl), downloadPath);
				}
				catch (WebException ex)
				{
					UpdateStatusAndFinish(false, "从" + package.DownloadUrl + "下载失败：\n" + ex.Message);
					return;
				}
			}

			InstallStep_InstallEffectPackage(package, downloadPath);
		}
		void InstallStep_InstallEffectPackage(EffectPackage package, string downloadPath)
		{
			UpdateStatus("Extracting " + package.Name + " ...");

			string tempPath = Path.Combine(Path.GetTempPath(), "ReShadeSetup");
			string tempPathEffects = null;
			string tempPathTextures = null;

			string basePath = Path.GetDirectoryName(currentInfo.configPath);
			string targetPathEffects = Path.GetFullPath(Path.Combine(basePath, package.InstallPath));
			string targetPathTextures = Path.GetFullPath(Path.Combine(basePath, package.TextureInstallPath));

			IEnumerable<string> effects = null;

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
				tempPathEffects = Directory.EnumerateDirectories(tempPath, "Shaders", SearchOption.AllDirectories).FirstOrDefault();
				tempPathTextures = Directory.EnumerateDirectories(tempPath, "Textures", SearchOption.AllDirectories).FirstOrDefault();

				// If that does not exist, look for the first directory that contains shaders/textures
				if (tempPathEffects == null)
				{
					tempPathEffects = effects.Select(x => Path.GetDirectoryName(x)).OrderBy(x => x.Length).FirstOrDefault();
				}
				if (tempPathTextures == null)
				{
					string[] extensions = { "*.png", "*.jpg", "*.jpeg" };

					string path = extensions.SelectMany(ext => Directory.EnumerateFiles(tempPath, ext, SearchOption.AllDirectories)).Select(x => Path.GetDirectoryName(x)).OrderBy(x => x.Length).FirstOrDefault();
					if (!string.IsNullOrEmpty(path))
					{
						tempPathTextures = tempPathTextures != null ? tempPathTextures.Union(path).ToString() : path;
					}
				}

				// Skip any effects not in the shader directory
				effects = effects.Where(filePath => filePath.StartsWith(tempPathEffects));

				// Delete denied effects
				if (package.DenyEffectFiles != null)
				{
					var denyEffectFiles = effects.Where(effectPath => package.DenyEffectFiles.Contains(Path.GetFileName(effectPath)));

					foreach (string effectPath in denyEffectFiles)
					{
						File.Delete(effectPath);
					}

					effects = effects.Except(denyEffectFiles);
				}

				// Delete all unselected effects
				if (package.Selected == null)
				{
					var disabledEffectFiles = effects.Where(effectPath => package.EffectFiles.Any(effectFile => !effectFile.Selected && effectFile.FileName == Path.GetFileName(effectPath)));

					foreach (string effectPath in disabledEffectFiles)
					{
						File.Delete(effectPath);
					}

					effects = effects.Except(disabledEffectFiles);
				}
			}
			catch (SystemException ex)
			{
				UpdateStatusAndFinish(false, "提取" + package.Name + "失败：\n" + ex.Message);
				return;
			}

			GetEffectSearchPaths(out List<KeyValuePair<string, bool>> effectSearchPaths);

			try
			{
				var existingEffectFiles = effectSearchPaths
					.Where(searchPath => (searchPath.Value ? !targetPathEffects.StartsWith(searchPath.Key) : searchPath.Key != targetPathEffects) && Directory.Exists(searchPath.Key))
					.SelectMany(searchPath => Directory.EnumerateFiles(searchPath.Key, "*.fx", searchPath.Value ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
					.Where(effectPath => effects.Select(x => Path.GetFileName(x)).Contains(Path.GetFileName(effectPath)) == true);

				if (existingEffectFiles.Any())
				{
					string existingPathEffects = Path.GetDirectoryName(existingEffectFiles.First());

					bool overwriteExistingEffectFiles = false;
					if (!isHeadless)
					{
						UpdateStatus("等待用户确认 ...");

						Dispatcher.Invoke(() =>
						{
							if (MessageBox.Show(this, "效果文件" + string.Join(", ", existingEffectFiles.Select(x => Path.GetFileName(x))) + "位于 \"" + existingPathEffects + "\" 而不是默认位置 \"" + targetPathEffects + "\".\n\n是否要覆盖位于 \"" + existingPathEffects + "\"的文件？", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
							{
								overwriteExistingEffectFiles = true;
							}
						});
					}

					if (!overwriteExistingEffectFiles)
					{
						UpdateStatusAndFinish(false, "有一些效果文件位于非默认位置。请先移除它们，避免文件重复。");
						return;
					}

					targetPathEffects = existingPathEffects;
					package.InstallPath = null; // Prevent install path getting written to config below
				}

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
			catch (SystemException ex)
			{
				UpdateStatusAndFinish(false, "安装" + package.Name + "失败：\n" + ex.Message);
				return;
			}

			WriteSearchPaths(package.InstallPath, package.TextureInstallPath);
		}
		void InstallStep_AutoCN2_InstallEffectPackage(bool installPresets)
		{
			try
			{
				string tempPathEffects = Path.Combine(SetupConfig.CN2PackDir, @"reshade-shaders\Shaders");
				string tempPathTextures = Path.Combine(SetupConfig.CN2PackDir, @"reshade-shaders\Textures");
				string basePath = Path.GetDirectoryName(currentInfo.configPath);
				string targetPathEffects = Path.Combine(basePath, @"reshade-shaders\Shaders");
				string targetPathTextures = Path.Combine(basePath, @"reshade-shaders\Textures");
				var prevInstall = new bool[2] { Directory.Exists(targetPathEffects), Directory.Exists(targetPathTextures) };
				if (prevInstall[0] || prevInstall[1])
				{
					var AutoCN2Confirm = MessageBox.Show("似乎检测到了先前的效果器安装。\n选“是”将完全清理它们，并安装AuroraShade；\n选“否”则保持原样，你可能需要自行备份。", "提示", MessageBoxButton.YesNo);
					if (AutoCN2Confirm == MessageBoxResult.Yes)
					{
						if (prevInstall[0])
						{
							Directory.Delete(targetPathEffects, true);
						}
						if (prevInstall[1])
						{
							Directory.Delete(targetPathTextures, true);
						}
						MoveFiles(tempPathEffects, targetPathEffects);
						MoveFiles(tempPathTextures, targetPathTextures);
					}
				}
				else
				{
					MoveFiles(tempPathEffects, targetPathEffects);
					MoveFiles(tempPathTextures, targetPathTextures);
				}
				//var customPathEffects = Path.Combine(basePath, @"reshade-shaders\CustomShaders");
				//var customPathTextures = Path.Combine(basePath, @"reshade-shaders\CustomTextures");

				//if (!Directory.Exists(customPathEffects))
				//	Directory.CreateDirectory(customPathEffects);
				//if (!Directory.Exists(customPathTextures))
				//	Directory.CreateDirectory(customPathTextures);

				var tempPathPresets = Path.Combine(SetupConfig.CN2PackDir, @"reshade-presets");
				var targetPathPresets = Path.Combine(basePath, @"reshade-presets");
				if (Directory.Exists(targetPathPresets) && installPresets)
				{
					var AutoCN2Confirm = MessageBox.Show("似乎检测到了先前的预设安装。\n选“是”将完全清理它们，并安装AuroraShade预设；\n选“否”则保持原样，你可能需要自行备份。", "提示", MessageBoxButton.YesNo);
					if (AutoCN2Confirm == MessageBoxResult.Yes)
					{
						Directory.Delete(targetPathPresets, true);
						MoveFiles(tempPathPresets, targetPathPresets);
					}
				}
				else
				{
					MoveFiles(tempPathPresets, targetPathPresets);
				}
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "安装AuroraShade失败：\n" + ex.Message);
				return;
			}
		}
		void InstallStep_AutoCN2_InstallAddon(Addon addon, string downloadPath)
		{
			string addonPath = Path.GetDirectoryName(currentInfo.targetPath);

			var globalConfigPath = Path.Combine(addonPath, "ReShade.ini");
			if (File.Exists(globalConfigPath))
			{
				var globalConfig = new IniFile(globalConfigPath);
				addonPath = globalConfig.GetString("ADDON", "AddonPath", addonPath);
				if (!Directory.Exists(addonPath))
				{
					addonPath = Path.GetDirectoryName(currentInfo.targetPath);
				}
			}
			try
			{
				File.Copy(downloadPath, Path.Combine(addonPath, Path.GetFileName(downloadPath)), true);
			}
			catch (Exception ex)
			{
				UpdateStatusAndFinish(false, "安装" + addon.Name + "失败：\n" + ex.Message);
				return;
			}
		}
		void InstallStep_CheckAddons()
		{
#if RESHADE_ADDON
			if (!isHeadless)
			{
				Dispatcher.Invoke(() =>
				{
					CurrentPage.Navigate(new SelectAddonsPage(currentInfo.is64Bit));
				});
				return;
			}
#endif

			InstallStep_Finish();
		}
		void InstallStep_DownloadAddon(Addon addon)
		{
			UpdateStatus("从" + addon.Name + "下载" + addon.DownloadUrl + " ...");

			string downloadPath = Path.Combine(Path.GetTempPath(), "ReShadeSetupDownload.tmp");

			String tempPath = (addon.DownloadUrl);
			if (tempPath[0] == '@')
			{
				tempPath = tempPath.Trim('@');
				downloadPath = Path.Combine(SetupConfig.CN2PackDir, tempPath);
				InstallStep_AutoCN2_InstallAddon(addon, downloadPath);
				return;
			}

			using (var client = new WebClient())
			{
				client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
				{
					// Avoid negative percentage values
					if (e.TotalBytesToReceive > 0)
					{
						UpdateStatus("下载" + addon.Name + " ... (" + ((100 * e.BytesReceived) / e.TotalBytesToReceive) + "%)");
					}
				};

				try
				{
					client.DownloadFile(new Uri(addon.DownloadUrl), downloadPath);
				}
				catch (WebException ex)
				{
					UpdateStatusAndFinish(false, "从" + addon.DownloadUrl + "下载失败：\n" + ex.Message);
					return;
				}
			}

			InstallStep_InstallAddon(addon, downloadPath);
		}
		void InstallStep_InstallAddon(Addon addon, string downloadPath)
		{
			string ext = Path.GetExtension(new Uri(addon.DownloadUrl).AbsolutePath);

			string tempPath = null;
			string tempPathEffects = null;

			if (ext != ".addon" && ext != ".addon32" && ext != ".addon64")
			{
				UpdateStatus("Extracting " + addon.Name + " ...");

				tempPath = Path.Combine(Path.GetTempPath(), "reshade-addons");

				try
				{
					// Delete existing directories since extraction fails if the target is not empty
					if (Directory.Exists(tempPath))
					{
						Directory.Delete(tempPath, true);
					}

					ZipFile.ExtractToDirectory(downloadPath, tempPath);

					string addonPath = Directory.EnumerateFiles(tempPath, currentInfo.is64Bit ? "*.addon64" : "*.addon32", SearchOption.AllDirectories).FirstOrDefault();
					if (addonPath == null)
					{
						IEnumerable<string> addonPaths = Directory.EnumerateFiles(tempPath, "*.addon");
						if (addonPaths.Count() == 1)
						{
							addonPath = addonPaths.First();
						}
						else
						{
							addonPath = addonPaths.FirstOrDefault(x => x.Contains(currentInfo.is64Bit ? "x64" : "x86") || Path.GetFileNameWithoutExtension(x).EndsWith(currentInfo.is64Bit ? "64" : "32"));
						}
					}
					if (addonPath == null)
					{
						Directory.Delete(tempPath, true);
						File.Delete(downloadPath);

						throw new FileFormatException("插件安装包内没有找到插件。");
					}

					downloadPath = addonPath;

					// Check for any effect files to install
					IEnumerable<string> effects = Directory.EnumerateFiles(tempPath, "*.fx", SearchOption.TopDirectoryOnly);
					effects = effects.Concat(Directory.EnumerateFiles(tempPath, "*.addonfx", SearchOption.TopDirectoryOnly));

					tempPathEffects = effects.Select(x => Path.GetDirectoryName(x)).OrderBy(x => x.Length).FirstOrDefault();
				}
				catch (SystemException ex)
				{
					UpdateStatusAndFinish(false, "提取" + addon.Name + "失败：\n" + ex.Message);
					return;
				}
			}

			string basePath = Path.GetDirectoryName(currentInfo.configPath);
            string targetPathAddon = Path.GetDirectoryName(currentInfo.targetPath);
            string targetPathEffects = Path.GetFullPath(Path.Combine(basePath, addon.EffectInstallPath));

			string globalConfigPath = Path.Combine(targetPathAddon, "ReShade.ini");
			if (File.Exists(globalConfigPath))
			{
				var globalConfig = new IniFile(globalConfigPath);
				targetPathAddon = globalConfig.GetString("ADDON", "AddonPath", targetPathAddon);
			}

			try
			{
				File.Copy(downloadPath, Path.Combine(targetPathAddon, Path.GetFileNameWithoutExtension(tempPath != null ? downloadPath : new Uri(addon.DownloadUrl).AbsolutePath) + (currentInfo.is64Bit ? ".addon64" : ".addon32")), true);

				if (tempPathEffects != null)
				{
					MoveFiles(tempPathEffects, targetPathEffects);

					WriteSearchPaths(addon.EffectInstallPath, null);
				}

				File.Delete(downloadPath);
				if (tempPath != null)
				{
					Directory.Delete(tempPath, true);
				}
			}
			catch (SystemException ex)
			{
				UpdateStatusAndFinish(false, "安装" + addon.Name + "失败：\n" + ex.Message);
				return;
			}
		}
		void InstallStep_Finish()
		{
			UpdateStatusAndFinish(true, (currentOperation != InstallOperation.Uninstall ? "成功安装ReShade。" : "成功卸载ReShade。") +
				(isHeadless ? string.Empty : "\n点击“完成”按钮退出安装程序。"));
		}

		void OnWindowInit(object sender, EventArgs e)
		{
			AeroGlass.HideIcon(this);
			AeroGlass.HideSystemMenu(this, currentInfo.targetPath != null);
		}

		void OnNextButtonClick(object sender, RoutedEventArgs e)
		{
			if (CurrentPage.Content is SelectAppPage appPage)
			{
				appPage.Cancel();

				currentInfo.targetPath = appPage.FileName;
				SetupConfig.SCFontPath = appPage.InstallSCFontCheckBox.IsChecked ?? false ? SetupConfig.SCFontPath : null;
				SetupConfig.ShutterSEPath = appPage.InstallShutterSECheckBox.IsChecked ?? false ? SetupConfig.ShutterSEPath : null;

				if (!isElevated && !IsWritable(Path.GetDirectoryName(currentInfo.targetPath)))
				{
					RestartWithElevatedPrivileges();
				}
				else
				{
					RunTaskWithExceptionHandling(InstallStep_AnalyzeExecutable);
				}
				return;
			}

			if (CurrentPage.Content is SelectApiPage apiPage)
			{
				currentInfo.targetApi = apiPage.SelectedApi;
				currentInfo.targetOpenXR = apiPage.SelectedOpenXR;

				RunTaskWithExceptionHandling(InstallStep_CheckExistingInstallation);
				return;
			}

			if (CurrentPage.Content is SelectOperationPage operationPage)
			{
				currentOperation = operationPage.SelectedOperation;

				switch (currentOperation)
				{
					case InstallOperation.Update:
					case InstallOperation.UpdateWithEffects:
						RunTaskWithExceptionHandling(InstallStep_InstallReShadeModule);
						break;
					case InstallOperation.Uninstall:
						RunTaskWithExceptionHandling(InstallStep_UninstallReShadeModule);
						break;
				}
				return;
			}

			if (CurrentPage.Content is SelectEffectsPage effectsPage)
			{
				currentInfo.presetPath = effectsPage.PresetPath;

				var packages = new List<EffectPackage>(effectsPage.SelectedItems);

				var isAutoPresets = effectsPage.AutoPresets.IsChecked ?? false;
				var isAutoCN2 = effectsPage.AutoCN2.IsChecked ?? false;

				RunTaskWithExceptionHandling(() =>
				{
					if (isAutoCN2)
					{
						InstallStep_AutoCN2_InstallEffectPackage(isAutoPresets);
					}
					else
					{
						InstallStep_CheckPreset();

						foreach (EffectPackage package in packages)
						{
							InstallStep_DownloadEffectPackage(package);

							if (currentOperation == InstallOperation.Finished)
							{
								return;
							}
						}
					}

					InstallStep_CheckAddons();
				});
				return;
			}

			if (CurrentPage.Content is SelectAddonsPage addonsPage)
			{
				var addons = new List<Addon>(addonsPage.SelectedItems);

				RunTaskWithExceptionHandling(() =>
				{
					foreach (Addon addon in addons)
					{
						InstallStep_DownloadAddon(addon);

						if (currentOperation == InstallOperation.Finished)
						{
							return;
						}
					}

					InstallStep_Finish();
				});
				return;
			}
		}
		void OnBackButtonClick(object sender, RoutedEventArgs e)
		{
			if (currentOperation == InstallOperation.Finished)
			{
				ResetStatus();
			}
		}
		void OnFinishButtonClick(object sender, RoutedEventArgs e)
		{
			Close();
		}

		void OnSkipButtonClick(object sender, RoutedEventArgs e)
		{
			InstallStep_Finish();
		}
		void OnCancelButtonClick(object sender, RoutedEventArgs e)
		{
			if (CurrentPage.Content is SelectAppPage appPage)
			{
				appPage.Cancel();
			}

			Close();
		}

		void OnCurrentPageNavigated(object sender, NavigationEventArgs e)
		{
			bool isFinished = currentOperation == InstallOperation.Finished;
			bool isSkippable = e.Content is SelectAddonsPage || e.Content is SelectEffectsPage;

			NextButton.Visibility = isFinished ? Visibility.Collapsed : Visibility.Visible;
			FinishButton.Visibility = isFinished ? Visibility.Visible : Visibility.Collapsed;

			SkipButton.Visibility = isSkippable ? Visibility.Visible : Visibility.Collapsed;
			CancelButton.Visibility = isSkippable ? Visibility.Collapsed : Visibility.Visible;

			BackButton.IsEnabled = isFinished;
			CancelButton.IsEnabled = !(e.Content is StatusPage);

			if (!(e.Content is SelectAppPage))
			{
				NextButton.IsEnabled = isFinished || !(e.Content is StatusPage);
			}
		}
	}
}
