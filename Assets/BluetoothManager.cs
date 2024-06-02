using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using PimDeWitte.UnityMainThreadDispatcher;

public class BluetoothManager : MonoBehaviour
{
    public static BluetoothManager Instance { get; private set; }

    public static event Action OnPlayerPlaying;
    public static event Action OnPlayerPaused;

    public static event Action<string> OnPlayerTitleChanged;
    public static event Action<string> OnPlayerArtistChanged;
    public static event Action OnPlayerStopped;

    public static event Action OnUpdateUI;
    public static event Action<string> OnRemoveDevice;

    public static event Action<string> OnDeviceConnected;
    public static event Action<string> OnDeviceDisconnected;

    private void Awake()
    {
        Instance = this;
    }

    private Process process;
    private StreamWriter processInputWriter;

    [SerializeField] private DeviceEntry deviceEntryPrefab;
    [SerializeField] private Transform deviceEntryParent;
    [SerializeField] private Transform transformPaired;
    [SerializeField] private Transform transformFound;

    [SerializeField] private List<BluetoothDevice> pairedBluetoothDevices = new List<BluetoothDevice>();
    [SerializeField] private List<BluetoothDevice> bluetoothDevices = new List<BluetoothDevice>();

    private void Start()
    {
        StartBluetoothCtl();

        var devices = ListConnectedDevices();

        if (devices.Count > 0)
        {
            OnDeviceConnected?.Invoke(devices[0].macAddress);
            UnityEngine.Debug.Log($"<b><color=green>Connected to: {devices[0].macAddress}</color></b>");
        }
    }

    public bool IsSoftBlocked()
    {
        string input = LinuxCommand.Run("rfkill list");
        bool softLock = BluetoothParser.IsSoftBlocked(input);
        return softLock;
    }

    public void SetBluetoothLock(bool blocked)
    {
        string command = blocked ? "rfkill block bluetooth" : "rfkill unblock bluetooth";
        LinuxCommand.Run(command);
    }

    public void ScanDevices()
    {
        SendCommand("scan on");
    }

    public void ConnectToDevice(string deviceAddress)
    {
        SendCommand($"connect {deviceAddress}");
    }

    public void DisconnectToDevice(string deviceAddress)
    {
        SendCommand($"disconnect {deviceAddress}");
    }

    public void PairDevice(string deviceAddress)
    {
        SendCommand($"pair {deviceAddress}");
    }

    public void RemoveDevice(string deviceAddress)
    {
        SendCommand($"remove {deviceAddress}");
    }

    public void TrustDevice(string deviceAddress)
    {
        SendCommand($"trust {deviceAddress}");
    }

    public void UntrustDevice(string deviceAddress)
    {
        SendCommand($"untrust {deviceAddress}");
    }

    public List<BluetoothDevice> ListPairedDevices()
    {
        string devices = LinuxCommand.Run("bluetoothctl devices Paired");
        return BluetoothParser.ParseDevices(devices);
    }

    public List<BluetoothDevice> ListBondedDevices()
    {
        string devices = LinuxCommand.Run("bluetoothctl devices Bonded");
        return BluetoothParser.ParseDevices(devices);
    }

    public List<BluetoothDevice> ListTrustedDevices()
    {
        string devices = LinuxCommand.Run("bluetoothctl devices Trusted");
        return BluetoothParser.ParseDevices(devices);
    }

    public List<BluetoothDevice> ListConnectedDevices()
    {
        string devices = LinuxCommand.Run("bluetoothctl devices Connected");
        return BluetoothParser.ParseDevices(devices);
    }

    public BluetoothDeviceInfo GetDeviceInfo(string deviceAddress)
    {
        string info = LinuxCommand.Run($"bluetoothctl info {deviceAddress}");
        UnityEngine.Debug.Log(info);
        return BluetoothParser.ParseDeviceInfo(info);
    }


    public void StartBluetoothCtl()
    {
        // Check if the system is running on Linux
        if (System.Environment.OSVersion.Platform != PlatformID.Unix)
        {
            UnityEngine.Debug.Log("Unsupported platform: This function is intended for Linux systems only.");
            return;
        }

        // Create process start info
        ProcessStartInfo psi = new ProcessStartInfo("bluetoothctl");
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.RedirectStandardInput = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        // Start the process
        process = new Process();
        process.StartInfo = psi;
        process.Start();

        // Read the output
        process.OutputDataReceived += OutputDataReceived;
        process.ErrorDataReceived += ErrorDataReceived;

        processInputWriter = process.StandardInput;

        process.BeginOutputReadLine();
    }

    private void OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        //UnityEngine.Debug.Log(e.Data);
        EventType eventType = BluetoothParser.ParseEventType(e.Data);
        switch (eventType)
        {
            case EventType.NEW_Device:
                UnityEngine.Debug.Log("New device");
                var (macAddress, deviceName) = BluetoothParser.ExtractDeviceInfo(e.Data);
                if (macAddress != null)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() => OnDeviceFound(deviceName, macAddress, false));

                }
                break;
            case EventType.NEW_Transport:
                UnityEngine.Debug.Log("New Transport");
                break;
            case EventType.DEL_Device:
                UnityEngine.Debug.Log("Device deleted");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    OnRemoveDevice?.Invoke(BluetoothParser.ExtractDeviceInfo(e.Data).macAddress));
                break;
            case EventType.DEL_Transport:
                UnityEngine.Debug.Log("Transport deleted");
                break;
            case EventType.CHG_Device:
                UnityEngine.Debug.Log("Device changed");
                UnityMainThreadDispatcher.Instance().Enqueue(() => HandleDeviceChange(e.Data));
                break;
            case EventType.CHG_Transport:
                UnityEngine.Debug.Log("Transport changed");
                break;
            case EventType.CHG_Player:
                UnityMainThreadDispatcher.Instance().Enqueue(() => HandlePlayerChange(e.Data));
                break;
            default:
                break;
        }


        if (e.Data.Contains("Confirm passkey"))
        {
            SendCommand("yes");
            return;
        }
    }
    private void HandleDeviceChange(string input)
    {
        OnUpdateUI?.Invoke();

        var (macAddress, connected, success) = BluetoothParser.ParseDeviceConnection(input);

        if (!success)
            return;

        if (connected)
        {
            OnDeviceConnected?.Invoke(macAddress);
            UnityEngine.Debug.Log($"<b><color=green>Connected to: {macAddress}</color></b>");
        }
        else
        {
            OnDeviceDisconnected?.Invoke(macAddress);
            UnityEngine.Debug.Log($"<b><color=red>Disconnected from: {macAddress}</color></b>");
        }
    }

    private void HandlePlayerChange(string input)
    {
        if (input.Contains("paused"))
        {
            OnPlayerPaused?.Invoke();

            UnityEngine.Debug.Log("Player paused");
            return;
        }
        else if (input.Contains("playing"))
        {
            OnPlayerPlaying?.Invoke();
            UnityEngine.Debug.Log("Player playing");
            return;
        }
        else if (input.Contains("stopped"))
        {
            OnPlayerStopped?.Invoke();
            UnityEngine.Debug.Log("Player Stopped (HIDE)");
            return;
        }

        string titlePattern = @"Track.Title:\s(.+)";
        string artistPattern = @"Track.Artist:\s(.+)";

        Match titleMatch = Regex.Match(input, titlePattern);
        Match artistMatch = Regex.Match(input, artistPattern);

        if (titleMatch.Success)
        {
            // Extract the device name from the first capturing group
            string title = titleMatch.Groups[1].Value.Trim();
            UnityEngine.Debug.Log($"Player title changed to: {title}");

            OnPlayerTitleChanged?.Invoke(title);
            return;
        }
        if (artistMatch.Success)
        {
            // Extract the device name from the first capturing group
            string artist = artistMatch.Groups[1].Value.Trim();
            UnityEngine.Debug.Log($"Player artist changed to: {artist}");

            OnPlayerArtistChanged?.Invoke(artist);
            return;
        }
    }


    ////////////////////////////////////////////////////////////////////////////////

    public void OnDeviceFound(string name, string macAddress, bool paired)
    {
        if (string.IsNullOrEmpty(macAddress)) { return; }

        if (!bluetoothDevices.Exists(device => device.macAddress == macAddress))
        {
            int index = 0;
            if (paired)
            {
                index = transformPaired.GetSiblingIndex();
                bluetoothDevices.Add(new BluetoothDevice
                {
                    name = name,
                    macAddress = macAddress
                });
            }
            else
            {
                index = transformFound.GetSiblingIndex();
                bluetoothDevices.Add(new BluetoothDevice
                {
                    name = name,
                    macAddress = macAddress
                });
            }

            DeviceEntry deviceEntry = Instantiate(deviceEntryPrefab, deviceEntryParent);
            deviceEntry.gameObject.SetActive(true);
            deviceEntry.transform.SetSiblingIndex(index + 1);
            deviceEntry.Init(new BluetoothDevice
            {
                name = name,
                macAddress = macAddress
            });


            UnityEngine.Debug.Log($"<b>Found: {name}</b>");
        }
        else
        {
            OnUpdateUI?.Invoke();
        }
    }

    public void SendPlayerCommand(string command)
    {
        SendCommand("menu player");
        SendCommand(command);
        SendCommand("back");
    }

    public void SendCommand(string command)
    {
        if (process != null && !process.HasExited && processInputWriter != null)
        {
            processInputWriter.WriteLine(command);
            processInputWriter.Flush();  // Ensure the command is sent immediately
        }
    }

    void OnDestroy()
    {
        if (process != null && !process.HasExited)
        {
            process.Kill();
        }

        if (processInputWriter != null)
        {
            processInputWriter.Close();
            processInputWriter = null;
        }
    }
    private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        UnityEngine.Debug.LogError(e.Data);
    }
}
