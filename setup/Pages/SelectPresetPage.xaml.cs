/*
 * Copyright (C) 2021 Patrick Mours
 * SPDX-License-Identifier: BSD-3-Clause
 */

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;

namespace ReShade.Setup.Pages
{
	public partial class SelectPresetPage : Page
	{
		public SelectPresetPage()
		{
			InitializeComponent();

			if (Directory.Exists("./" + SetupConfig.CN2Version + "/reshade-presets"))
			{
				AutoPresets.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#ff00aa33");
				AutoPresets.Content = "预设包：就绪";
				AutoPresets.IsChecked = true;
			}
			else
			{
				AutoPresets.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#ffaa0033");
				AutoPresets.Content = "未找到预设包";
				AutoPresets.IsEnabled = false; AutoPresets.IsChecked = false;
			}
		}

		public string FileName { get => PathBox.Text; set => PathBox.Text = value; }

		private void OnBrowseClick(object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog
			{
				Filter = "Presets|*.ini;*.txt",
				DefaultExt = ".ini",
				Multiselect = false,
				ValidateNames = true,
				CheckFileExists = true
			};

			if (dlg.ShowDialog() == true)
			{
				FileName = dlg.FileName;
			}
		}
	}
}
