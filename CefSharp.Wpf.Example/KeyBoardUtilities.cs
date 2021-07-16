using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace CefSharp.Wpf.Example
{
    public class KeyBoardUtilities
    {
        public enum MapType : uint
        {
            MAPVK_VK_TO_VSC = 0x0,
            MAPVK_VSC_TO_VK = 0x1,
            MAPVK_VK_TO_CHAR = 0x2,
            MAPVK_VSC_TO_VK_EX = 0x3,
        }

        [DllImport("user32.dll")]
        public static extern int ToUnicode(uint virtualKeyCode, uint scanCode,
        byte[] keyboardState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)]
                StringBuilder receivingBuffer,
        int bufferSize, uint flags);

        /// <summary>
        /// https://stackoverflow.com/a/6949520/450141
        /// </summary>
        public static string GetCharsFromKeys(Keys keys, bool shift, bool altGr)
        {
            var buf = new StringBuilder(256);
            var keyboardState = new byte[256];
            if (shift)
                keyboardState[(int)Keys.ShiftKey] = 0xff;
            if (altGr)
            {
                keyboardState[(int)Keys.ControlKey] = 0xff;
                keyboardState[(int)Keys.Menu] = 0xff;
            }
            ToUnicode((uint)keys, 0, keyboardState, buf, 256, 0);
            return buf.ToString();
        }

        static void SendKeys(ChromiumWebBrowser browser)
        {
            KeyEvent[] events = new KeyEvent[] {
                new KeyEvent() { FocusOnEditableField = true, WindowsKeyCode = GetCharsFromKeys(Keys.R, false, false)[0], Modifiers = CefEventFlags.None, Type = KeyEventType.Char, IsSystemKey = false }, // Just the letter R, no shift (so no caps...?)
                new KeyEvent() { FocusOnEditableField = true, WindowsKeyCode = GetCharsFromKeys(Keys.R, true, false)[0], Modifiers = CefEventFlags.ShiftDown, Type = KeyEventType.Char, IsSystemKey = false }, // Capital R?
                new KeyEvent() { FocusOnEditableField = true, WindowsKeyCode = GetCharsFromKeys(Keys.D4, false, false)[0], Modifiers = CefEventFlags.None, Type = KeyEventType.Char, IsSystemKey = false }, // Just the number 4
                new KeyEvent() { FocusOnEditableField = true, WindowsKeyCode = GetCharsFromKeys(Keys.D4, true, false)[0], Modifiers = CefEventFlags.ShiftDown, Type = KeyEventType.Char, IsSystemKey = false }, // Shift 4 (should be $)
            };

            foreach (KeyEvent ev in events)
            {
                Thread.Sleep(100);
                browser.GetBrowser().GetHost().SendKeyEvent(ev);
            }
        }

        //[DllImport("user32.dll")]
        //public static extern int ToUnicode(
        //            uint wVirtKey,
        //            uint wScanCode,
        //            byte[] lpKeyState,
        //            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)]
        //    StringBuilder pwszBuff,
        //            int cchBuff,
        //            uint wFlags);

        //[DllImport("user32.dll")]
        //public static extern bool GetKeyboardState(byte[] lpKeyState);

        //[DllImport("user32.dll")]
        //public static extern uint MapVirtualKey(uint uCode, MapType uMapType);

        //public static char? GetCharFromKey(Key key, bool shift)
        //{
        //    char? ch = null;

        //    int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        //    byte[] keyboardState = new byte[256];
        //    GetKeyboardState(keyboardState);

        //    if (shift)
        //    {
        //        keyboardState[(int)Key.LeftShift] = 0xff;
        //    }

        //    uint scanCode = MapVirtualKey((uint)virtualKey, MapType.MAPVK_VK_TO_VSC);
        //    StringBuilder stringBuilder = new StringBuilder(8);

        //    int result = ToUnicode((uint)virtualKey, scanCode, keyboardState, stringBuilder, stringBuilder.Capacity, 0);
        //    switch (result)
        //    {
        //        case -1:
        //            break;
        //        case 0:
        //            break;
        //        case 1:
        //            {
        //                ch = stringBuilder[0];
        //                break;
        //            }
        //        default:
        //            {
        //                ch = stringBuilder[0];
        //                break;
        //            }
        //    }
        //    return ch;
        //}
    }
}
