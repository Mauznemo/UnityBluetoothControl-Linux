using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BluetoothUI : MonoBehaviour
{
    [SerializeField] private Button scanButton;
    [SerializeField] private Button lockButton;
    [SerializeField] private Button listDevicesButton;
    [SerializeField] private InputField deviceAddressInput;
    [SerializeField] private TMP_Text lockButtonText;

    bool blocked;

    void Start()
    {
        blocked = BluetoothManager.Instance.IsSoftBlocked();
        lockButtonText.text = blocked ? "Bluetooth: Off" : "Bluetooth: On";

        scanButton.onClick.AddListener(BluetoothManager.Instance.ScanDevices);
        lockButton.onClick.AddListener(() => {
            blocked = !blocked;
            BluetoothManager.Instance.SetBluetoothLock(blocked);
            lockButtonText.text = blocked ? "Bluetooth: Off" : "Bluetooth: On";
        });
        listDevicesButton.onClick.AddListener(() => {
            var devices = BluetoothManager.Instance.ListPairedDevices();
            foreach(var device in devices)
            {
                BluetoothManager.Instance.OnDeviceFound(device.name, device.macAddress, true);
            }
        });
    }
}
