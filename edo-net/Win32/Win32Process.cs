﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Edo.Win32
{
    /// <summary>
    /// Provides a managed low level interface to a process
    /// </summary>
    public class Win32Process
    {
        /// <summary>
        /// Opens a handle with given access rights to a process with given process id
        /// </summary>
        /// <param name="id">The id of the process to be opened</param>
        /// <param name="desiredRights">The desired access rights to the process</param>
        /// <returns>The new handle to the process</returns>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        public static SafeProcessHandle OpenHandle(Int32 id, ProcessRights desiredRights)
        {
            IntPtr handle = Kernel32.OpenProcess(desiredRights, false, Convert.ToUInt32(id));
            if (handle.IsNullPtr())
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open handle to process");

            return new SafeProcessHandle(handle, true);
        }

        /// <summary>
        /// Opens a process with given process id
        /// </summary>
        /// <param name="id">The id of the process to be opened</param>
        /// <param name="desiredRights">The desired access rights to the process</param>
        /// <returns>The newly opened process</returns>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        public static Win32Process Open(Int32 id, ProcessRights desiredRights)
        {
            return new Win32Process(OpenHandle(id, desiredRights));
        }

        /// <summary>
        /// Returns the current process opened as a Win32Process with full access rights
        /// </summary>
        /// <returns>The current process</returns>
        public static Win32Process GetCurrentProcess()
        {
            var process = Process.GetCurrentProcess();
            return Open(process.Id, ProcessRights.AllAccess);
        }

        /// <summary>
        /// Returns a collection of handles that are currently active within the system
        /// </summary>
        /// <returns>A collection of active handles</returns>
        public static ICollection<SystemHandle> GetHandles()
        {
            byte[] buffer;
            NtStatus status;
            int size = 1024;
            uint actualSize = 0;
            do
            {
                buffer = new byte[size];
                status = NtDll.NtQuerySystemInformation(SystemInformationType.HandleInformation, buffer,
                    Convert.ToUInt32(size), ref actualSize);
                if(status != NtStatus.Success && status != NtStatus.BufferTooSmall && status != NtStatus.InfoLengthMismatch)
                    throw new Win32Exception("Could not retrieve system handle information");

                size = Convert.ToInt32(actualSize);
            }
            while (status != NtStatus.Success);

            int count = Seriz.Parse<int>(buffer);
            NtDll.SystemHandle[] systemHandles = Seriz.Parse<NtDll.SystemHandle>(buffer.Skip(4).ToArray(), count);
            List<SystemHandle> results = new List<SystemHandle>();
            foreach (var handle in systemHandles)
                results.Add(new SystemHandle(Convert.ToInt32(handle.ProcessId), handle.Type,
                    new IntPtr(handle.Handle), handle.Rights));

            return results;
        }

        /// <summary>
        /// Creates the process with given handle
        /// </summary>
        /// <param name="handle">A handle to the process to be targeted</param>
        public Win32Process(SafeProcessHandle handle)
        {
            Handle = handle;
            Buffer = new byte[16];
        }

        /// <summary>
        /// Reads given amount of bytes from given address in virtual memory of the process and writes them into given buffer
        /// </summary>
        /// <param name="address">The address to be read from</param>
        /// <param name="buffer">The buffer to write the data to</param>
        /// <param name="count">The number of bytes to be read</param>
        /// <exception cref="ArgumentException">If count is equal to or less than zero</exception>
        /// <exception cref="ArgumentException">If buffer is too small to fit the requested data</exception>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        /// <exception cref="InvalidOperationException">If the call succeeds but too few bytes were read</exception>
        public void Read(IntPtr address, byte[] buffer, Int32 count)
        {
            if(buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if(count <= 0)
                throw new ArgumentException("Count must be greater than zero");

            if (count > buffer.Length)
                throw new ArgumentException("Not enough room in buffer to hold requested amount of data");

            UIntPtr nrBytesRead;
            UInt32 unsignedCount = Convert.ToUInt32(count);
            if (!Kernel32.ReadProcessMemory(Handle.DangerousGetHandle(), address, buffer,
                new UIntPtr(unsignedCount), out nrBytesRead))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not perform read operation");

            if (nrBytesRead.ToUInt32() != unsignedCount)
                throw new InvalidOperationException(string.Format("Operation only read {0} out of {1} wanted bytes", nrBytesRead, count));
        }

        /// <summary>
        /// Reads given amount of bytes from given address in virtual memory of the process and writes them into given stream
        /// </summary>
        /// <param name="address">The address to be read from</param>
        /// <param name="outStream">The stream to write the data to</param>
        /// <param name="count">The amount of bytes to be read</param>
        /// <exception cref="ArgumentException">If count is equal to or less than zero</exception>
        /// <exception cref="ArgumentException">If buffer is too small to fit the requested data</exception>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        /// <exception cref="InvalidOperationException">If the call succeeds but too few bytes were read</exception>
        public void Read(IntPtr address, Stream outStream, Int32 count)
        {
            if(outStream == null)
                throw new ArgumentNullException(nameof(outStream));

            if(count <= 0)
                throw new ArgumentException("Count must be greater than zero");

            if (count > Buffer.Length)
                Buffer = new byte[count];

            Read(address, Buffer, count);
            outStream.Write(Buffer, 0, count);
        }

        /// <summary>
        /// Reads a structure or an instance of a formatted class from given address in virtual memory of the process
        /// </summary>
        /// <typeparam name="T">The type of structure or formatted class to be read</typeparam>
        /// <param name="address">The address to be read from</param>
        /// <returns>The structure or instance read from virtual memory of the process</returns>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        /// <exception cref="InvalidOperationException">If the call succeeds but too few bytes were read</exception>
        public T Read<T>(IntPtr address)
        {
            return Read<T>(address, 1)[0];
        }

        /// <summary>
        /// Reads an array of structures or instances of a formatted class from given address in virtual memory of the process
        /// </summary>
        /// <typeparam name="T">The type of structure or formatted class to be read</typeparam>
        /// <param name="address">The address to be read from</param>
        /// <param name="count">The number of elements in the array to be read</param>
        /// <returns>The array of structures or instances read from virtual memory of the process</returns>
        /// <exception cref="ArgumentException">If count is equal to or less than zero</exception>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        /// <exception cref="InvalidOperationException">If the call succeeds but too few bytes were read</exception>
        public T[] Read<T>(IntPtr address, Int32 count)
        {
            if(count <= 0)
                throw new ArgumentException("Count must be greater than zero");

            int size = Marshal.SizeOf<T>();
            int totalSize = size * count;
            if(Buffer.Length < totalSize)
                Buffer = new byte[totalSize];

            Read(address, Buffer, totalSize);
            return Seriz.Parse<T>(Buffer, count);
        }

        /// <summary>
        /// Writes given count of bytes from given buffer to given address in virtual memory of the process
        /// </summary>
        /// <param name="address">The address to be written to</param>
        /// <param name="buffer">The buffer containing the data to be written</param>
        /// <param name="count">The amount of bytes to write from the buffer</param>
        /// <exception cref="ArgumentException">If count is equal to or less than zero</exception>
        /// <exception cref="ArgumentException">If buffer is too small to fit the requested data</exception>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        /// <exception cref="InvalidOperationException">If the call succeeds but too few bytes were written</exception>
        public void Write(IntPtr address, byte[] buffer, Int32 count)
        {
            if(buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if(count <= 0)
                throw new ArgumentException("Count must be greater than or equal to zero");

            if (count > buffer.Length)
                throw new ArgumentException("Not enough room in buffer to hold requested amount of data");

            UInt32 unsignedCount = Convert.ToUInt32(count);
            UIntPtr nrBytesWritten;
            if(!Kernel32.WriteProcessMemory(Handle.DangerousGetHandle(), address, buffer,
                new UIntPtr(unsignedCount), out nrBytesWritten))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not perform write operation");

            if(nrBytesWritten.ToUInt32() != unsignedCount)
                throw new InvalidOperationException(string.Format("Operation only wrote {0} out of {1} wanted bytes", nrBytesWritten, count));
        }

        /// <summary>
        /// Writes given count of bytes from given stream to given address in virtual memory of the process
        /// </summary>
        /// <param name="address">The address to be written to</param>
        /// <param name="stream">The stream containing the data to be written</param>
        /// <param name="count">The amount of bytes to write from the stream</param>
        /// <exception cref="ArgumentException">If count is equal to or less than zero</exception>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        /// <exception cref="InvalidOperationException">If the call succeeds but too few bytes were written</exception>
        public void Write(IntPtr address, Stream stream, Int32 count)
        {
            if(stream == null)
                throw new ArgumentNullException(nameof(stream));

            if(count <= 0)
                throw new ArgumentException("Count must be greater then zero");

            if (count > Buffer.Length)
                Buffer = new byte[count];

            stream.Read(Buffer, 0, count);
            Write(address, Buffer, count);
        }

        /// <summary>
        /// Writes a structure or an instance of a formatted class to given address in virtual memory of the process
        /// </summary>
        /// <typeparam name="T">The type of the structure or formatted class to be written</typeparam>
        /// <param name="address">The address to be written to</param>
        /// <param name="value">The structure or instance of a formatted class to be written</param>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        /// <exception cref="InvalidOperationException">If the call succeeds but too few bytes were written</exception>
        public void Write<T>(IntPtr address, T value)
        {
            if(value == null)
                throw new ArgumentNullException(nameof(value));
            
            Write(address, new T[] {value});
        }

        /// <summary>
        /// Writes an array of structures or instances of a formatted class to given address in virtual memory of the process
        /// </summary>
        /// <typeparam name="T">The type of the structure or formatted class to be written</typeparam>
        /// <param name="address">The address to be written to</param>
        /// <param name="values">The array of structures or instances of a formatted class to be written</param>
        /// <exception cref="ArgumentException">If the given array is empty</exception>
        /// <exception cref="Win32Exception">On Windows API error</exception>
        /// <exception cref="InvalidOperationException">If the call succeeds but too few bytes were written</exception>
        public void Write<T>(IntPtr address, T[] values)
        {
            if(values == null)
                throw new ArgumentNullException(nameof(values));

            if(values.Length == 0)
                throw new ArgumentException("Cannot write an empty array");

            int size = Marshal.SizeOf<T>();
            int totalSize = size * values.Length;
            if(Buffer.Length < totalSize)
                Buffer = new byte[totalSize];

            Seriz.Serialize(Buffer, values);
            Write(address, Buffer, totalSize);
        }

        /// <summary>
        /// Allocates a block of given size and given protection options inside virtual memory of the process using given allocation options
        /// </summary>
        /// <param name="count">The amount of bytes to be allocated</param>
        /// <param name="allocationType">The allocation options to be used when allocating</param>
        /// <param name="protectionOptions">The protection options of the allocated block</param>
        /// <returns>The starting address of the newly allocated block</returns>
        /// <exception cref="ArgumentException">If count is equal to or less than zero</exception>
        public IntPtr Alloc(int count, AllocationOptions allocationType, ProtectionOptions protectionOptions)
        {
            if(count <= 0)
                throw new ArgumentException("Count must be greater than zero");

            IntPtr address = Kernel32.VirtualAllocEx(Handle.DangerousGetHandle(), IntPtr.Zero,
                new UIntPtr(Convert.ToUInt32(count)), allocationType, protectionOptions);
            if(address.IsNullPtr())
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not allocate memory within process");

            return address;
        }

        /// <summary>
        /// Allocates a block of given size inside virtual memory of the process
        /// </summary>
        /// <param name="count">The amount of bytes to be allocated</param>
        /// <returns>The starting address of the newly allocated block</returns>
        /// <exception cref="ArgumentException">If count is equal to or less than zero</exception>
        public IntPtr Alloc(int count)
        {
            if (count <= 0)
                throw new ArgumentException("Count must be greater than zero");

            return Alloc(count, AllocationOptions.Commit, ProtectionOptions.ExecuteReadWrite);
        }

        /// <summary>
        /// Frees a previously allocated block of memory inside virtual memory of the process
        /// </summary>
        /// <param name="address">The starting address of the block to be freed</param>
        public void Free(IntPtr address)
        {
            if(!Kernel32.VirtualFreeEx(Handle.DangerousGetHandle(), address, UIntPtr.Zero, FreeOptions.Release))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not free memory in process");
        }

        /// <summary>
        /// Enables or disables the privilege with given key for this process
        /// </summary>
        /// <param name="key">The privilege key</param>
        /// <param name="enable">Whether to enable or disable the privilege</param>
        public void SetPrivilege(String key, Boolean enable)
        {
            AdvApi32.Luid luid = new AdvApi32.Luid();
            if(!AdvApi32.LookupPrivilegeValue(null, key, ref luid))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not perform lookup of privilege value");

            IntPtr handle = IntPtr.Zero;
            if(!AdvApi32.OpenProcessToken(Handle.DangerousGetHandle(), TokenAccessLevels.AdjustPrivileges, ref handle))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open process token");

            using (SafeAccessTokenHandle tokenHandle = new SafeAccessTokenHandle(handle))
            {
                AdvApi32.TokenPrivileges privileges = new AdvApi32.TokenPrivileges();
                privileges.PrivilegeCount = 1;
                privileges.Luid = luid;
                privileges.State = enable ? PrivilegeState.Enabled : PrivilegeState.Removed;

                if(!AdvApi32.AdjustTokenPrivileges(tokenHandle.DangerousGetHandle(), false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not adjust token privileges");
            }
        }

        /// <summary>
        /// Returns a duplicate of given process handle owned by the process
        /// </summary>
        /// <param name="sourceHandle">The process handle to be duplicated</param>
        /// <param name="desiredRights">The desired rights to the process associated with given handle</param>
        /// <param name="inherit">Whether the new handle is inheritable</param>
        /// <param name="duplicationOptions">The duplication options to use when duplicating</param>
        /// <returns>The newly duplicated handle</returns>
        /// <exception cref="Win32Exception">On windows api error</exception>
        public SafeProcessHandle DuplicateHandle(IntPtr sourceHandle, ProcessRights desiredRights,
            Boolean inherit, DuplicationOptions duplicationOptions)
        {
            IntPtr targetHandle = IntPtr.Zero;
            if (!Kernel32.DuplicateHandle(Handle.DangerousGetHandle(), sourceHandle, IntPtrExtension.Invalid,
                ref targetHandle, desiredRights, inherit, duplicationOptions))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not duplicate handle");

            return new SafeProcessHandle(targetHandle, true);
        }

        /// <summary>
        /// Returns a duplicate of given process handle owned by the process with equal access rights to the source handle
        /// </summary>
        /// <param name="sourceHandle">The process handle to be duplicated</param>
        /// <param name="inherit">Whether the new handle is inheritable</param>
        /// <returns>The newly duplicated handle</returns>
        /// <exception cref="Win32Exception">On windows api error</exception>
        public SafeProcessHandle DuplicateHandle(IntPtr sourceHandle, Boolean inherit)
        {
            return DuplicateHandle(sourceHandle, ProcessRights.None, inherit, DuplicationOptions.SameAccess);
        }

        /// <summary>
        /// The active handle to the process
        /// </summary>
        public SafeProcessHandle Handle { get; private set; }

        /// <summary>
        /// The id of the process
        /// </summary>
        public Int32 Id
        {
            get { return Convert.ToInt32(Kernel32.GetProcessId(Handle.DangerousGetHandle())); }
        }

        /// <summary>
        /// The full path of the file used to start the process
        /// </summary>
        public string FullPath
        {
            get
            {
                uint capacity = 1024;
                StringBuilder builder = new StringBuilder(Convert.ToInt32(capacity));
                if(!Kernel32.QueryFullProcessImageName(Handle.DangerousGetHandle(), 0, builder, ref capacity))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not retrieve process filename");

                return builder.ToString(0, Convert.ToInt32(capacity));
            }
        }

        /// <summary>
        /// The filename of the file used to start the process
        /// </summary>
        public string FileName
        {
            get { return Path.GetFileName(FullPath); }
        }

        /// <summary>
        /// The collection of modules that are loaded into this process
        /// </summary>
        public ICollection<Module> Modules
        {
            get
            {
                IntPtr snapshot = Kernel32.CreateToolhelp32Snapshot(SnapshotFlags.Module | SnapshotFlags.NoHeaps, Convert.ToUInt32(Id));
                if(snapshot.IsInvalidHandle())
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create module snapshot");

                try
                {
                    Kernel32.ModuleEntry32 moduleEntry = new Kernel32.ModuleEntry32();
                    moduleEntry.StructSize = Convert.ToUInt32(Marshal.SizeOf<Kernel32.ModuleEntry32>());
                    if (!Kernel32.Module32First(snapshot, ref moduleEntry))
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "Could not load the first module from the snapshot");

                    List<Module> modules = new List<Module>();
                    do
                    {
                        Module module = new Module();
                        module.BaseAddress = moduleEntry.BaseAddress;
                        module.BaseSize = Convert.ToInt32(moduleEntry.BaseSize);
                        module.FullPath = moduleEntry.FullPath;

                        modules.Add(module);
                    }
                    while (Kernel32.Module32Next(snapshot, ref moduleEntry));

                    int code = Marshal.GetLastWin32Error();
                    if(code != (int)ErrorCode.NoMoreFiles)
                        throw new Win32Exception(code, "Could not load the next module from the snapshot");

                    return modules;
                }
                finally
                {
                    if(!Kernel32.CloseHandle(snapshot))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not close snapshot handle");
                }
            }
        }

        /// <summary>
        /// The main module of the process
        /// </summary>
        public Module MainModule
        {
            get { return Modules.Single(module => module.FileName == FileName); }
        }

        /// <summary>
        /// Internal buffer used in some operations
        /// </summary>
        private byte[] Buffer { get; set; }
    }
}
