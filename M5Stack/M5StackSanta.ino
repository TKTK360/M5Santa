#include <M5StickC.h>

// Bluetooth LE
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

#define BLE_NAME                  "M5Santa"
#define SERVICE_UUID                "8f6156ca-48cc-11eb-b378-0242ac130002"
#define CHARACTERISTIC_UUID_NOTIFY  "8f61590e-48cc-11eb-b378-0242ac130002"
#define CHARACTERISTIC_UUID_RX      "8f615b70-48cc-11eb-b378-0242ac130002"

//Relay
#define RELAY_PIN 33


//-----------------------------------------------------------------------------
//Relay
//-----------------------------------------------------------------------------
//button
bool _isRelay = false;

uint16_t getColor(uint8_t red, uint8_t green, uint8_t blue)
{
  return ((red>>3)<<11) | ((green>>2)<<5) | (blue>>3);
}

void RelayON()
{
  digitalWrite(RELAY_PIN, HIGH);

  M5.Lcd.fillScreen(getColor(255,0,0));
  Serial.println("ON");
}

  
void RelayOFF()
{
  digitalWrite(RELAY_PIN, LOW);
  
  M5.Lcd.fillScreen(getColor(0,0,255));
  Serial.println("OFF");
}


//-----------------------------------------------------------------------------
//BLE
//-----------------------------------------------------------------------------
BLEServer *pServer = NULL;
BLECharacteristic * pNotifyCharacteristic;
bool deviceConnected = false;
bool oldDeviceConnected = false;


// Bluetooth LE Change Connect State
class SantaBleServerCallbacks: public BLEServerCallbacks 
{
    void onConnect(BLEServer* pServer) 
    {
      deviceConnected = true;
    };

    void onDisconnect(BLEServer* pServer) 
    {    
      deviceConnected = false;
    }
};


// Bluetooth LE Recive
class SantaBleCallbacks: public BLECharacteristicCallbacks 
{
    void onWrite(BLECharacteristic *pCharacteristic) 
    {
      std::string rxValue = pCharacteristic->getValue();
      if (rxValue.length() <= 0) 
      {
        return;
      }
      
      String cmd = String(rxValue.c_str());
      Serial.print("Received Value: ");
      Serial.println(cmd);

      //Function
      if (cmd == "1")
      {
        RelayON();
      }
      else if (cmd == "0")
      {
        RelayOFF();
      }
    }
};


// Bluetooth LE initialize
void InitBLE() 
{
  // Create the BLE Device
  BLEDevice::init(BLE_NAME);

  // Create the BLE Server
  pServer = BLEDevice::createServer();
  pServer->setCallbacks(new SantaBleServerCallbacks());

  // Create the BLE Service
  BLEService *pService = pServer->createService(SERVICE_UUID);

  // Create a BLE Characteristic
  pNotifyCharacteristic = pService->createCharacteristic(
                        CHARACTERISTIC_UUID_NOTIFY,
                        BLECharacteristic::PROPERTY_NOTIFY
                        );
  
  pNotifyCharacteristic->addDescriptor(new BLE2902());
  
  BLECharacteristic * pRxCharacteristic = pService->createCharacteristic(
                       CHARACTERISTIC_UUID_RX,
                      BLECharacteristic::PROPERTY_WRITE
                    );

  pRxCharacteristic->setCallbacks(new SantaBleCallbacks());

  // Start the service
  pService->start();

  // Start advertising
  pServer->getAdvertising()->start();
}


// Bluetooth LE loop
void LoopBLE() 
{
    // disconnecting
    if (!deviceConnected && oldDeviceConnected) 
    {
      // give the bluetooth stack the chance to get things ready
        delay(500);

        // restart advertising
        pServer->startAdvertising();
        Serial.println("startAdvertising");
        oldDeviceConnected = deviceConnected;
    }
    
    // connecting
    if (deviceConnected && !oldDeviceConnected) 
    {
      // do stuff here on connecting
        oldDeviceConnected = deviceConnected;
    }
}


//-----------------------------------------------------------------------------
//Main
//-----------------------------------------------------------------------------
void setup() 
{
  M5.begin();

  //Relay
  pinMode(RELAY_PIN, OUTPUT);
  
  RelayOFF();

  //Ble
  InitBLE();
}


void loop() 
{
  M5.update();
  
  //Button
  if (M5.BtnA.wasReleased())
  {
    _isRelay = !_isRelay;
    if (_isRelay)
    {
      RelayON();
    }
    else
    {
      RelayOFF();
    }
  }

  //BLE
  LoopBLE();
}
