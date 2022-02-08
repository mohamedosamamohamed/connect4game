﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleClient.Dialogs
{
	public partial class CounterRequestDialog : Form
	{
		string counterName;
		public CounterRequestDialog(string counterName)
		{
			InitializeComponent();
			MsgLable.Text = $"{counterName} want to play!";
		}

		private void AcceptRequestButton_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.OK;
			this.Close();
		}

		private void DeclinesRequestButton_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}

		private void CounterRequestDialog_Load(object sender, EventArgs e)
		{
			
		}
	}
}
