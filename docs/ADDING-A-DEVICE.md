# Adding a device

<sub>[English](ADDING-A-DEVICE.md) · [简体中文](ADDING-A-DEVICE.zh-CN.md)</sub>

Volt splits device support into two files:

- `src/DeviceTable.cs` — the model table. Answers "which physical device is `VID:PID`?" It knows names, kinds, vendors and the product IDs for each transport.
- `src/ProtocolsVendor.cs` — the protocols. Answers "how do I ask this thing for its battery?" One class per family.

Adding a model is one line in the first file. Adding a family is one class in the second.

---

## 1. Find the endpoints

Run a HID enumeration and look for collections with a vendor usage page:

```
VendorId ProductId  UsagePage/Usage  InLen OutLen  Product
373B     10C9       FF02/0002        17    17      NK mouse NANO dongle
373B     10C9       FF02/0002        20    20      NK mouse NANO dongle
3554     FA09       FF02/0002        20    20      2.4G Wireless Receiver
```

You want a collection where input and output lengths are equal and non-zero — that is the query channel. Consumer control (`000C/0001`) and mouse (`0001/0002`) collections are not it.

**Watch the lengths.** The ATK dongle above exposes *two* collections on the same usage page, and only the 17-byte one accepts the Compx frame. Picking the first match gets you the 20-byte one, every write fails, and the device looks unsupported. That mistake is easy to make and hard to see, so `HidGroup.ByUsage(page, usage, outputLength)` takes the length as a required argument.

If the same physical device appears under several VID/PID pairs (dongle, cable, Bluetooth), list them all in **one** `DeviceModel`: `Wireless` for the dongle PIDs, `Wired` for the cable PIDs. Volt then shows one row regardless of how it is connected.

---

## 2. Get a reading

Send the family's request frame and inspect the reply. See [PROTOCOLS.md](PROTOCOLS.md) for the formats already implemented.

To find the percentage byte: take readings at different charge levels and look for a byte that tracks the number. To find the charging flag: take one reading with the cable plugged in and one without, and diff.

Three transport styles are in use, and they are not interchangeable:

| Style | Helper | When |
|---|---|---|
| Output report, then read | `HidQuery.Run` | Most mice and keyboards. Write a request, wait for the reply. |
| Feature report round-trip | `HidQuery.RunFeature` | Razer, ASUS, Lamzu, Lenovo. Set a feature report, sleep, read it back. Works on a zero-access handle, so it still works while Windows owns the device. |
| Push, no request | `HidQuery.RunPush` | Sony, Audeze, Jabra, Corsair Virtuoso, SteelSeries. The dongle emits periodically; you just listen. |

---

## 3. Add the model

One line in `src/DeviceTable.cs`, inside `All()`:

```csharp
Add(m, "atk-x1-se", "ATK X1 SE", "鼠标", "ATK", 0x373B,
    W(0x10C9), W(0x1107));
```

The arguments are: list, key, display name, kind, vendor, VID, wireless PIDs, wired PIDs. Two optional trailing arguments exist:

```csharp
Add(m, "sony-inzone-buds", "Sony INZONE Buds", "耳机", "Sony", 0x054C,
    W(0x0EC2), new ushort[0], true);
```

`true` marks the device as true-wireless earbuds, which switches the row icon from a headset to a pair of earbuds. The last argument is a note shown when the device cannot report a level:

```csharp
Add(m, "baseus-aequr-g10", "Baseus AeQur G10", "耳机", "Baseus", 0x10D6,
    W(0x4801), new ushort[0], true, "2.4G 接收器不上报电量");
```

Use `W(...)` for PID lists — it takes `int` so you can write hex without casts.

The `key` is the identity used for the on-disk cache and for name overrides in `%LOCALAPPDATA%\Volt\names.tsv`. Keep it stable once shipped; renaming it loses the user's cached readings.

If the device speaks a family that is already implemented, that is the whole change. `ProtocolRegistry.Probe` walks the protocols in priority order and the first one whose `Matches` returns true gets the device.

---

## 4. New protocol family

Subclass `BatteryProtocol` in `src/ProtocolsVendor.cs`:

```csharp
internal sealed class MyVendorProtocol : BatteryProtocol
{
    public override string Name { get { return "MyVendor"; } }

    // Lower runs first. Compx is 30, the generic HID battery fallback is 90.
    public override int Priority { get { return 55; } }

    public override bool Matches(HidGroup group)
    {
        if (group.Vid != 0x1234) return false;
        return group.ByUsage(0xFF00, 0x0001, 8) != null;
    }

    public override BatteryReading Probe(HidGroup group)
    {
        HidInterface iface = group.ByUsage(0xFF00, 0x0001, 8);
        if (iface == null) return null;

        byte[] req = new byte[8];
        req[0] = 0x02;

        byte[] resp = HidQuery.Run(iface, req, 3, 400, 120,
            new Func<byte[], bool>(got => got != null && got.Length >= 4 && got[0] == 0x02));

        if (resp == null) return null;

        BatteryReading r = new BatteryReading();
        r.Protocol = Name;
        r.RawPercent = resp[2];
        r.Percent = Clamp(resp[2]);
        r.Charge = resp[3] == 0x01 ? ChargeState.Charging : ChargeState.Discharging;
        r.Ok = true;
        return r;
    }
}
```

Then register it in `ProtocolRegistry.All()`.

Rules that matter:

- **`Matches` must be cheap and side-effect free.** It runs for every connected group on every scan.
- **Return `null` when there is no answer**, not a reading with `Ok = false`. `null` lets the next protocol try.
- **Set `Protocol = Name`.** It shows up in the cache file and makes bug reports diagnosable.
- **Set `RawPercent` as well as `Percent`.** The filter needs the raw value; `Percent` is overwritten with the filtered value by the scanner.
- **Do not clamp silently in `Probe`.** `Clamp` exists for genuinely out-of-range conversions, but a byte that reads 116 in a 0–100 field usually means you matched the wrong frame — the scanner would rather drop it than display 100.

If the device reports several batteries (earbuds with a case), fill `LeftPercent` / `RightPercent` / `CasePercent` as well as `Percent`. The flyout shows all of them.

### Why a fresh handle per attempt

`HidQuery.Run` opens a **new handle for every attempt** and closes it before retrying. That is deliberate: a timed-out overlapped read stays pending on the handle and will swallow the next reply, so retrying on the same handle fails forever. Closing the handle cancels the pending I/O.

---

## 5. Verify

```bat
build.bat
Volt.exe
```

Check four things:

1. The percentage matches what the vendor software reports.
2. Unplug the cable — the row should switch transport, not duplicate.
3. Leave the device idle for a few minutes — the row should show `last HH:mm`, not go blank.
4. Mark the model honestly in `docs/DEVICES.md` — `Verified` only if you actually ran it against the hardware. That file is generated: add the key to the `Verified` array in `tools/DevicesDoc.cs`, then run `tools\gen-devices.bat`.

If the name is wrong, the match is probably too loose. Prefer VID/PID plus report lengths over product strings; product strings differ between dongles of the same model, and generic ones like `2.4G Wireless Receiver` are shared across whole vendors.
