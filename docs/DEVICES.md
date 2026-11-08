# Device list

Generated from `src/DeviceTable.cs`. Do not edit by hand.

**137 models, 28 vendors, 217 wireless PIDs, 58 wired PIDs.** 54 models expose both a dongle and a cable.

| Kind | Models |
|---|---|
| Mouse | 60 |
| Headset | 54 |
| Keyboard | 13 |
| Receiver | 7 |
| Controller | 3 |

| Vendor | Models |
|---|---|
| Razer | 46 |
| Logitech | 16 |
| SteelSeries | 12 |
| HyperX | 9 |
| Sony | 6 |
| VXE | 5 |
| ATK | 4 |
| AULA | 4 |
| Corsair | 4 |
| MCHOSE | 4 |
| Jabra | 3 |
| Lamzu | 3 |
| ASUS ROG | 2 |
| Audeze | 2 |
| Dareu | 2 |
| Lenovo | 2 |
| VGN | 2 |
| A4Tech | 1 |
| ASTRO | 1 |
| Attack Shark | 1 |
| Baseus | 1 |
| Flydigi | 1 |
| G-Wolves | 1 |
| NINX | 1 |
| Ninjutso | 1 |
| Plantronics | 1 |
| Scyrox | 1 |
| Zaopin | 1 |

## What "supported" means here

A row means Volt knows the VID:PID and has a protocol implementation for that family. Two levels of confidence:

- **Verified** — read against real hardware on the development machine.
- **Ported** — frame format taken from an open-source implementation or vendor documentation. The protocol family is implemented; the individual model has not been tested.

A model that reports through the standard HID battery page (`0x0006` / `0x0020`) works without appearing in this table at all.

**Wireless** lists the product IDs the device uses on a 2.4G or Bluetooth dongle; **Cable** lists the IDs it uses when plugged in. A model with both appears once in the flyout.

## A4Tech

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Bloody SG5 Pro | Mouse | 3069 | 3144 | Ported |

## ASTRO

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| ASTRO A50 Gen 4 | Headset | 002C | — | Ported |

## ASUS ROG

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| ASUS ROG Falchion | Keyboard | 193E | — | Ported |
| ASUS ROG PELTA | Headset | 1B84 | — | Ported |

## ATK

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| ATK A9 Ultimate | Mouse | 11D9 | 11B6 | Ported |
| ATK F1 Ultimate | Mouse | 1031 | 102E | Ported |
| ATK X1 SE | Mouse | 10C9 | 1107 | Verified |
| ATK Zero | Mouse | 1155 124F | 1154 | Ported |

## AULA

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| AULA (wired) | Keyboard | — | 010B 010C 010D 010E | Ported |
| AULA F68 | Keyboard | FA08 | 010B | Ported |
| AULA F75 / F99 | Keyboard | FA09 | — | Verified |
| AULA F87 | Keyboard | FA0A FA10 | 010D | Ported |

## Attack Shark

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Attack Shark X3 | Mouse | FA60 | FA61 | Ported |

## Audeze

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Audeze Maxwell | Headset | 4B19 | 4B18 | Ported |
| Audeze Maxwell 2 | Headset | 4B29 | 4B28 | Ported |

## Baseus

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Baseus AeQur G10 | Headset | 4801 | — | Ported |

## Corsair

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Corsair Virtuoso SE | Headset | 0A3E | 0A3D | Ported |
| Corsair Virtuoso XT | Headset | 0A64 | 0A62 | Ported |
| Corsair Void | Headset | 0A14 0A16 0A17 0A1A 0A1D 0A2B 0A38 0A4F 0A51 0A52 0A55 0A56 0A75 1B23 1B27 1B29 1B2A | — | Ported |
| Corsair Void V2 | Headset | 2A02 2A08 | — | Ported |

## Dareu

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Dareu A950 Air | Mouse | 1175 | 1193 | Ported |
| Dareu A950 Wing | Mouse | 1154 | 1196 | Ported |

## Flydigi

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Flydigi Vader 5 Pro | Controller | 2401 | — | Ported |

## G-Wolves

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| G-Wolves Lycan | Mouse | 3854 | 4719 | Ported |

## HyperX

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| HyperX Cloud Alpha 2 Wireless | Headset | 08BE | — | Ported |
| HyperX Cloud Alpha S | Headset | 16ED | — | Ported |
| HyperX Cloud Alpha Wireless | Headset | 098D | — | Ported |
| HyperX Cloud Flight Wireless | Headset | 0E90 1723 1749 16C4 | — | Ported |
| HyperX Cloud Flight Wireless | Headset | 1723 | — | Ported |
| HyperX Cloud II Wireless | Headset | 0696 | — | Ported |
| HyperX Cloud II Wireless (Kingston) | Headset | 1718 | — | Ported |
| HyperX Cloud III | Headset | 089D 03CC | — | Ported |
| HyperX Cloud III Wireless | Headset | 05B7 | — | Ported |

## Jabra

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Jabra Evolve2 65 Flex | Headset | 2519 | — | Ported |
| Jabra Link 380 | Headset | 24C7 | — | Ported |
| Jabra Link 390 | Headset | 2E57 | — | Ported |

## Lamzu

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Lamzu Inca | Mouse | 0010 | 0009 | Ported |
| Lamzu Maya X | Mouse | 001E | 001C | Ported |
| Lamzu Thorn V2 | Mouse | 0040 | — | Ported |

## Lenovo

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Lenovo Nano 接收器 | Receiver | 6042 | — | Ported |
| Lenovo Wireless VoIP | Headset | A07D | — | Ported |

## Logitech

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| ASTRO A50 Gen 5 | Headset | 0B1C | — | Ported |
| Logitech Bolt 接收器 | Receiver | C548 | — | Ported |
| Logitech EX100 接收器 | Receiver | C517 | — | Ported |
| Logitech G PRO Series | Headset | 0AA7 0AAA 0ABA 0AFB 0AFC | — | Ported |
| Logitech G PRO X 2 LIGHTSPEED | Headset | 0AF7 | — | Ported |
| Logitech G431/G432/G433 | Headset | 0A9B 0A9C 0A6D | — | Ported |
| Logitech G522 LIGHTSPEED | Headset | 0B18 | — | Ported |
| Logitech G533 | Headset | 0A66 | — | Ported |
| Logitech G535 | Headset | 0AC4 | — | Ported |
| Logitech G633/G733/G933/G935 | Headset | 0A5C 0A89 0A5B 0A87 0AB5 0AFE 0B1F | — | Ported |
| Logitech G930 | Headset | 0A1F | — | Ported |
| Logitech Lightspeed 接收器 | Receiver | C539 C53A C53D C53F C541 C545 C54D | — | Ported |
| Logitech Nano 接收器 | Receiver | C52F C518 C51A C51B C521 C525 C526 C52E C531 C534 C535 C537 | — | Ported |
| Logitech PRO X Wireless | Headset | C547 | — | Ported |
| Logitech Unifying 接收器 | Receiver | C52B C532 | — | Ported |
| Logitech Zone Wired | Headset | 0AAD 0ADE | — | Ported |

## MCHOSE

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| MCHOSE K7 Ultra | Mouse | 1001 | 4150 | Ported |
| MCHOSE L7 Pro | Mouse | 1020 | 00B0 | Ported |
| MCHOSE V9 Pro | Headset | 385D | — | Ported |
| MCHOSE X9 | Headset | 6045 | — | Ported |

## NINX

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| NINX NX60 ANSI | Keyboard | 910B | — | Ported |

## Ninjutso

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Ninjutso Sora V2 | Mouse | AE1C | AE11 | Ported |

## Plantronics

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Plantronics BT600 | Headset | 02EE | — | Ported |

## Razer

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Razer Atheris | Mouse | 0062 | — | Ported |
| Razer Basilisk Mobile | Mouse | 00D3 | — | Ported |
| Razer Basilisk Ultimate | Mouse | 0088 | 0086 | Ported |
| Razer Basilisk V3 Pro | Mouse | 00AB | 00AA | Ported |
| Razer Basilisk V3 Pro 35K | Mouse | 00CD | 00CC | Ported |
| Razer Basilisk V3 Pro 35K Phantom Green | Mouse | 00D7 | 00D6 | Ported |
| Razer Basilisk V3 X HyperSpeed | Mouse | 00B9 | — | Ported |
| Razer Basilisk X HyperSpeed | Mouse | 0083 | — | Ported |
| Razer BlackShark V2 Pro (2.4G) | Headset | 0555 | — | Ported |
| Razer BlackShark V2 Pro (PS) | Headset | 1014 | — | Ported |
| Razer BlackWidow V3 Mini HyperSpeed | Keyboard | 0259 | 0258 | Ported |
| Razer BlackWidow V3 Pro | Keyboard | 025C | 025A | Ported |
| Razer BlackWidow V4 Mini HyperSpeed | Keyboard | 02B8 | 02B9 | Ported |
| Razer BlackWidow V4 TKL HyperSpeed | Keyboard | 02D6 | 02D7 | Ported |
| Razer Cobra HyperSpeed | Mouse | 00DA | — | Ported |
| Razer Cobra Pro | Mouse | 00B0 | 00AF | Ported |
| Razer DeathAdder V2 Pro | Mouse | 007D | 007C | Ported |
| Razer DeathAdder V2 X HyperSpeed | Mouse | 009C | — | Ported |
| Razer DeathAdder V3 HyperSpeed | Mouse | 00C5 | 00C4 | Ported |
| Razer DeathAdder V3 Pro | Mouse | 00B7 | 00B6 | Ported |
| Razer DeathAdder V4 Pro | Mouse | 00BF | 00BE | Ported |
| Razer DeathStalker V2 Pro | Keyboard | 0293 | 0292 | Ported |
| Razer DeathStalker V2 Pro TKL | Keyboard | 0299 | 0298 | Ported |
| Razer HyperPolling Dongle | Receiver | 00B3 | — | Ported |
| Razer Joro | Keyboard | 02CD | — | Ported |
| Razer Lancehead Wireless | Mouse | 0070 | 0059 | Ported |
| Razer Mamba 2012 | Mouse | 0025 | 0024 | Ported |
| Razer Mamba Chroma Wireless | Mouse | 0045 | 0044 | Ported |
| Razer Mamba Wireless | Mouse | 0073 | — | Ported |
| Razer Naga Epic | Mouse | 001F | — | Ported |
| Razer Naga Epic Chroma | Mouse | 003E | — | Ported |
| Razer Naga Pro | Mouse | 0090 | 008F | Ported |
| Razer Naga V2 HyperSpeed | Mouse | 00B4 | — | Ported |
| Razer Naga V2 Pro | Mouse | 00A8 | 00A7 | Ported |
| Razer Orochi V2 | Mouse | 0094 | — | Ported |
| Razer Ouroboros | Mouse | 0032 | — | Ported |
| Razer Pro Click | Mouse | 0077 | — | Ported |
| Razer Pro Click Mini | Mouse | 009A | — | Ported |
| Razer Pro Click V2 | Mouse | 00D1 | 00D0 | Ported |
| Razer Pro Click V2 Vertical Edition | Mouse | 00C8 | 00C7 | Ported |
| Razer Viper Mini SE | Mouse | 009E | — | Ported |
| Razer Viper Ultimate | Mouse | 0078 | 007A | Ported |
| Razer Viper V2 Pro | Mouse | 00A6 | 00A5 | Ported |
| Razer Viper V3 HyperSpeed | Mouse | 00B8 | — | Ported |
| Razer Viper V3 Pro | Mouse | 00C1 | 00C0 | Ported |
| Razer Viper V3 Pro SE | Mouse | 00DD | 00DE | Ported |

## Scyrox

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Scyrox V8 | Mouse | F5F7 | F5F6 | Ported |

## Sony

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Sony DualSense | Controller | 0CE6 | — | Ported |
| Sony DualShock 4 | Controller | 09CC 05C4 | — | Ported |
| Sony INZONE Buds | Headset | 0EC2 | — | Ported |
| Sony INZONE H5 | Headset | 0EBF | — | Ported |
| Sony INZONE H9 II | Headset | 0FA8 | — | Ported |
| Sony PULSE 3D | Headset | 0D5E | — | Ported |

## SteelSeries

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| SteelSeries Arctis 1 | Headset | 12B3 12B6 12D7 12D5 | — | Ported |
| SteelSeries Arctis 7 | Headset | 1260 12AD 1252 1280 | — | Ported |
| SteelSeries Arctis 7+ | Headset | 220E 2212 2216 2236 | — | Ported |
| SteelSeries Arctis 9 | Headset | 12C2 12C4 | — | Ported |
| SteelSeries Arctis GameBuds | Headset | 230A | — | Ported |
| SteelSeries Arctis Nova 3 | Headset | 12EC | — | Ported |
| SteelSeries Arctis Nova 3P | Headset | 2269 226D | — | Ported |
| SteelSeries Arctis Nova 5 | Headset | 2232 2253 | — | Ported |
| SteelSeries Arctis Nova 7 | Headset | 2202 2206 223A 2258 227A 227E 229E 22A1 22A4 22A5 22A9 22AD | — | Ported |
| SteelSeries Arctis Nova 7P | Headset | 220A 2298 22A7 | — | Ported |
| SteelSeries Arctis Nova Pro Wireless | Headset | 12E0 12E5 | — | Ported |
| SteelSeries Arctis Pro Wireless | Headset | 1290 | — | Ported |

## VGN

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| VGN F1 Pro | Mouse | F503 | F502 | Ported |
| VGN F2 Pro Max | Mouse | FB3E | FB3D | Ported |

## VXE

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| VXE MAD R | Mouse | 104D | 103F | Ported |
| VXE MAD R Major Plus | Mouse | 1040 | 104C | Ported |
| VXE R1 Pro Max | Mouse | F58A | F58C | Ported |
| VXE R1 SE+ | Mouse | F58E | F58F | Ported |
| VXE R1 SE+ (1K dongle) | Mouse | 1085 | — | Ported |

## Zaopin

| Model | Kind | Wireless | Cable | Status |
|---|---|---|---|---|
| Zaopin Z2 Mini | Mouse | F524 | F526 | Ported |
