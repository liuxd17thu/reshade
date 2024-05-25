/*
 * Copyright (C) 2021 Patrick Mours
 * SPDX-License-Identifier: BSD-3-Clause
 */

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Navigation;
using ReShade.Setup.Utilities;

namespace ReShade.Setup.Pages
{
	public class Addon : INotifyPropertyChanged
	{
		public bool Enabled
		{
			get
			{
				var tmp = DownloadUrl;
				if (string.IsNullOrEmpty(tmp))
					return false;
				else
				{
					if (tmp[0] == '@')
						return File.Exists(Path.Combine(SetupConfig.CN2PackDir, tmp.Trim('@')));
					else
						return true;
				}
			}
		}
		public bool Selected { get; set; } = false;

		public string Name { get; internal set; }
		public string Description { get; internal set; }

		public string EffectInstallPath { get; internal set; }
		public string DownloadUrl { get; internal set; }
		public string RepositoryUrl { get; internal set; }

		public event PropertyChangedEventHandler PropertyChanged;

		internal void NotifyPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public partial class SelectAddonsPage : Page
	{
		public SelectAddonsPage(bool is64Bit)
		{
			InitializeComponent();
			DataContext = this;

			if (Directory.Exists(@".\" + SetupConfig.CN2Version + @"\reshade-addons"))
			{
				Items.Add(new Addon
				{
					Name = "[AuroraShade] REST - AuroraShade改版",
					Description = "允许你将ReShade滤镜应用到指定的游戏着色器之前。",
					DownloadUrl = "@.\\reshade-addons\\REST\\ReshadeEffectShaderToggler.addon64",
					RepositoryUrl = "https://github.com/liuxd17thu/ReshadeEffectShaderToggler"
				});
				Items.Add(new Addon
				{
					Name = "[AuroraShade] REST的《最终幻想14》特供配置文件",
					Description = "在《最终幻想14》中，可以作为FFKeepUI的上位替代，但不止于此……",
					DownloadUrl = "@.\\reshade-addons\\REST\\ReshadeEffectShaderToggler.ini",
					RepositoryUrl = "https://github.com/liuxd17thu/ReshadeEffectShaderToggler"
				});
			}

			Task.Run(() =>
			{
				// Attempt to download add-ons list
				using (var client = new WebClient())
				{
					// Ensure files are downloaded again if they changed
					client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);

					try
					{
						using (Stream addonsStream = client.OpenRead("https://raw.githubusercontent.com/crosire/reshade-shaders/list/Addons.ini"))
						{
							var addonsIni = new IniFile(addonsStream);

							foreach (string addon in addonsIni.GetSections())
							{
								string downloadUrl = addonsIni.GetString(addon, "DownloadUrl");
								if (string.IsNullOrEmpty(downloadUrl))
								{
									downloadUrl = addonsIni.GetString(addon, is64Bit ? "DownloadUrl64" : "DownloadUrl32");
								}

								var item = new Addon
								{
									Name = addonsIni.GetString(addon, "PackageName"),
									Description = addonsIni.GetString(addon, "PackageDescription"),
									EffectInstallPath = addonsIni.GetString(addon, "EffectInstallPath", string.Empty),
									DownloadUrl = downloadUrl,
									RepositoryUrl = addonsIni.GetString(addon, "RepositoryUrl")
								};

								Dispatcher.Invoke(() => { Items.Add(item); });
							}
						}
					}
					catch (WebException)
					{
						// Ignore if this list failed to download, since setup can still proceed without it
					}
				}
			});
		}

		public IEnumerable<Addon> SelectedItems => Items.Where(x => x.Selected);
		public ObservableCollection<Addon> Items { get; } = new ObservableCollection<Addon>();

		private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			try
			{
				Process.Start(e.Uri.AbsoluteUri);
				e.Handled = true;
			}
			catch
			{
				e.Handled = false;
			}
		}
	}
}
