using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Collections.Generic;

namespace Shared.Windows
{
    using static NativeImports;

    public class BluetoothRadio : IDisposable
    {
        private SafeObjectHandle handle;

        public static List<BluetoothRadio> FindAllRadios()
        {
            var btRadios = new List<BluetoothRadio>();

            var radioParams = BLUETOOTH_FIND_RADIO_PARAMS.Create();
            var searchHandle = NativeImports.BluetoothFindFirstRadio(in radioParams, out var radioHandle);

            bool moreDevices = searchHandle != null && !searchHandle.IsInvalid && radioHandle != null && !radioHandle.IsInvalid;
            while (moreDevices)
            {
                if (radioHandle != null && !radioHandle.IsInvalid)
                {
                    btRadios.Add(new BluetoothRadio(radioHandle));
                }

                moreDevices = NativeImports.BluetoothFindNextRadio(searchHandle, out radioHandle);
            }

            if (searchHandle != null && !searchHandle.IsInvalid)
                searchHandle.Dispose();

            return btRadios;
        }

        private BluetoothRadio(SafeObjectHandle radioHandle)
        {
            handle = radioHandle;
        }

        ~BluetoothRadio()
        {
            Dispose(disposing: false);
        }

        public bool TryGetInfo(out BLUETOOTH_RADIO_INFO radioInfo)
        {
            radioInfo = BLUETOOTH_RADIO_INFO.Create();
            uint result = NativeImports.BluetoothGetRadioInfo(handle, ref radioInfo);

            return result == 0;
        }

        public List<BluetoothDevice> FindAllDevices(
            bool includeUnknown = true, bool includeConnected = true, bool includeRemembered = true,
            bool includeAuthenticated = true, bool issueInquiry = true, byte timeoutMultiplier = 2)
        {
            if (!TryGetInfo(out _))
                return null;

            // Set search parameters
            var searchParams = BLUETOOTH_DEVICE_SEARCH_PARAMS.Create();
            searchParams.hRadio = handle.DangerousGetHandle();
            searchParams.fIssueInquiry = issueInquiry;
            searchParams.fReturnUnknown = includeUnknown;
            searchParams.fReturnConnected = includeConnected;
            searchParams.fReturnRemembered = includeRemembered;
            searchParams.fReturnAuthenticated = includeAuthenticated;
            searchParams.cTimeoutMultiplier = timeoutMultiplier;

            // Search for devices
            var devices = new List<BluetoothDevice>();
            var deviceInfo = BLUETOOTH_DEVICE_INFO.Create();
            var searchHandle = NativeImports.BluetoothFindFirstDevice(in searchParams, ref deviceInfo);

            bool more = searchHandle != null && !searchHandle.IsInvalid;
            while (more)
            {
                devices.Add(new BluetoothDevice(this, deviceInfo));
                more = NativeImports.BluetoothFindNextDevice(searchHandle, ref deviceInfo);
            }

            if (searchHandle != null && !searchHandle.IsInvalid)
                searchHandle.Dispose();

            return devices;
        }

        public uint AuthenticateDevice(in BLUETOOTH_DEVICE_INFO deviceInfo, string password)
        {
            return NativeImports.BluetoothAuthenticateDevice(IntPtr.Zero, handle, in deviceInfo, password, (uint)password.Length);
        }

        public uint EnumerateInstalledServices(in BLUETOOTH_DEVICE_INFO deviceInfo, out Guid[] serviceGuids)
        {
            uint serviceCount = 0;
            uint result = NativeImports.BluetoothEnumerateInstalledServices(handle, in deviceInfo, ref serviceCount, null);
            if (result != 0 || serviceCount == 0)
            {
                serviceGuids = null;
                return result;
            }

            serviceGuids = new Guid[serviceCount];
            return NativeImports.BluetoothEnumerateInstalledServices(handle, in deviceInfo, ref serviceCount, serviceGuids);
        }

        public uint SetServiceState(in BLUETOOTH_DEVICE_INFO deviceInfo, Guid serviceGuid, bool enable)
        {
            return NativeImports.BluetoothSetServiceState(handle, in deviceInfo, in serviceGuid, enable ? BluetoothServiceFlag.Enable : BluetoothServiceFlag.Disable);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (handle != null && !handle.IsInvalid)
                {
                    handle.Dispose();
                    handle = null;
                }
            }
        }
    }
}