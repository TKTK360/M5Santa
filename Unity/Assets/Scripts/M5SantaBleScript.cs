using UnityEngine;
using UnityEngine.UI;


public class M5SantaBleScript : MonoBehaviour
{
    protected string DeviceName = "M5Santa";
    protected string ServiceUUID = "8f6156ca-48cc-11eb-b378-0242ac130002";
    protected string SubscribeCharacteristic = "8f61590e-48cc-11eb-b378-0242ac130002";
    protected string WriteCharacteristic = "8f615b70-48cc-11eb-b378-0242ac130002";

    public Text StateText;
    public Text LogText;
    public Text Log2Text;
    protected string _deviceName = string.Empty;
    protected string _serviceName = string.Empty;

    private bool _connected = false;
    private float _timeout = 0f;
    private States _state = States.None;
    private string _deviceAddress;
    private bool _foundSubscribeID = false;
    private bool _foundWriteID = false;
    private byte[] _dataBytes = null;
    private bool _rssiOnly = false;
    private int _rssi = 0;
    private bool _isON = false;

    public AudioSource audioSound;

    /// <summary>
    /// States
    /// </summary>
    public enum States
    {
        None,
        Scan,
        ScanRSSI,
        Connect,
        Subscribe,
        Unsubscribe,
        Disconnect,
        Write,
    }


    public States State
    {
        get
        {
            return _state;
        }
    }


    /// <summary>
    /// Start
    /// </summary>
    void Start()
    {
        //StartProcess();
    }


    /// <summary>
    /// Update
    /// </summary>
    void Update()
    {
        if (_timeout > 0f)
        {
            _timeout -= Time.deltaTime;
            if (_timeout > 0f)
            {
                return;
            }

            _timeout = 0f;


            switch (_state)
            {
                //None
                case States.None:
                    break;

                //Scan
                case States.Scan:
                    BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, (address, name) =>
                    {
                        // if your device does not advertise the rssi and manufacturer specific data
                        // then you must use this callback because the next callback only gets called
                        // if you have manufacturer specific data
                        // デバイスがrssiおよびメーカー固有のデータをアドバタイズしない場合、次のコールバックはメーカー固有のデータがある場合にのみ呼び出されるため、このコールバックを使用する必要があります。

                        _deviceName += string.Format(",{0} ", name);

                        if (!_rssiOnly)
                        {
                            if (name.Contains(DeviceName))
                            {
                                _deviceName += string.Format("[FIND]-{0}\n", _deviceAddress);

                                BluetoothLEHardwareInterface.StopScan();

                                // found a device with the name we want
                                // this example does not deal with finding more than one
                                _deviceAddress = address;
                                SetState(States.Connect, 0.5f);
                            }
                            else
                            {
                                _deviceName += "\n";
                            }
                        }

                        if (LogText != null)
                        {
                            LogText.text = _deviceName;
                        }


                    }, (address, name, rssi, bytes) =>
                    {
                        // use this one if the device responses with manufacturer specific data and the rssi
                        // デバイスがメーカー固有のデータとrssiで応答する場合、これを使用します
                        if (name.Contains(DeviceName))
                        {
                            if (_rssiOnly)
                            {
                                _rssi = rssi;
                            }
                            else
                            {
                                BluetoothLEHardwareInterface.StopScan();

                                // found a device with the name we want
                                // this example does not deal with finding more than one
                                _deviceAddress = address;
                                SetState(States.Connect, 0.5f);
                            }
                        }

                    }, _rssiOnly); // this last setting allows RFduino to send RSSI without having manufacturer data

                    if (_rssiOnly)
                    {
                        SetState(States.ScanRSSI, 0.5f);
                    }

                    break;

                //ScanRSSI
                case States.ScanRSSI:
                    break;

                //Connect
                case States.Connect:
                    // set these flags
                    _foundSubscribeID = false;
                    _foundWriteID = false;

                    // note that the first parameter is the address, not the name. I have not fixed this because
                    // of backwards compatiblity.
                    // also note that I am note using the first 2 callbacks. If you are not looking for specific characteristics you can use one of
                    // the first 2, but keep in mind that the device will enumerate everything and so you will want to have a timeout
                    // large enough that it will be finished enumerating before you try to subscribe or do any other operations.
                    // 最初のパラメータは名前ではなくアドレスであることに注意してください。後方互換性のため、これは修正していません。
                    // また、最初の2つのコールバックを使用していることに注意してください。特定の特性を探していない場合は、最初の2つのいずれかを使用できますが、
                    // デバイスはすべてを列挙するので、サブスクライブする前に列挙が完了するまでタイムアウトを大きくする必要があることに注意してください。他の操作を行います。

                    BluetoothLEHardwareInterface.ConnectToPeripheral(_deviceAddress, null, null, (address, serviceUUID, characteristicUUID) =>
                    {
                        _serviceName += string.Format("{0} ", serviceUUID);


                        if (IsEqual(serviceUUID, ServiceUUID))
                        {
                            _serviceName += string.Format("[FIND]-{0}\n", _deviceAddress);

                            _foundSubscribeID = _foundSubscribeID || IsEqual(characteristicUUID, SubscribeCharacteristic);
                            _foundWriteID = _foundWriteID || IsEqual(characteristicUUID, WriteCharacteristic);


                            _serviceName += string.Format("<{0}>,{1},{2}\n", characteristicUUID, _foundSubscribeID, _foundWriteID);


                            // if we have found both characteristics that we are waiting for
                            // set the state. make sure there is enough timeout that if the
                            // device is still enumerating other characteristics it finishes
                            // before we try to subscribe
                            //if (_foundSubscribeID && _foundWriteID)
                            if (_foundSubscribeID)
                            {
                                _connected = true;
                                SetState(States.Subscribe, 2f);
                            }
                        }
                        else
                        {
                            _serviceName += "\n";
                        }

                        if (Log2Text != null)
                        {
                            Log2Text.text = _serviceName;
                        }

                    });
                    break;

                // Subscribe
                case States.Subscribe:
                    BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(_deviceAddress, ServiceUUID, SubscribeCharacteristic, null, (address, characteristicUUID, bytes) =>
                    {
                        // we don't have a great way to set the state other than waiting until we actually got
                        // some data back. For this demo with the rfduino that means pressing the button
                        // on the rfduino at least once before the GUI will update.
                        // 実際にデータが返されるまで待つ以外に、状態を設定する優れた方法はありません。このrfduinoのデモでは、GUIが更新される前にrfduinoのボタンを少なくとも1回押すことを意味します。
                        _state = States.None;

                        // we received some data from the device
                        _dataBytes = bytes;

                    });
                    break;

                // Unsubscribe
                case States.Unsubscribe:
                    BluetoothLEHardwareInterface.UnSubscribeCharacteristic(_deviceAddress, ServiceUUID, SubscribeCharacteristic, null);
                    SetState(States.Disconnect, 4f);
                    break;

                // Disconnect
                case States.Disconnect:
                    if (_connected)
                    {
                        BluetoothLEHardwareInterface.DisconnectPeripheral(_deviceAddress, (address) =>
                        {
                            BluetoothLEHardwareInterface.DeInitialize(() =>
                            {
                                _connected = false;
                                _state = States.None;
                            });
                        });
                    }
                    else
                    {
                        BluetoothLEHardwareInterface.DeInitialize(() =>
                        {
                            _state = States.None;
                        });
                    }
                    break;
            }
        }
    }



    /// <summary>
    /// Reset
    /// </summary>
    void Reset()
    {
        _deviceName = string.Empty;
        _serviceName = string.Empty;


        _connected = false;
        _timeout = 0f;
        _state = States.None;
        _deviceAddress = null;
        _foundSubscribeID = false;
        _foundWriteID = false;
        _dataBytes = null;
        _rssi = 0;
    }


    /// <summary>
    /// SetState
    /// </summary>
    /// <param name="newState"></param>
    /// <param name="timeout"></param>
    void SetState(States newState, float timeout)
    {
        _state = newState;
        _timeout = timeout;


        //State
        if (StateText != null)
        {
            StateText.text = _state.ToString();
        }
    }



    /// <summary>
    /// StartProcess
    /// </summary>
    public void StartProcess()
    {
        Reset();

        BluetoothLEHardwareInterface.Initialize(true, false, () =>
        {
            SetState(States.Scan, 0.1f);

        }, (error) =>
        {

            BluetoothLEHardwareInterface.Log("Error during initialize: " + error);
        });
    }


    /// <summary>
    /// StopProcess
    /// </summary>
    public void StopProcess()
    {
        if (_state == States.ScanRSSI)
        {
            BluetoothLEHardwareInterface.StopScan();
            SetState(States.Disconnect, 0.5f);
        }
    }


    /// <summary>
    /// WriteData
    /// </summary>
    /// <param name="str"></param>
    public void WriteToggleData()
    {
        _isON = !_isON;
        var strData = _isON ? "1" : "0";

        //BGM
        if (audioSound != null)
        {
            if (_isON)
            {
                audioSound.Play();
            }
            else
            {
                audioSound.Stop();
            }
        }

        //Send Device
        WriteData(strData);
    }


    /// <summary>
    /// WriteData
    /// </summary>
    /// <param name="str"></param>
    public bool WriteData(string str)
    {
        if (!_connected)
        {
            return false;
        }

        SetState(States.Write, 0.5f);

        var data = System.Text.Encoding.UTF8.GetBytes(str);
        SendBytes(data);

        return true;
    }



    string FullUUID(string uuid)
    {
        return "0000" + uuid + "-0000-1000-8000-00805f9b34fb";
    }

    bool IsEqual(string uuid1, string uuid2)
    {
        if (uuid1.Length == 4)
        {
            uuid1 = FullUUID(uuid1);
        }
        if (uuid2.Length == 4)
        {
            uuid2 = FullUUID(uuid2);
        }

        return (uuid1.ToUpper().CompareTo(uuid2.ToUpper()) == 0);
    }

    void SendBytes(byte[] data)
    {
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, ServiceUUID, WriteCharacteristic, data, data.Length, true, (characteristicUUID) =>
        {
            BluetoothLEHardwareInterface.Log("Write Succeeded");
        });
    }

    void SendByte(byte value)
    {
        var data = new byte[] { value };
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, ServiceUUID, WriteCharacteristic, data, data.Length, true, (characteristicUUID) =>
        {
            BluetoothLEHardwareInterface.Log("Write Succeeded");
        });
    }
}


