﻿using System;
using System.Runtime.InteropServices;
using System.Text;
using Edo.Win32.Model;

namespace Edo.Win32
{
    /// <summary>
    /// Wraps functions imported from the windows api
    /// </summary>
    public static class Api
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenProcess([In] ProcessAccess desiredAccess, [In] Boolean inheritHandle, [In] UInt32 processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern Boolean CloseHandle([In] IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern Boolean ReadProcessMemory([In] IntPtr processHandle, [In] IntPtr address, [Out] byte[] buffer,
            [In] UIntPtr nrBytesToRead, [Out] out UIntPtr nrBytesRead);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern Boolean WriteProcessMemory([In] IntPtr processHandle, [In] IntPtr address, [Out] byte[] buffer,
            [In] UIntPtr nrBytesToWrite, [Out] out UIntPtr nrBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern UInt32 GetProcessId([In] IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern Boolean QueryFullProcessImageName([In] IntPtr processHandle, [In] UInt32 flags,
            [Out] StringBuilder path, ref UInt32 size);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateToolhelp32Snapshot([In] SnapshotFlags flags, [In] UInt32 processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern Boolean Module32First([In] IntPtr snapshot, ref ModuleEntry32 moduleEntry);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern Boolean Module32Next([In] IntPtr snapshot, ref ModuleEntry32 moduleEntry);
    }
}