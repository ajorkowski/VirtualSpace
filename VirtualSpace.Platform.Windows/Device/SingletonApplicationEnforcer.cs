﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace VirtualSpace.Platform.Windows.Device
{
    // http://danielvaughan.org/post/Enforcing-Single-Instance-WPF-Applications.aspx

    /// <summary>
    /// This class allows restricting the number of executables in execution, to one.
    /// </summary>
    public sealed class SingletonApplicationEnforcer : IDisposable
    {
        private readonly Action<IEnumerable<string>> _processArgsFunc;
        private readonly string _applicationId;

        private EventWaitHandle _eventWaitHandle;
        private Task _task;
        private string _argDelimiter = "_;;_";
        private bool _isRunning;

        /// <summary>
        /// Gets or sets the string that is used to join 
        /// the string array of arguments in memory.
        /// </summary>
        /// <value>The arg delimeter.</value>
        public string ArgDelimeter
        {
            get
            {
                return _argDelimiter;
            }
            set
            {
                _argDelimiter = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SingletonApplicationEnforcer"/> class.
        /// </summary>
        /// <param name="processArgsFunc">A handler for processing command line args 
        /// when they are received from another application instance.</param>
        /// <param name="applicationId">The application id used 
        /// for naming the <seealso cref="EventWaitHandle"/>.</param>
        public SingletonApplicationEnforcer(Action<IEnumerable<string>> processArgsFunc, string applicationId)
        {
            if (processArgsFunc == null)
            {
                throw new ArgumentNullException("processArgsFunc");
            }
            this._processArgsFunc = processArgsFunc;
            this._applicationId = applicationId;
        }

        /// <summary>
        /// Determines if this application instance is not the singleton instance.
        /// If this application is not the singleton, then it should exit.
        /// </summary>
        /// <returns><c>true</c> if the application should shutdown, 
        /// otherwise <c>false</c>.</returns>
        public bool ShouldApplicationExit()
        {
            if(_isRunning)
            {
                throw new InvalidOperationException();
            }

            bool createdNew;
            string argsWaitHandleName = "ArgsWaitHandle_" + _applicationId;
            string memoryFileName = "ArgFile_" + _applicationId;

            _isRunning = true;
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, argsWaitHandleName, out createdNew);

            if (createdNew)
            {
                /* This is the main, or singleton application. 
                    * A thread is created to service the MemoryMappedFile. 
                    * We repeatedly examine this file each time the argsWaitHandle 
                    * is Set by a non-singleton application instance. */
                _task = Task.Run(() =>
                {
                    try
                    {
                        using (MemoryMappedFile file = MemoryMappedFile.CreateOrOpen(memoryFileName, 10000))
                        {
                            while (_isRunning)
                            {
                                _eventWaitHandle.WaitOne();
                                if(!_isRunning)
                                {
                                    break;
                                }

                                using (MemoryMappedViewStream stream = file.CreateViewStream())
                                {
                                    var reader = new BinaryReader(stream);
                                    string args;
                                    try
                                    {
                                        args = reader.ReadString();
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine("Unable to retrieve string. " + ex);
                                        continue;
                                    }
                                    string[] argsSplit = args.Split(new string[] { _argDelimiter }, StringSplitOptions.RemoveEmptyEntries);
                                    _processArgsFunc(argsSplit);
                                }

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Unable to monitor memory file. " + ex);
                    }
                });
            }
            else
            {
                /* Non singleton application instance. 
                    * Should exit, after passing command line args to singleton process, 
                    * via the MemoryMappedFile. */
                using (var mmf = MemoryMappedFile.OpenExisting(memoryFileName))
                using (var stream = mmf.CreateViewStream())
                {
                    var writer = new BinaryWriter(stream);
                    string[] args = Environment.GetCommandLineArgs();
                    string joined = string.Join(_argDelimiter, args);
                    writer.Write(joined);
                }
                _eventWaitHandle.Set();
            }

            return !createdNew;
        }

        public void Dispose()
        {
            if (_isRunning)
            {
                _isRunning = false;
                _eventWaitHandle.Set();
                _task.Wait();
                _task.Dispose();
                _task = null;
                _eventWaitHandle.Dispose();
                _eventWaitHandle = null;
            }
        }
    }
}
