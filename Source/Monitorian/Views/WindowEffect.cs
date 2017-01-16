﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

using Monitorian.Helper;

namespace Monitorian.Views
{
	internal class WindowEffect
	{
		#region Win32 (for Win7)

		[DllImport("Dwmapi.dll")]
		private static extern int DwmIsCompositionEnabled(out bool pfEnabled);

		[DllImport("Dwmapi.dll")]
		private static extern int DwmEnableBlurBehindWindow(
			IntPtr hWnd,
			ref DWM_BLURBEHIND pBlurBehind);

		[StructLayout(LayoutKind.Sequential)]
		private struct DWM_BLURBEHIND
		{
			public DWM_BB dwFlags;

			[MarshalAs(UnmanagedType.Bool)]
			public bool fEnable;

			public IntPtr hRgnBlur;

			[MarshalAs(UnmanagedType.Bool)]
			public bool fTransitionOnMaximized;
		}

		[Flags]
		private enum DWM_BB : uint
		{
			DWM_BB_ENABLE = 0x00000001,
			DWM_BB_BLURREGION = 0x00000002,
			DWM_BB_TRANSITIONONMAXIMIZED = 0x00000004
		}

		private const int S_OK = 0x00000000;

		#endregion

		#region Win32 (for Win10)

		/// <summary>
		/// Sets window composition attribute (Undocumented API).
		/// </summary>
		/// <param name="hwnd">Window handle</param>
		/// <param name="data">Attribute data</param>
		/// <returns>True if succeeded. False if not.</returns>
		/// <remarks>
		/// This API and relevant parameters are derived from:
		/// https://github.com/riverar/sample-win10-aeroglass 
		/// </remarks>
		[DllImport("User32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetWindowCompositionAttribute(
			IntPtr hwnd,
			ref WindowCompositionAttributeData data);

		[StructLayout(LayoutKind.Sequential)]
		private struct WindowCompositionAttributeData
		{
			public WindowCompositionAttribute Attribute;
			public IntPtr Data;
			public int SizeOfData;
		}

		private enum WindowCompositionAttribute
		{
			// ...
			WCA_ACCENT_POLICY = 19
			// ...
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct AccentPolicy
		{
			public AccentState AccentState;
			public int AccentFlags;
			public int GradientColor;
			public int AnimationId;
		}

		private enum AccentState
		{
			ACCENT_DISABLED = 0,
			ACCENT_ENABLE_GRADIENT = 1,
			ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
			ACCENT_ENABLE_BLURBEHIND = 3,
			ACCENT_INVALID_STATE = 4
		}

		#endregion

		public static bool EnableBackgroundBlur(Window window)
		{
			if (!OsVersion.IsVistaOrNewer)
				return false;

			if (!OsVersion.Is8OrNewer)
				return EnableBackgroundBlurForWin7(window);

			if (!OsVersion.Is10Threshold1OrNewer)
				return false; // For Windows 8 and 8.1, no blur effect is available.

			return EnableBackgroundBlurForWin10(window);
		}

		private static bool EnableBackgroundBlurForWin7(Window window)
		{
			bool isEnabled;
			if ((DwmIsCompositionEnabled(out isEnabled) != S_OK) || !isEnabled)
				return false;

			var windowHandle = new WindowInteropHelper(window).Handle;

			var bb = new DWM_BLURBEHIND
			{
				dwFlags = DWM_BB.DWM_BB_ENABLE,
				fEnable = true,
				hRgnBlur = IntPtr.Zero
			};

			return (DwmEnableBlurBehindWindow(windowHandle, ref bb) == S_OK);
		}

		private static bool EnableBackgroundBlurForWin10(Window window)
		{
			var windowHandle = new WindowInteropHelper(window).Handle;

			var accent = new AccentPolicy { AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND };
			var accentSize = Marshal.SizeOf(accent);

			IntPtr accentPointer = IntPtr.Zero;
			try
			{
				accentPointer = Marshal.AllocHGlobal(accentSize);
				Marshal.StructureToPtr(accent, accentPointer, false);

				var data = new WindowCompositionAttributeData
				{
					Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
					Data = accentPointer,
					SizeOfData = accentSize,
				};

				return SetWindowCompositionAttribute(windowHandle, ref data);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Failed to set window composition attribute.\r\n{ex}");
				return false;
			}
			finally
			{
				Marshal.FreeHGlobal(accentPointer);
			}
		}
	}
}