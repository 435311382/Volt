# Adding a device

Volt's catalogue is a list of `DeviceDefinition` objects. Each one names a physical device and lists the HID endpoints it can be reached through.

Adding a model is usually one entry. Adding a protocol family is one `Read` method.

---

## 1. Find the endpoints

Run a HID enumeration and look for collections with a vendor usage page:

```
VendorId ProductId  UsagePage/Usage  InLen OutLen  Product
373B     10C9       FF02/0002        17    17      NK mouse NANO dongle
3554     FA09       FF02/0002        20    20      2.4G Wireless Receiver
```

You want a collection where input and output lengths are equal and non-zero — that is the query channel. Consumer control (`000C/0001`) and mouse (`0001/0002`) collections are not it.

If the same physical device appears under several VID/PID pairs (dongle, cable, Bluetooth), add them all as endpoints of **one** definition. Volt then shows one row regardless of how it is connected.

---

## 2. Get a reading

Send the family's request frame and inspect the reply. See [PROTOCOLS.md](PROTOCOLS.md) for the two known families.

To find the percentage byte: take readings at different charge levels and look for a byte that tracks the number. To find the charging flag: take one reading with the cable plugged in and one without, and diff.

---

## 3. Add the definition

In `src/Protocols.cs`, extend `Catalog.Build()`:

```csharp
DeviceDefinition mouse = new DeviceDefinition();
mouse.Key = "atk-compx";                  // selects the protocol handler
mouse.DisplayName = "ATK X1 SE";          // shown in the UI — use the real name
mouse.Kind = "鼠标";                       // drives the row icon
mouse.Vendor = "ATK";
mouse.Endpoints = new Endpoint[]
{
    new Endpoint { Vid = 0x373B, Pid = 0x10C9, UsagePage = 0xFF02, Usage = 0x0002,
                   InputLength = 17, OutputLength = 17, Transport = "2.4G 接收器" },
    new Endpoint { Vid = 0x373B, Pid = 0x1107, UsagePage = 0xFF02, Usage = 0x0002,
                   InputLength = 17, OutputLength = 17, Transport = "USB 有线" },
};
list.Add(mouse);
```

`Endpoint.Matches` compares VID, PID, usage page, usage and both report lengths. All six must line up — that is what stops Volt from grabbing the wrong collection when a dongle exposes two with the same usage.

If your device speaks an existing family, that is the whole change. `Key` already routes to the right handler.

---

## 4. New protocol family

If the frame layout is different, add a handler in `src/Protocols.cs` and route to it from `DeviceDefinition.Read`:

```csharp
public static class MyVendor
{
    public static BatteryReading Read(HidInterface iface)
    {
        BatteryReading r = new BatteryReading();

        byte[] req = new byte[iface.OutputLength];
        req[0] = 0x13;
        req[1] = 0xCA;

        byte[] resp = HidQuery.Run(iface, req, 4, 400, 120,
            new Func<byte[], bool>(got => got != null && got.Length >= 7 && got[0] == 0x13));

        if (resp == null) { r.Note = "no reply"; return r; }

        r.Percent = resp[5];
        r.Charge = resp[6] == 0x10 ? ChargeState.Charging : ChargeState.Discharging;
        r.Ok = r.Percent >= 0 && r.Percent <= 100;
        return r;
    }
}
```

`HidQuery.Run` opens a **fresh handle per attempt** and closes it before retrying. That is deliberate: a timed-out overlapped read stays pending on the handle and will swallow the next reply, so retrying on the same handle fails forever. Closing the handle cancels the pending I/O.

---

## 5. Verify

```bat
build.bat
Volt.exe
```

Check three things:

1. The percentage matches what the vendor software reports.
2. Unplug the cable — the row should switch transport, not duplicate.
3. Leave the device idle for a few minutes — the row should show `last HH:mm`, not go blank.

If the name is wrong, the match is probably too loose. Prefer VID/PID plus report lengths over product strings; product strings differ between dongles of the same model.
