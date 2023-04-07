using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Collections.Generic;
using WinUIEx.Messaging;

namespace HotKeyHandler
{
    public class HotKeyPressedEventArgs
    {
        public int hotkey_id;
    }

    public class HotKey
    {
        #region Modifier Keys

        [Flags]
        public enum MOD_KEY
        {
            ALT = 0x0001,
            CONTROL = 0x0002,
            SHIFT = 0x0004,
            WIN = 0x0008,
            NOREPEAT = 0x4000,
        }

        #endregion

        #region Virtual Keys

        [Flags]
        public enum VKey
        {
            LEFT_MOUSE = 0x01,
            RIGHT_MOUSE = 0x02,
            CANCEL = 0x03,
            MIDDLE_MOUSE = 0x04,
            BACKSPACE = 0x08,
            TAB = 0x09,
            CLEAR = 0x0C,
            ENTER = 0x0D,
            SHIFT = 0x10,
            CONTROL = 0x11,
            ALT = 0x12,
            PAUSE = 0x13,
            CAPS_LOCK = 0x14,
            ESCAPE = 0x1B,
            SPACE = 0x20,
            PAGE_UP = 0x21,
            PAGE_DOWN = 0x22,
            END = 0x23,
            HOME = 0x24,
            LEFT = 0x25,
            UP = 0x26,
            RIGHT = 0x27,
            DOWN = 0x28,
            SELECT = 0x29,
            PRINT = 0x2A,
            EXECUTE = 0x2B,
            SNAPSHOT = 0x2C,
            INSERT = 0x2D,
            DELETE = 0x2E,
            HELP = 0x2F,
            LEFT_WIN = 0x5B,
            RIGHT_WIN = 0x5C,
            APPS = 0x5D,
            NUMPAD0 = 0x60,
            NUMPAD1 = 0x61,
            NUMPAD2 = 0x62,
            NUMPAD3 = 0x63,
            NUMPAD4 = 0x64,
            NUMPAD5 = 0x65,
            NUMPAD6 = 0x66,
            NUMPAD7 = 0x67,
            NUMPAD8 = 0x68,
            NUMPAD9 = 0x69,
            MULTIPLY = 0x6A,
            ADD = 0x6B,
            SEPERATOR = 0x6C,
            SUBTRACT = 0x6D,
            DECIMAL = 0x6E,
            DEVIDE = 0x6F,
            F1 = 0x70,
            F2 = 0x71,
            F3 = 0x72,
            F4 = 0x73,
            F5 = 0x74,
            F6 = 0x75,
            F7 = 0x76,
            F8 = 0x77,
            F9 = 0x78,
            F10 = 0x79,
            F11 = 0x7A,
            F12 = 0x7B,
            F13 = 0x7C,
            F14 = 0x7D,
            F15 = 0x7E,
            F16 = 0x7F,
            F17 = 0x80,
            F18 = 0x81,
            F19 = 0x82,
            F20 = 0x83,
            F21 = 0x84,
            F22 = 0x85,
            F23 = 0x86,
            F24 = 0x87,
            NUMLOCK = 0x90,
            SCROLL_LOCK = 0x91,
            LEFT_SHIFT = 0xA0,
            RIGHT_SHIFT = 0xA1,
            LEFT_CONTROL = 0xA2,
            RIGHT_CONTROL = 0xA3,
            LEFT_ALT = 0xA4,
            RIGHT_ALT = 0xA5,
            VOLUME_MUTE = 0xAD,
            VOLUME_DOWN = 0xAE,
            VOLUME_UP = 0xAF,
            MEDIA_NEXT_TRACK = 0xB0,
            MEDIA_PREV_TRACK = 0xB1,
            MEDIA_STOP = 0xB2,
            MEDIA_PLAY_PAUSE = 0xB3,
            PLAY = 0xFA
        }

        #endregion

        private readonly int WM_HOTKEY = 0x0312;

        #region DLL_Imports

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        #endregion

        // Singleton Instance
        private static readonly HotKey instance = new();

        // Variables
        private IntPtr? hwnd;
        private WindowMessageMonitor monitor;

        // All registered HotKeys
        List<int> registered_hot_keys;

        // Event Handler
        public event EventHandler<HotKeyPressedEventArgs> HotKeyPressed;

        /// <summary>
        /// Private constructor because this is a singleton class
        /// </summary>
        private HotKey() 
        {
            registered_hot_keys = new List<int>();
        }

        // Allow the instance to be usable
        public static HotKey Instance
        {
            get => instance;
        }

        /// <summary>
        /// Set the Window Handle by passing the window object
        /// </summary>
        /// <param name="window">Window for window handle</param>
        public void SetHwnd(Window window) 
        {
            SetHwnd(new WindowInteropHelper(window).EnsureHandle()); 
        }

        /// <summary>
        /// Set Window Handle
        /// </summary>
        /// <param name="hwnd">Window Handle</param>
        public void SetHwnd(int hwnd)
        {
            SetHwnd((IntPtr) hwnd);
        }

        /// <summary>
        /// Set Window Handle
        /// </summary>
        /// <param name="hwnd">Window Handle</param>
        public void SetHwnd(IntPtr hwnd)
        {
            this.hwnd = hwnd;

            registered_hot_keys = new List<int>();

            monitor = new WindowMessageMonitor(hwnd);
            monitor.WindowMessageReceived += OnWindowMessage;
        }

        /// <summary>
        /// Register a new Global HotKey with the Win32 API.
        /// </summary>
        /// <param name="modifier_key">Modifier Key</param>
        /// <param name="key">Virtual Key</param>
        /// <returns>True if the HotKey successfully registered, else false</returns>
        public bool NewGlobalHotKey(MOD_KEY modifier_key, VKey key)
        {
            bool success = false;

            if (hwnd != null)
            {
                success = RegisterHotKey((IntPtr) hwnd, (int) modifier_key * (int) key, (uint) modifier_key, (uint) key);

                if (success)
                {
                    registered_hot_keys.Add((int) modifier_key * (int) key);
                }
            }

            return success;
        }

        /// <summary>
        /// Removes the Global HotKey with the correct MOD and Virtual Key
        /// </summary>
        /// <param name="modifier_key">Modifier Key</param>
        /// <param name="key">Virtual Key</param>
        /// <returns>True if HotKey was successfully removed, otherwise false</returns>
        public bool RemoveGlobalHotKey(MOD_KEY modifier_key, VKey key)
        {
            bool success = false;

            if (hwnd != null)
            {
                success = UnregisterHotKey((IntPtr) this.hwnd, (int) modifier_key * (int) key);

                if (success)
                {
                    registered_hot_keys.Remove((int) modifier_key * (int) key);
                }
            }

            return success;
        }

        /// <summary>
        /// Checks if the HotKey ID maps to the a specific MOD and Virtual Key
        /// </summary>
        /// <param name="hotkey_id">Hotkey ID</param>
        /// <param name="modifier_key">Modifier Key</param>
        /// <param name="key">Virtual Key</param>
        /// <returns></returns>
        public static bool CheckCombo(int hotkey_id, MOD_KEY modifier_key, VKey key)
        {
            return hotkey_id == ((int) modifier_key * (int) key);
        }

        /// <summary>
        /// The message Hook that gets called when the Hotkey is pressed.
        /// </summary>
        private void OnWindowMessage(object sender, WindowMessageEventArgs e)
        {
            if (e.Message.MessageId == WM_HOTKEY)
            {
                HotKeyPressedEventArgs hotKeyPressedEventArgs = new()
                {
                    hotkey_id = (int) e.Message.WParam
                };

                OnHotKeyPressed(hotKeyPressedEventArgs);
            }
        }

        protected virtual void OnHotKeyPressed(HotKeyPressedEventArgs e)
        {
            HotKeyPressed?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the registered HotKey and the attached listener.
        /// </summary>
        public void Dispose()
        {
            if (hwnd != null)
            {
                foreach (var id in registered_hot_keys)
                {
                    UnregisterHotKey((IntPtr) this.hwnd, id);
                }

                monitor.WindowMessageReceived -= OnWindowMessage;
            }
        }
    }
}
