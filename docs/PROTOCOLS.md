# Protocols

<sub>[English](PROTOCOLS.md) · [简体中文](PROTOCOLS.zh-CN.md)</sub>

Everything here was derived by sending frames to real hardware and reading the replies. Where a claim comes from someone else's source, the repository is named.

Both supported devices sit on the **Compx** radio platform, but they do not share a frame layout. They expose no standard HID battery usage page (`0x06 / 0x20`), so Windows never sees a battery for them. The level is only available through a vendor-private collection with usage page `0xFF02`.

---

## Compx / ATK family — 17-byte report

Used by ATK X1 SE over both the 2.4G dongle (`373B:10C9`) and the USB cable (`373B:1107`). The collection is `FF02/0002` with input and output report length 17.

### Frame layout

```
[0]     report ID            0x08
[1]     command              0x04 = get battery
[2]     status
[3..4]  EEPROM address, big endian
[5]     data length
[6..15] payload
[16]    checksum
```

### Checksum

```
checksum = (0x55 - sum(bytes[0..15])) & 0xFF
```

A frame with the first two bytes `08 04` and everything else zero has `sum = 0x0C`, so the checksum byte is `0x49`. That gives the shortest working request:

```
08 04 00 00 00 00 00 00 00 00 00 00 00 00 00 00 49
```

The device validates the whole frame, not just the checksum byte. Filling byte `[2]` with `0x3D` to make the sum come out to `0x49` produces a timeout — the device is checking that the payload fields are consistent with the command, not merely that the arithmetic adds up.

### Response

```
08 04 00 00 00 02 32 00 0E A3 00 00 00 00 00 00 64
              ^^ ^^ ^^^^^
              |  |  └── [8..9] pack voltage, big endian, mV  (0x0EA3 = 3747 mV)
              |  └───── [7]    charging flag, 1 = charging
              └──────── [6]    percentage, 0..100
```

`[16]` is the checksum of the reply, same formula. **Volt verifies it**: a reply is accepted only when all 17 bytes sum to `0x55` modulo 256, not merely when the first two bytes are `08 04`.

That second check matters more than it looks. The interface also carries unrelated traffic, and a frame that happens to start with `08 04` but is not a battery reply will yield garbage in `[6]` — observed values include 116, 123 and 127. Checking the checksum drops those before they reach the parser.

Voltage is only accepted when it falls strictly inside `2000 < mV < 5000` — an open interval, so `2000` and `5000` themselves are rejected. That filters out devices that leave the field zeroed.

### Interface caveat

The ATK dongle exposes **two** `FF02/0002` collections: a 20-byte one (`col04`) and a 17-byte one (`col05`). Only the 17-byte collection accepts this frame. Writing 20 bytes to `col04` returns `ERROR_INVALID_FUNCTION` (1); writing 17 or 19 bytes returns `ERROR_INVALID_PARAMETER` (87).

This matters because a naive `FirstOrDefault` over the matching collections picks the 20-byte one first. Volt matches on VID/PID **and** both report lengths for exactly this reason.

---

## AULA family — 20-byte report

Used by the AULA F99 over its 2.4G dongle (`3554:FA09`). The collection is `FF02/0002`, 20 bytes in and out.

### Request

```
13 CA 00 00 ... 00 <sum8>
```

`sum8` is the low byte of the sum of the first 19 bytes, which for this frame is `0xDD`.

### Response

```
13 4A 01 00 02 33 10 00 00 00 00 00 00 00 00 00 00 00 00 A3
^^^^^^^^^^^ ^^ ^^
    |        |  └── [6] status: 0x10 = charging, 0x01 = discharging
    |        └───── [5] percentage, 0..100  (0x33 = 51)
    └────────────── [0..4] fixed prefix
```

There is a **second reply shape** on the same endpoint:

```
13 0A 01 00 04 05 60 10 00 00 00 00 00 00 00 00 00 00 00 97
^^^^^^^^^^^^^^^^^ ^^ ^^
       |           |  └── [7] status, same encoding
       |           └───── [6] percentage  (0x60 = 96)
       └───────────────── [0..5] fixed prefix
```

Both were observed from the same keyboard within seconds of each other. The `13 0A` shape is what a `13 05 01` query returns — that is the sub-command used by [2.4g-battery-tray-python](https://github.com/ahutsunboy/2.4g-battery-tray-python), which reads the percentage from index 6. So the endpoint answers at least two different commands with two different layouts, and a parser that only knows `13 4A` will treat every `13 0A` reply as silence. Volt matches both prefixes and reads the percentage from the offset that belongs to each.

The same command is used by other AULA models and by LEOBOG keyboards on the same VID. Third-party projects ([AulaTray](https://github.com/brhnshn/AulaTray), [AulaMonitor](https://github.com/Makkksx/AulaMonitor)) agree on `0xFF02` / `0x13 0x4A`.

### Interface caveat

This HID path intermittently disappears between reads — open succeeds, then the next open returns `ERROR_FILE_NOT_FOUND` (2). Re-enumerating before each sample fixes it. Volt enumerates once per scan and opens a fresh handle per attempt.

---

## Headsets

Headsets do not share one protocol the way the Compx mice do. Each vendor has its own frame, and several of them speak over **feature reports** rather than output reports — which is a different code path in `hid.dll` and is why `HidNative.Handle` grew `SetFeature` / `GetFeature`.

All of the formats below were ported from [Sapd/HeadsetControl](https://github.com/Sapd/HeadsetControl) (`lib/devices/*.hpp`). None of them has been exercised against the physical device on this machine; they are implemented, not verified.

| Family | Transport | Request | Percentage at | Notes |
|---|---|---|---|---|
| Sony INZONE Buds | push only | none | `[16]` left, `[14]` right, `[18]` case | accepts a frame when `[1]==0x12 && [2]==0x04`; `[19]` must equal `(left + right + 117) & 0xFF` |
| Audeze Maxwell | push | `06 05 5A 05 D6 0C` | `[8]` | reply marker `0x05`, type `0x5D` |
| Corsair Virtuoso XT / SE | push | 64 bytes, report `0x02`, target `0x09` then `0x08`, property `0x0F` | `[7..4]` little-endian, ÷10 | property `0x10` is the charge state, `1` = charging |
| Corsair Void | output | `C9 64` | `[2] & 0x7F` | bit 7 of `[2]` is a mic-up flag; `[4]` is the charge state |
| Jabra (GNP) | push | 64 bytes, report `0x05`, cmd `0x12`, sub `0x02` | `[8]` | `[4] >> 6` must be `0x03` (response); `[7] & 0x01` is charging |
| Lenovo Wireless VoIP | feature | `24 01` | `[7]` | reply ID `0x27` |
| Plantronics BT600 | push | 64 bytes, report `0x07`, `02 0A 1A` at `[8..10]` | `[11] × 100 ÷ ([12] − 1)` | `[13]` is charging; `[12] < 2` means a malformed reply |
| MCHOSE X9 / V9 Pro | output | `55 65 01` | `[2]` | reply must have `[3]==0x02`; lives on usage page `0xFF90` |
| SteelSeries legacy (Arctis 1/7/9, Pro) | push | 31-byte packet, command per model | mapped from a per-model range | each model has its own command, byte offset, offline value and 0–4 vs 0–100 scale |
| SteelSeries Nova (Arctis 7+, Nova 3/5/7/7P/Pro, GameBuds) | push | `00 20` | `[2]` | `[3]` is `0` offline, `1`/`2` charging; six older PIDs report 0–4 instead of 0–100 |

### Three batteries, one device

A true-wireless headset is not one battery. It is three: left bud, right bud, charging case. Every open-source implementation reviewed here collapses that to a single number — HeadsetControl's Sony driver parses all three bytes and then returns `std::min(left, right)`, discarding the case.

`BatteryReading` therefore carries `LeftPercent`, `RightPercent` and `CasePercent` alongside `Percent`, and the flyout shows `L 88% R 87% C 100%` when more than one is present. Only the Sony INZONE Buds driver populates all three today, because it is the only 2.4G dongle in the reviewed set that actually transmits them.

### Why most 2.4G audio dongles show nothing

A headset dongle is an audio endpoint first. The vendor usually keeps battery telemetry on the **Bluetooth** side (BLE GATT, or a vendor advertisement payload) and never exposes it over USB HID.

The Baseus AeQur G10 (`10D6:4801`) is the worked example. It exposes two vendor collections, `FF90/0001` with report ID `0x55` and `FF82/0001` with report ID `0x41`, and both look like plausible telemetry channels. They are not:

- `0x55` and `0x41` are **Actions ADFU bootloader entry triggers**. Writing an output report to either one puts the dongle into firmware-upgrade mode until it is physically replugged.
- `0x06` and `0x07` are Consumer Control (volume) and Telephony (mute LED).

Forty-eight combinations of a JieLi-style TLV query across those channels produced zero replies, which is consistent with there being nothing to reply. A model known to be in this state is marked with a note in `src/DeviceTable.cs` and is left out of the flyout rather than shown as a permanent blank row — the alternative was a row that can never fill in.

Source: [`ryan-mant/AreYouLowBattery`](https://github.com/ryan-mant/AreYouLowBattery), `areyoulowbattery/handlers/actions.py`, whose header documents the same finding for `10d6:4802`.

**Do not write to `0x55` or `0x41` on an Actions-based dongle.** Reading is harmless; writing can leave the device unusable until it is unplugged.

---

## Sleep behaviour

Neither device pushes unsolicited reports. Both must be polled, and both go quiet when idle:

- The keyboard stops answering after a few minutes of no input. A single keypress wakes it.
- The mouse answers reliably while in use.

Because the failure mode is a timeout rather than an error, Volt keeps the last good reading and labels it `last HH:mm`. A stale value beats a blank row.

Note that polling resets the device idle timer, which costs battery. Volt polls every 5 seconds while the flyout is open and every 45 seconds while it is closed; the slower idle rate is deliberately above the 30–60 s sleep timer most of these keyboards use, so the poll does not keep waking the device.

---

## Reading the percentage

Raw percentage bytes are noisier than they look, and the fix is not to smooth them.

A lithium cell's terminal voltage sags under load and recovers when the load goes away. Any device that estimates charge from voltage — which is what these peripherals do — will therefore report a *higher* level the moment you stop using it, or the moment you unplug the charging cable. That is the mechanism behind a keyboard reading 96%, dropping to 84% when unplugged, and climbing back to 96%.

Microsoft's Bluetooth accessory guidance settles the question: battery level is a **decrease-only** attribute. It may only increase when the device is on a charger, or when the battery was physically removed and reinstalled. The same document gives the exact scenario — a mouse measuring 5%, entering a low-power state, and measuring 10% because the load dropped — and states the reported value must stay at 5%.

Volt implements that rule in `src/Filter.cs`, plus three guards:

- **Bad values are dropped.** `[6]` outside 1..100 is discarded rather than clamped.
- **Large drops must persist.** A single bad frame would otherwise pin the display forever, since the value is now allowed to fall but not to rise. A drop of more than one point is accepted only after it has stayed below the previous value for 10 seconds across at least two readings. A one-point drop is accepted immediately, because that is ordinary discharge.
- **Long gaps resync.** If more than 5 minutes passed between readings — sleep, hibernation, program start — the new value is taken as-is. The alternative is a stale number that can never climb back.

Changing the connection (dongle ↔ cable) also resyncs, for the same reason Microsoft allows an increase after a battery swap: everything that happened in between is unobserved.

Why not an average, a median filter, or a Kalman filter? Because the noise here is not measurement noise around a true value; it is a systematic bias that appears and disappears with load. Smoothing it produces a number that lags reality and still moves in the wrong direction. The decrease-only rule removes the wrong-direction movement at the source, and leaves the genuine discharge curve intact.

---

## Discovering a new device

1. Enumerate HID collections and look for a vendor usage page (`>= 0xFF00`) with symmetric input and output lengths.
2. Send a frame with the known command bytes and read the reply.
3. Diff replies across charging states and battery levels to find the percentage byte and the charging flag.

If the vendor ships a WebHID-based configurator, reading its JavaScript is faster than capturing USB traffic — the frame layout is usually spelled out there.

`docs/ADDING-A-DEVICE.md` walks through wiring the result into the catalogue.
