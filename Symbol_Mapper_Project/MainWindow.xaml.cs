﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using PInvoke;
using WinRT;
using WinRT.Interop;
using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Symbol_Mapper_Project.Mapper;
using System.Runtime.InteropServices;
using HotKeyHandler;
using Windows.System;
using WinUIEx;
using Symbol_Mapper_Project.Models;
using Windows.Storage;
using System.Collections;
using Windows.ApplicationModel.Activation;
using Microsoft.UI.Input;
using System.Diagnostics;

namespace Symbol_Mapper_Project
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        readonly IntPtr hwnd;

        readonly int window_size_x = 700;

        readonly bool set_dynamic_height = true;
        int window_size_y = 70;
        readonly int window_size_y_smallest = 70;

        readonly int window_up_displacement = 200;

        private WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        private DesktopAcrylicController m_acrylicController;
        private SystemBackdropConfiguration m_configurationSource;

        private string searchbox_last_value = string.Empty;
        
        #region Window styles
        [Flags]
        public enum ExtendedWindowStyles
        {
            WS_EX_TOOLWINDOW = 0x00000080,
        }
        #endregion

        public MainWindow()
        {
            this.InitializeComponent();

            // Set height
            window_size_y = (set_dynamic_height) ? window_size_y_smallest : 310;

            // Use Titlebar space for Window
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBar);

            // Set Position on Window
            int screensize_x = User32.GetSystemMetrics(User32.SystemMetric.SM_CXFULLSCREEN);
            int screensize_y = User32.GetSystemMetrics(User32.SystemMetric.SM_CYFULLSCREEN);

            int middle_x = screensize_x / 2;
            int middle_y = screensize_y / 2;

            // Get window Handle
            hwnd = WindowNative.GetWindowHandle(this);

            // User32.SetWindowPos(hwnd, new IntPtr(0), middle_x - (window_size_x / 2), middle_y - (window_size_y / 2) - window_up_displacement, window_size_x, window_size_y, User32.SetWindowPosFlags.SWP_SHOWWINDOW);
            HwndExtensions.SetWindowPositionAndSize(hwnd, middle_x - (window_size_x / 2), middle_y - (window_size_y / 2) - window_up_displacement, window_size_x, window_size_y);
            HwndExtensions.HideWindow(hwnd);
            
            // Register Global HotKeys
            HotKey.Instance.SetHwnd(hwnd);
            HotKey.Instance.NewGlobalHotKey(HotKey.MOD_KEY.CONTROL | HotKey.MOD_KEY.NOREPEAT, HotKey.VKey.SPACE);
            HotKey.Instance.HotKeyPressed += OnHotKeyPressed;

            // Try set Backdrop
            bool success = TrySetAcrylicBackdrop();

            // Set loading on taskbar icon
            MenuFlyout flyout = new();

            flyout.Items.Add(new MenuFlyoutItem()
            {
                Text = "Starting ..."
            });

            taskbar_icon.ContextFlyout = flyout;
            
            SymbolMapper.FetchUnicodeData();

            // Add show hex values to local storage - default false
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            if (!localSettings.Values.ContainsKey("hex_values"))
            {
                localSettings.Values.Add("hex_values", false);
            }

            // Add Trayicon menu
            taskbar_icon.ContextFlyout = tray_menu;
        }

        #region Trayicon_Menu
        private void ExitApplicationCommand_ExecuteRequested(object _, ExecuteRequestedEventArgs args)
        {
            this?.Close();
            Environment.Exit(1);
        }

        private void ToogleHexValues_ExecuteRequested(object _, ExecuteRequestedEventArgs args)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values["hex_values"] != null)
            {
                localSettings.Values["hex_values"] = !(bool) localSettings.Values["hex_values"];
            }
        }
        #endregion

        #region Window_Events
        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_acrylicController != null)
            {
                m_acrylicController.Dispose();
                m_acrylicController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;

            HotKey.Instance?.Dispose();
        }
        #endregion

        #region Suggestionbox_Events
        private void OnFocusGot(object sender, RoutedEventArgs e)
        {
            searchbox.Text = string.Empty;
            searchbox_last_value = string.Empty;

            List<UnicodeData> search_result = SymbolMapper.MapStringToSymbol("");

            if (set_dynamic_height)
            {
                int amount = (search_result.Count) > 8 ? 8 : search_result.Count;

                window_size_y = window_size_y_smallest + (amount * 40);
                window_size_y += (amount > 0) ? 5 : 0;

                User32.SetWindowPos(hwnd, new IntPtr(0), 0, 0, window_size_x, window_size_y, User32.SetWindowPosFlags.SWP_NOMOVE);
            }

            search_display.ItemsSource = search_result;
        }
        
        private void OnFocusLost(object sender, RoutedEventArgs e)
        {
            window_size_y = window_size_y_smallest;

            _ = RunWithDelay(() =>
            {
                // User32.ShowWindow(hwnd, User32.WindowShowStyle.SW_HIDE);
                HwndExtensions.HideWindow(hwnd);
            }, 1);
        }
        
        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Implement keyboard movement
            // and don't allow one SPACE as the first character
            switch (e.Key)
            {
                case VirtualKey.Down:
                case VirtualKey.Tab:
                {
                    e.Handled = true;

                    int last_index = search_display.SelectedIndex;

                    if (last_index == -1)
                    {
                        searchbox_last_value = searchbox.Text;
                    }
                    
                    int list_len = (search_display.ItemsSource as IList).Count;

                    if (list_len > 0)
                    {
                        int next_index = (last_index + 1) % list_len;

                        search_display.SelectedIndex = next_index;

                        searchbox.Text = (search_display.SelectedItem as UnicodeData).UnicodeCharacter;
                        search_display.ScrollIntoView(search_display.SelectedItem);

                        if (next_index == 0 && (last_index + 1) > 0)
                        {
                            search_display.SelectedIndex = -1;
                            searchbox.Text = searchbox_last_value;
                        }
                    }
                    else
                    {
                        search_display.SelectedIndex = -1;
                    }

                    searchbox.SelectionStart = searchbox.Text.Length;
                    searchbox.SelectionLength = 0;

                    break;
                }
                
                case VirtualKey.Up:
                {
                    int last_index = search_display.SelectedIndex;
                    int next_index = last_index - 1;

                    if (search_display.SelectedIndex == 0)
                    {
                        search_display.SelectedIndex = -1;

                        searchbox.Text = searchbox_last_value;
                    }
                    else if (next_index < -1)
                    {
                        int list_len = (search_display.ItemsSource as IList).Count;

                        search_display.SelectedIndex = list_len - 1;
                        search_display.ScrollIntoView(search_display.SelectedItem);
                        
                        searchbox.Text = (search_display.SelectedItem as UnicodeData).UnicodeCharacter;
                    }
                    else
                    {
                        search_display.SelectedIndex = next_index;
                        search_display.ScrollIntoView(search_display.SelectedItem);

                        searchbox.Text = (search_display.SelectedItem as UnicodeData).UnicodeCharacter;
                    }

                    searchbox.SelectionStart = searchbox.Text.Length;
                    searchbox.SelectionLength = 0;

                    break;
                }

                case VirtualKey.Space:
                    if (searchbox.Text.Length == 0)
                    {
                        e.Handled = true;
                    }
                    break;

                case VirtualKey.Enter:
                    QuerySubmitted();
                    break;

                default:
                    search_display.SelectedIndex = -1;
                    break;
            }
        }
        
        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (search_display.SelectedIndex == -1 &&
                (searchbox.Text == string.Empty || 
                searchbox.Text != searchbox_last_value))
            {
                if (searchbox.Text.Trim() == string.Empty)
                {
                    searchbox.Text = string.Empty;
                }

                List<UnicodeData> search_result = SymbolMapper.MapStringToSymbol(searchbox.Text.Trim().ToLower());

                if (set_dynamic_height)
                {
                    int amount = (search_result.Count) > 8 ? 8 : search_result.Count;

                    window_size_y = window_size_y_smallest + (amount * 40);
                    window_size_y += (amount > 0) ? 5 : 0;

                    User32.SetWindowPos(hwnd, new IntPtr(0), 0, 0, window_size_x, window_size_y, User32.SetWindowPosFlags.SWP_NOMOVE);
                }

                search_display.ItemsSource = search_result;
            }
        }
        
        private void OnItemClicked(object _, ItemClickEventArgs e)
        {
            search_display.SelectedItem = e.ClickedItem;

            QuerySubmitted();
        }
        
        private void QuerySubmitted()
        {
            string symbol = string.Empty;
            
            if (search_display.SelectedItem != null)
            {
                if (search_display.SelectedItem is UnicodeData item)
                {
                    symbol = item.UnicodeCharacter.ToString();
                }
            }
            else
            {
                List<UnicodeData> search_result = SymbolMapper.MapStringToSymbol(searchbox.Text.Trim().ToLower());

                if (search_result.Count > 0)
                {
                    symbol = search_result[0].UnicodeCharacter;
                }
            }

            if (string.IsNullOrEmpty(symbol))
            {
                return;
            }

            char[] chars = symbol.ToCharArray();

            // User32.ShowWindow(hwnd, User32.WindowShowStyle.SW_HIDE);
            HwndExtensions.HideWindow(hwnd);

            List<User32.INPUT> inputs = new();

            // Send keyup and keydown instruction to focused window
            foreach (bool keyUp in new bool[] { true, false })
            {
                // Send the amount of characters needed (Unicode Symbols can be 2 characters long) 
                for (int i = 0; i < chars.Length; i++)
                {
                    User32.INPUT input = new()
                    {
                        type = User32.InputType.INPUT_KEYBOARD,
                        Inputs = new User32.INPUT.InputUnion
                        {
                            ki = new User32.KEYBDINPUT
                            {
                                wVk = 0,
                                wScan = (User32.ScanCode) chars[i],
                                dwFlags = User32.KEYEVENTF.KEYEVENTF_UNICODE | (keyUp ? User32.KEYEVENTF.KEYEVENTF_KEYUP : 0),
                            }
                        }
                    };

                    inputs.Add(input);
                }
            }

            User32.SendInput(inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(User32.INPUT)));
        }
        #endregion

        #region Key_Handler
        private void OnHotKeyPressed(object sender, HotKeyPressedEventArgs e)
        {
            if (HotKey.CheckCombo(e.hotkey_id, HotKey.MOD_KEY.CONTROL | HotKey.MOD_KEY.NOREPEAT, HotKey.VKey.SPACE))
            {
                HandleHotKey();
            }
        }
        
        private void OnKeyUp(object _, KeyRoutedEventArgs e)
        {
            if (Visible && e.Key == VirtualKey.Escape) 
            {
                HandleHotKey();
            }
        }

        private void HandleHotKey()
        {
            if (!Visible)
            {
                int screensize_x = User32.GetSystemMetrics(User32.SystemMetric.SM_CXFULLSCREEN);
                int screensize_y = User32.GetSystemMetrics(User32.SystemMetric.SM_CYFULLSCREEN);

                int middle_x = screensize_x / 2;
                int middle_y = screensize_y / 2;

                // User32.SetWindowPos(hwnd, new IntPtr(0), middle_x - (window_size_x / 2), middle_y - (window_size_y / 2) - window_up_displacement, window_size_x, window_size_y, User32.SetWindowPosFlags.SWP_SHOWWINDOW);
                // User32.ShowWindow(hwnd, User32.WindowShowStyle.SW_SHOWNORMAL);

                HwndExtensions.SetWindowPositionAndSize(hwnd, middle_x - (window_size_x / 2), middle_y - (window_size_y / 2) - window_up_displacement, window_size_x, window_size_y);
                HwndExtensions.ShowWindow(hwnd);
                
                _ = RunWithDelay(() =>
                {
                    User32.SetForegroundWindow(hwnd);

                    searchbox.Focus(FocusState.Keyboard);
                }, 1);
            }
            else
            {
                // User32.ShowWindow(hwnd, User32.WindowShowStyle.SW_HIDE);
                // HwndExtensions.SetAlwaysOnTop(hwnd, false);
                HwndExtensions.HideWindow(hwnd);
            }
        }
        #endregion

        #region AcrylicBackdrop
        bool TrySetAcrylicBackdrop()
        {
            if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object
                m_configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;
                // ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default;
                // SetConfigurationSourceTheme();

                m_acrylicController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_acrylicController.SetSystemBackdropConfiguration(m_configurationSource);

                return true; // succeeded
            }

            return false; // Acrylic is not supported on this system
        }
        #endregion
        
        static async Task RunWithDelay(Action callback, int delay_ms)
        {
            await Task.Delay(delay_ms);
            callback();
        }
    }
}
