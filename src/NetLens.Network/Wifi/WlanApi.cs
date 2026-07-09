using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NetLens.Network.Wifi;

/// <summary>
/// P/Invoke declarations for wlanapi.dll.
/// These native structures match the Windows WLAN API definitions exactly.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WlanApi
{
    private const string WlanLib = "wlanapi.dll";

    // ──────────────────────────────────────────
    // Native function declarations
    // ──────────────────────────────────────────

    [DllImport(WlanLib, SetLastError = true)]
    internal static extern uint WlanOpenHandle(
        uint dwClientVersion,
        IntPtr pReserved,
        out uint pdwNegotiatedVersion,
        out IntPtr phClientHandle);

    [DllImport(WlanLib, SetLastError = true)]
    internal static extern uint WlanCloseHandle(
        IntPtr hClientHandle,
        IntPtr pReserved);

    [DllImport(WlanLib, SetLastError = true)]
    internal static extern uint WlanEnumInterfaces(
        IntPtr hClientHandle,
        IntPtr pReserved,
        out IntPtr ppInterfaceList);

    [DllImport(WlanLib, SetLastError = true)]
    internal static extern uint WlanQueryInterface(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        WLAN_INTF_OPCODE OpCode,
        IntPtr pReserved,
        out uint pdwDataSize,
        ref IntPtr ppData,
        IntPtr pWlanOpcodeValueType);

    [DllImport(WlanLib)]
    internal static extern void WlanFreeMemory(IntPtr pMemory);

    // ──────────────────────────────────────────
    // Native enumerations
    // ──────────────────────────────────────────

    internal enum WLAN_INTF_OPCODE
    {
        wlan_intf_opcode_autoconf_enabled = 1,
        wlan_intf_opcode_background_scan_enabled,
        wlan_intf_opcode_media_streaming_mode,
        wlan_intf_opcode_radio_state,
        wlan_intf_opcode_bss_type,
        wlan_intf_opcode_interface_state,
        wlan_intf_opcode_current_connection,
    }

    internal enum WLAN_INTERFACE_STATE
    {
        wlan_interface_state_not_ready = 0,
        wlan_interface_state_connected,
        wlan_interface_state_ad_hoc_network_formed,
        wlan_interface_state_disconnecting,
        wlan_interface_state_disconnected,
        wlan_interface_state_associating,
        wlan_interface_state_discovering,
        wlan_interface_state_authenticating,
    }

    // ──────────────────────────────────────────
    // Native structures
    // ──────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;
        public WLAN_INTERFACE_STATE isState;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_INTERFACE_INFO_LIST
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
        public WLAN_INTERFACE_INFO InterfaceInfo; // First element (others follow in memory)
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct DOT11_SSID
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;

        public string GetSsid()
        {
            if (ucSSID == null || uSSIDLength == 0) return string.Empty;
            return System.Text.Encoding.UTF8.GetString(ucSSID, 0, (int)uSSIDLength);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DOT11_MAC_ADDRESS
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] ucDot11MacAddress;

        public string ToFormattedString()
        {
            if (ucDot11MacAddress == null) return "00:00:00:00:00:00";
            return string.Join(":", ucDot11MacAddress.Select(b => b.ToString("X2")));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_ASSOCIATION_ATTRIBUTES
    {
        public DOT11_SSID dot11Ssid;
        public uint dot11BssType;
        public DOT11_MAC_ADDRESS dot11Bssid;
        public uint dot11PhyType;
        public uint uDot11PhyIndex;
        public uint wlanSignalQuality; // 0-100
        public uint ulRxRate;          // kbps
        public uint ulTxRate;          // kbps
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_SECURITY_ATTRIBUTES
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool bSecurityEnabled;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bOneXEnabled;
        public uint dot11AuthAlgorithm;
        public uint dot11CipherAlgorithm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WLAN_CONNECTION_ATTRIBUTES
    {
        public WLAN_INTERFACE_STATE isState;
        public uint wlanConnectionMode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;
        public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
        public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
    }

    // dot11PhyType mapping
    internal static string GetPhysicalTypeName(uint phyType) => phyType switch
    {
        1 => "802.11b",
        2 => "802.11a",
        3 => "802.11g",
        4 => "802.11n (Wi-Fi 4)",
        5 => "802.11ac (Wi-Fi 5)",
        7 => "802.11ax (Wi-Fi 6)",
        8 => "802.11be (Wi-Fi 7)",
        _ => $"Unknown ({phyType})"
    };
}
