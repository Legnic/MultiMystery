using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CoopDemo
{
    // 이 컴퓨터에 연결된 오디오 장치 이름 목록 (음성 채팅 연결 여부와 상관없이 언제든 읽을 수 있다).
    // - 마이크: Unity Microphone.devices
    // - 스피커(출력): Windows 오디오 장치 API(Core Audio, MMDevice)로 "사용 중"인 재생 장치를 읽는다. Windows가 아니면 빈 목록.
    // 장치 이름은 Windows에 표시되는 이름 그대로라서 Vivox 장치 이름과 같다 → 음성이 연결되면 이름으로 맞춰 쓴다.
    public static class SystemAudioDevices
    {
        public static string[] Microphones()
        {
            try { return Microphone.devices ?? Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }

        // 재생 장치 이름 목록과 Windows 기본 재생 장치 이름
        public static List<string> Speakers(out string defaultName)
        {
            var list = new List<string>();
            defaultName = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IMMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                if (enumerator.EnumAudioEndpoints(DataFlowRender, DeviceStateActive, out var collection) == 0 && collection != null)
                {
                    collection.GetCount(out uint count);
                    for (uint i = 0; i < count; i++)
                    {
                        if (collection.Item(i, out var device) != 0 || device == null) continue;
                        string name = FriendlyName(device);
                        if (!string.IsNullOrEmpty(name)) list.Add(name);
                        Marshal.ReleaseComObject(device);
                    }
                    Marshal.ReleaseComObject(collection);
                }
                if (enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleMultimedia, out var def) == 0 && def != null)
                {
                    defaultName = FriendlyName(def);
                    Marshal.ReleaseComObject(def);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SystemAudioDevices] 재생 장치 목록을 읽지 못했습니다: " + e.Message);
            }
            finally
            {
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
#endif
            return list;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const int DataFlowRender = 0;      // eRender
        const int RoleMultimedia = 1;      // eMultimedia
        const int DeviceStateActive = 1;   // DEVICE_STATE_ACTIVE
        static readonly PropertyKey FriendlyNameKey = new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

        static string FriendlyName(IMMDevice device)
        {
            if (device.OpenPropertyStore(0 /* STGM_READ */, out var store) != 0 || store == null) return null;
            try
            {
                var key = FriendlyNameKey;
                var pv = new PropVariant();
                if (store.GetValue(ref key, out pv) != 0) return null;
                string name = pv.vt == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(pv.pointerValue) : null;
                PropVariantClear(ref pv);
                return name;
            }
            finally { Marshal.ReleaseComObject(store); }
        }

        [DllImport("ole32.dll")]
        static extern int PropVariantClear(ref PropVariant pvar);

        [StructLayout(LayoutKind.Sequential)]
        struct PropertyKey
        {
            public Guid fmtid;
            public int pid;
            public PropertyKey(Guid f, int p) { fmtid = f; pid = p; }
        }

        [StructLayout(LayoutKind.Explicit)]
        struct PropVariant
        {
            [FieldOffset(0)] public short vt;
            [FieldOffset(8)] public IntPtr pointerValue;
            [FieldOffset(16)] public IntPtr padding;   // 구조체 크기를 실제 PROPVARIANT(64비트 24바이트)와 맞춤
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetAt(int index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
            [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
            [PreserveSig] int Commit();
        }
#endif
    }
}
