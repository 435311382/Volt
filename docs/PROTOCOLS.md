# Protocols

Everything here was derived by sending frames to real hardware and reading the replies. Where a claim comes from someone else's source, the repository is named.

Both supported devices sit on the **Compx** radio platform. They expose no standard HID battery usage page (`0x06 / 0x20`), so Windows never sees a battery for them. The level is only available through a vendor-private collection with usage page `0xFF02`.

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

`[16]` is the checksum of the reply, same formula. Verify it if you like; Volt only checks the `08 04` prefix.

Voltage is only accepted when it falls in 2000–5000 mV, which filters out devices that leave the field zeroed.

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

The same command is used by other AULA models and by LEOBOG keyboards on the same VID. Third-party projects ([AulaTray](https://github.com/brhnshn/AulaTray), [AulaMonitor](https://github.com/Makkksx/AulaMonitor)) agree on `0xFF02` / `0x13 0x4A`.

### Interface caveat

This HID path intermittently disappears between reads — open succeeds, then the next open returns `ERROR_FILE_NOT_FOUND` (2). Re-enumerating before each sample fixes it. Volt enumerates once per scan and opens a fresh handle per attempt.

---

## Sleep behaviour

Neither device pushes unsolicited reports. Both must be polled, and both go quiet when idle:

- The keyboard stops answering after a few minutes of no input. A single keypress wakes it.
- The mouse answers reliably while in use.

Because the failure mode is a timeout rather than an error, Volt keeps the last good reading and labels it `last HH:mm`. A stale value beats a blank row.

Note that polling resets the device idle timer, which costs battery. Five seconds is a reasonable compromise; a longer interval is friendlier to the pack.

---

## Discovering a new device

1. Enumerate HID collections and look for a vendor usage page (`>= 0xFF00`) with symmetric input and output lengths.
2. Send a frame with the known command bytes and read the reply.
3. Diff replies across charging states and battery levels to find the percentage byte and the charging flag.

If the vendor ships a WebHID-based configurator, reading its JavaScript is faster than capturing USB traffic — the frame layout is usually spelled out there.

`docs/ADDING-A-DEVICE.md` walks through wiring the result into the catalogue.
