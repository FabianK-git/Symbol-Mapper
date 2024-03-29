﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using PInvoke;
using WinRT;
using Microsoft.UI.Composition.SystemBackdrops;
using Symbol_Mapper_Project.Mapper;
using System.Runtime.InteropServices;
using HotKeyHandler;
using Windows.System;
using WinUIEx;
using Symbol_Mapper_Project.Models;
using Windows.Storage;
using Symbol_Mapper_Project.Components;

namespace Symbol_Mapper_Project
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // Window handle
        readonly IntPtr hwnd;

        // Window width
        readonly int window_size_x = 700;

        // If the window size adapts dynamicly and the height
        readonly bool set_dynamic_height = true;
        int window_size_y = 70;
        readonly int window_size_y_smallest = 60;

        // How many pixels does the window get moved upwards
        readonly int window_up_displacement = 50;
        
        // Variables for the acrylic backdrop
        private WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        private DesktopAcrylicController m_acrylicController;
        private SystemBackdropConfiguration m_configurationSource;

        // If the window was already activated
        private bool activated;

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

            // Set the activated flag
            activated = false;

            // Get window Handle
            hwnd = this.GetWindowHandle();

            // Remove application from taskbar and `Alt + Tab`
            int ex_style = User32.GetWindowLong(hwnd, User32.WindowLongIndexFlags.GWL_EXSTYLE);
            ex_style |= (int) ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            User32.SetWindowLongPtr(hwnd, User32.WindowLongIndexFlags.GWL_EXSTYLE, (IntPtr) ex_style);

            // Remove minimize, maximize buttons and make the titlebar invisible
            WindowManager.Get(this).IsTitleBarVisible = false;

            this.SetIsMinimizable(false);
            this.SetIsMaximizable(false);
            
            // Disable resize on the window
            this.SetIsResizable(false);
            
            // Set height
            window_size_y = (set_dynamic_height) ? window_size_y_smallest : 310;

            // Position window in the middle and hide it
            CenterWindowOnCurrentDisplay();
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

            // If local settings does not contain the key set it with false
            if (!localSettings.Values.ContainsKey("hex_values"))
            {
                localSettings.Values.Add("hex_values", false);
            }

            // Set the trayicon menu text depending on value
            if ((bool) localSettings.Values["hex_values"])
            {
                menu_hex_toggle.Text = "Disable hex values";
            }
            else
            {
                menu_hex_toggle.Text = "Enable hex values";
            }

            // If local settings does not contain the key set it with false
            if (!localSettings.Values.ContainsKey("primary_window_placement"))
            {
                localSettings.Values.Add("primary_window_placement", false);
            }

            // Set the trayicon menu text depending on value
            if ((bool) localSettings.Values["primary_window_placement"])
            {
                menu_placement_toggle.Text = "Disable only primary monitor placement";
            }
            else
            {
                menu_placement_toggle.Text = "Enable only primary monitor placement";
            }

            // Add Trayicon menu
            taskbar_icon.ContextFlyout = tray_menu;
        }

        #region Trayicon_Menu
        private void OnExitApplication(object _, ExecuteRequestedEventArgs args)
        {
            this?.Close();
            Environment.Exit(1);
        }

        private void OnToggleHexValues(object _, ExecuteRequestedEventArgs args)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values["hex_values"] != null)
            {
                localSettings.Values["hex_values"] = !(bool) localSettings.Values["hex_values"];
            }

            // Set the trayicon menu text depending on value
            if ((bool)localSettings.Values["hex_values"])
            {
                menu_hex_toggle.Text = "Disable hex values";
            }
            else
            {
                menu_hex_toggle.Text = "Enable hex values";
            }
        }
        
        private void OnToggleWindowPlacement(object _, ExecuteRequestedEventArgs args)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            
            if (localSettings.Values["primary_window_placement"] != null)
            {
                localSettings.Values["primary_window_placement"] = !(bool) localSettings.Values["primary_window_placement"];
            }

            // Set the trayicon menu text depending on value
            if ((bool) localSettings.Values["primary_window_placement"])
            {
                menu_placement_toggle.Text = "Disable only primary monitor placement";
            }
            else
            {
                menu_placement_toggle.Text = "Enable only primary monitor placement";
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
        private void OnFocusLost(object _, RoutedEventArgs e)
        {
            window_size_y = window_size_y_smallest;

            HwndExtensions.HideWindow(hwnd);
        }
        
        private void OnTextChanged(object _, TextChangedEventArgs e)
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

            searchbox.ListView.ItemsSource = search_result;
        }
        
        private void OnQuerySubmitted(object _, SearchboxQuerySubmittedEventArgs e)
        {
            string symbol = string.Empty;
            
            if (e.SelectedItem != null)
            {
                if (e.SelectedItem is UnicodeData item)
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
                if (!activated)
                {
                    Activate();
                    activated = true;
                }

                searchbox.Text = string.Empty;

                List<UnicodeData> search_result = SymbolMapper.MapStringToSymbol("");

                // Change Height depending on amount of data in the ListView
                int amount = (search_result.Count) > 8 ? 8 : search_result.Count;

                window_size_y = window_size_y_smallest + (amount * 40);
                window_size_y += (amount > 0) ? 5 : 0;

                searchbox.ListView.ItemsSource = search_result;

                // Set position
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

                if (localSettings.Values["primary_window_placement"] != null &&
                    (bool) localSettings.Values["primary_window_placement"])
                {
                    CenterWindowOnPrimaryDisplay();
                }
                else
                {
                    CenterWindowOnCurrentDisplay();
                }

                HwndExtensions.ShowWindow(hwnd);
                HwndExtensions.SetForegroundWindow(hwnd);

                searchbox.Focus(FocusState.Keyboard);
            }
            else
            {
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

        #region Window Placement
        private void CenterWindowOnCurrentDisplay()
        {
            // Get mouse position
            POINT mouse_positon = User32.GetCursorPos();

            // Get the display where the mouse is
            IntPtr current_monitor = User32.MonitorFromPoint(mouse_positon, User32.MonitorOptions.MONITOR_DEFAULTTONEAREST);

            // Create the object for the monitor information
            User32.MONITORINFO monitor_info = new() { cbSize = Marshal.SizeOf(typeof(User32.MONITORINFO)) };

            // If the function returns true place the window on the current monitor,
            // else try to center it on primary display
            if (User32.GetMonitorInfo(current_monitor, ref monitor_info))
            {
                // Coordinates to center the monitor on the current window
                double new_window_x = monitor_info.rcWork.left + (monitor_info.rcWork.right - monitor_info.rcWork.left - window_size_x) / 2;
                double new_window_y = (monitor_info.rcWork.top + (monitor_info.rcWork.bottom - monitor_info.rcWork.top - window_size_y) / 2) - window_up_displacement;

                HwndExtensions.SetWindowPositionAndSize(hwnd, (int) new_window_x, (int) new_window_y, window_size_x, window_size_y);
            }
            else
            {
                CenterWindowOnPrimaryDisplay();
            }
        }

        private void CenterWindowOnPrimaryDisplay()
        {
            int screensize_x = User32.GetSystemMetrics(User32.SystemMetric.SM_CXFULLSCREEN);
            int screensize_y = User32.GetSystemMetrics(User32.SystemMetric.SM_CYFULLSCREEN);

            int middle_x = screensize_x / 2;
            int middle_y = screensize_y / 2;

            HwndExtensions.SetWindowPositionAndSize(hwnd, middle_x - (window_size_x / 2), middle_y - (window_size_y / 2) - window_up_displacement, window_size_x, window_size_y);
        }
        #endregion
    }
}
