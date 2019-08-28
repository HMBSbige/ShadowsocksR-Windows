using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace Shadowsocks.Util.SingleInstance
{
    /// <summary>
    /// Enforces single instance for an application.
    /// </summary>
    public sealed class SingleInstance : IDisposable
    {
        private Mutex _mutex;
        private readonly bool _ownsMutex;
        private readonly string _identifier;

        /// <summary>
        /// Enforces single instance for an application.
        /// </summary>
        /// <param name="identifier">An identifier unique to this application.</param>
        public SingleInstance(string identifier)
        {
            _identifier = identifier;
            _mutex = new Mutex(true, identifier, out _ownsMutex);
        }

        /// <summary>
        /// Indicates whether this is the first instance of this application.
        /// </summary>
        public bool IsFirstInstance => _ownsMutex;

        /// <summary>
        /// Passes the given arguments to the first running instance of the application.
        /// </summary>
        /// <param name="arguments">The arguments to pass.</param>
        /// <returns>Return true if the operation succeeded, false otherwise.</returns>
        public bool PassArgumentsToFirstInstance(IEnumerable<string> arguments)
        {
            if (IsFirstInstance)
                throw new InvalidOperationException("This is the first instance.");

            try
            {
                using (var client = new NamedPipeClientStream(_identifier))
                using (var writer = new StreamWriter(client))
                {
                    client.Connect(200);

                    foreach (var argument in arguments)
                        writer.WriteLine(argument);
                }
                return true;
            }
            catch (TimeoutException)
            { } //Couldn't connect to server
            catch (IOException)
            { } //Pipe was broken

            return false;
        }

        /// <summary>
        /// Listens for arguments being passed from successive instances of the application.
        /// </summary>
        public void ListenForArgumentsFromSuccessiveInstances()
        {
            if (!IsFirstInstance)
                throw new InvalidOperationException("This is not the first instance.");
            ThreadPool.QueueUserWorkItem(ListenForArguments);
        }

        /// <summary>
        /// Listens for arguments on a named pipe.
        /// </summary>
        /// <param name="state">State object required by WaitCallback delegate.</param>
        // ReSharper disable once FunctionRecursiveOnAllPaths
        private void ListenForArguments(object state)
        {
            try
            {
                using var server = new NamedPipeServerStream(_identifier);
                using var reader = new StreamReader(server);
                server.WaitForConnection();

                var arguments = new List<string>();
                while (server.IsConnected)
                    arguments.Add(reader.ReadLine());

                ThreadPool.QueueUserWorkItem(CallOnArgumentsReceived, arguments.ToArray());
            }
            catch (IOException)
            { } //Pipe was broken
            finally
            {
                ListenForArguments(null);
            }
        }

        /// <summary>
        /// Calls the OnArgumentsReceived method casting the state Object to String[].
        /// </summary>
        /// <param name="state">The arguments to pass.</param>
        private void CallOnArgumentsReceived(object state)
        {
            OnArgumentsReceived((string[])state);
        }

        /// <summary>
        /// Event raised when arguments are received from successive instances.
        /// </summary>
        public event EventHandler<ArgumentsReceivedEventArgs> ArgumentsReceived;

        /// <summary>
        /// Fires the ArgumentsReceived event.
        /// </summary>
        /// <param name="arguments">The arguments to pass with the ArgumentsReceivedEventArgs.</param>
        private void OnArgumentsReceived(string[] arguments)
        {
            ArgumentsReceived?.Invoke(this, new ArgumentsReceivedEventArgs { Args = arguments });
        }

        #region IDisposable
        private bool _disposed;

        // ReSharper disable once UnusedParameter.Local
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_mutex != null && _ownsMutex)
                {
                    _mutex.ReleaseMutex();
                    _mutex = null;
                }
                _disposed = true;
            }
        }

        ~SingleInstance()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
