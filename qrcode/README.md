# qrcode

Generate or Scan QR code.

```
Usage of qrcode:
  -c string
    	content to create qrcode
  -f string
    	input/output file name
  -s int
    	image size (default 256)
```

```bash
# generate qrcode and save to file `qrcode.ong`
qrcode -c https://github.com
# scan qrcode.png
qrcode -f qrcode.png
```
