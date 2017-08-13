﻿using System;
using System.IO;

namespace Edo
{
    /// <summary>
    /// Provides information about a single module within a process
    /// </summary>
    public class Module
    {
        /// <summary>
        /// The base address of the module in virtual memory
        /// </summary>
        public IntPtr BaseAddress { get; set; }

        /// <summary>
        /// The full filename of the module, including path
        /// </summary>
        public String FullPath { get; set; }

        /// <summary>
        /// The name of the module file
        /// </summary>
        public String FileName
        {
            get { return Path.GetFileName(FullPath); }
        }

        /// <summary>
        /// The base size of the module in virtual memory
        /// </summary>
        public Int32 BaseSize { get; set; }
    }
}