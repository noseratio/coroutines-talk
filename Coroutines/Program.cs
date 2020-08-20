﻿// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Coroutines
{
    public class Program
    {
        #region Async UI Helpers
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1);
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private CancellationToken Token => _cts.Token;

        private void Stop()
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }

        private async void StartAsync(Func<CancellationToken, ValueTask> func)
        {
            try
            {
                Stop();
                await _asyncLock.WaitAsync(this.Token);
                try
                {
                    await func(this.Token);
                }
                finally
                {
                    _asyncLock.Release();
                }
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                {
                    HandleException(ex);
                }
            }
        }

        private void HandleException(Exception ex)
        {
            Console.Clear();
            Console.SetCursorPosition(0, 0);
            Trace.TraceError(ex.ToString());
            Console.WriteLine(ex.Message);
            Application.Exit();
        }
        #endregion

        #region Menu handlers
        void StartCoroutineDemo(object? s, EventArgs? e)
        {
            StartAsync(token => CoroutineDemo.DemoAsync(token));
        }

        void StartAsyncCoroutineDemo(object? s, EventArgs? e)
        {
            StartAsync(token => AsyncCoroutineDemo.DemoAsync(token));
        }

        void StartAsyncCoroutineDemoMutual(object? s, EventArgs? e)
        {
            StartAsync(token => AsyncCoroutineDemoMutual.DemoAsync(token));
        }
        #endregion

        #region UI

        private Form CreateUI()
        {
            var form = new Form
            {
                Text = Application.ProductName,
                Width = 800, Height = 400,
                StartPosition = FormStartPosition.CenterScreen
            };

            form.Controls.Add(new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                Dock = DockStyle.Fill
            });

            var menu = new MenuStrip { Dock = DockStyle.Top };
            var actionMenu = new ToolStripMenuItem("Coroutines");
            menu.Items.Add(actionMenu);
            var actionItems = actionMenu.DropDownItems;

            actionItems.Add(
                "IEnumerable/Pull-based coroutines", null,
                StartCoroutineDemo);

            actionItems.Add(
                "IAsyncEnumerable/Push-based coroutines", null,
                StartAsyncCoroutineDemo);

            actionItems.Add(
                "IAsyncEnumerable/Push-based mutual coroutines", null,
                StartAsyncCoroutineDemoMutual);

            menu.Items.Add(
                "Stop", null, (s, e) => Stop());

            menu.Items.Add(
                "Close", null, (s, e) => form.Close());

            form.MainMenuStrip = menu;
            form.Controls.Add(menu);
            form.FormClosing += (s, e) => Stop();

            return form;
        }
        #endregion

        public static void Main()
        {
            var program = new Program();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => program.HandleException(e.Exception);
            Application.Run(program.CreateUI());
            Console.Clear();
        }
    }
}
