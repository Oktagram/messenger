﻿using System.Windows;

namespace MessengerAdminPanel.Windows
{
	public partial class PromptWindow : Window
	{
		public PromptWindow()
		{
			InitializeComponent();
		}

		public PromptWindow(string text) : this()
		{
			textBlockTitle.Text = text;
		}

		public string ResponseText
		{
			get { return textBoxResponse.Text; }
			set { textBoxResponse.Text = value; }
		}

		private void OKButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}
