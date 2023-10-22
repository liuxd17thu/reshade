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
using System.Windows.Controls;
using System.Windows.Navigation;

namespace ReShade.Setup.Pages
{
	public class Addon : INotifyPropertyChanged
	{
		public bool Enabled {
			get
			{
				var tmp = MainWindow.is64Bit ? DownloadUrl64 : DownloadUrl32;
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

		public string DownloadUrl => MainWindow.is64Bit ? DownloadUrl64 : DownloadUrl32;
		public string DownloadUrl32 { get; internal set; }
		public string DownloadUrl64 { get; internal set; }
		public string RepositoryUrl { get; internal set; }

		public event PropertyChangedEventHandler PropertyChanged;

		internal void NotifyPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public partial class SelectAddonsPage : Page
	{
		public SelectAddonsPage(Utilities.IniFile addonsIni)
		{
			InitializeComponent();
			DataContext = this;

			if (Directory.Exists(@".\" + SetupConfig.CN2Version + @"\reshade-addons"))
			{
				Items.Add(new Addon
				{
					Name = "[BETA] REST 汉化版",
					Description = "允许你将ReShade滤镜应用到指定的游戏着色器之前。",
					DownloadUrl32 = "",
					DownloadUrl64 = "@.\\reshade-addons\\REST\\ReshadeEffectShaderToggler.addon64",
					RepositoryUrl = "https://github.com/liuxd17thu/ReshadeEffectShaderToggler"
				});
				Items.Add(new Addon
				{
					Name = "[BETA] REST的《最终幻想14》特供配置文件",
					Description = "在《最终幻想14》中，可以作为FFKeepUI的上位替代，但不止于此……",
					DownloadUrl32 = "",
					DownloadUrl64 = "@.\\reshade-addons\\REST\\ReshadeEffectShaderToggler.ini",
					RepositoryUrl = "https://github.com/liuxd17thu/ReshadeEffectShaderToggler"
				});
			}
			if (addonsIni != null)
			{
				foreach (var addon in addonsIni.GetSections())
				{
					Items.Add(new Addon
					{
						Name = addonsIni.GetString(addon, "Name"),
						Description = addonsIni.GetString(addon, "Description"),
						DownloadUrl32 = addonsIni.GetString(addon, "DownloadUrl32"),
						DownloadUrl64 = addonsIni.GetString(addon, "DownloadUrl64"),
						RepositoryUrl = addonsIni.GetString(addon, "RepositoryUrl")
					});
				}
			}
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
