using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.DbContextPackage.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EFCodeGenerate.WPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            var theme = ThemeManager.DetectAppStyle(Application.Current);
            var accent = ThemeManager.GetAccent("Emerald");
            ThemeManager.ChangeAppStyle(Application.Current, accent, theme.Item1);
            txtSavePath.Text = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Codes");
        }
        /// <summary>
        /// 开始生成代码
        /// </summary>
        private async void btnRun_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtDataSource.Text) ||
                   string.IsNullOrWhiteSpace(txtServiceName.Text) ||
                   string.IsNullOrWhiteSpace(txtUserName.Text) ||
                   string.IsNullOrWhiteSpace(txtPassword.Text))
                {
                    MessageBox.Show("主要参数不能为空.");
                    return;
                }

                var strCon = string.Format("DATA SOURCE={0}/{1};PASSWORD={3};PERSIST SECURITY INFO=True;POOLING=False;USER ID={2}",
                    txtDataSource.Text.Trim(),
                    txtServiceName.Text.Trim(),
                    txtUserName.Text.Trim().ToUpper(),
                    txtPassword.Text.Trim());

                List<string> tableNames = null;
                if (!string.IsNullOrWhiteSpace(txtTableNames.Text))
                {
                    tableNames = txtTableNames.Text.Split(new[] { " ", ",", "，", ":", "：" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                string savePath = null;
                if (!string.IsNullOrWhiteSpace(txtSavePath.Text))
                {
                    savePath = txtSavePath.Text.Trim();
                }

                btnRun.IsEnabled = false;
                var result = true;
                await Task.Factory.StartNew(() =>
                {
                    try
                    {
                        var handler = new OracleReverseEngineerCodeFirstHandler();
                        handler.ReverseEngineerCodeFirst(strCon, tableNames, savePath);
                    }
                    catch (Exception ex)
                    {
                        result = false;
                        this.Dispatcher.Invoke(async () => { await this.ShowMessageAsync("发生异常", ex.Message); });
                    }
                });

                if (result)
                {
                    await this.ShowMessageAsync("系统提示", "生成成功");
                }
                btnRun.IsEnabled = true;
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("发生异常", ex.Message);
            }
        }

        private async void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;

            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "确定",
                NegativeButtonText = "取消",
                AnimateShow = true,
                AnimateHide = false
            };

            var result = await this.ShowMessageAsync("系统提示",
                "确定要退出吗?",
                MessageDialogStyle.AffirmativeAndNegative, mySettings);
            if (result == MessageDialogResult.Affirmative)
                Application.Current.Shutdown();
        }
    }
}
